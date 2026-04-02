#ifndef GAMECLIENT_CEL_SHADING_LIT_FORWARD_PASS_INCLUDED
#define GAMECLIENT_CEL_SHADING_LIT_FORWARD_PASS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

// HDR-цвет в инспекторе часто > 1 по каналам — при слабом directional весь свет идёт отсюда → «всё белое».
half3 CelClampHdrRgb(half3 c, half maxChannel)
{
    half m = max(c.r, max(c.g, c.b));
    return (m > maxChannel && m > 1e-4h) ? c * (maxChannel / m) : c;
}

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half3 normalWS : TEXCOORD2;
    half fogCoord : TEXCOORD3;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord : TEXCOORD4;
#endif
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings CelShadingLitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);

#if defined(_FOG_FRAGMENT)
    output.fogCoord = half(vertexInput.positionVS.z);
#else
    output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif

    return output;
}

void CelShadingLitPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
    half3 albedo = tex.rgb * _BaseColor.rgb;
    half alpha = tex.a * _BaseColor.a;
    alpha = AlphaDiscard(alpha, _Cutoff);
    albedo = AlphaModulate(albedo, alpha);

#ifdef LOD_FADE_CROSSFADE
    LODFadeCrossFade(input.positionCS);
#endif

    half3 N = normalize(input.normalWS);
    float3 posWS = input.positionWS;

    // Всегда считаем координаты теней в пикселе: интерполяция из вершин на крупных треугольниках даёт «полосы» и пикселизацию на земле.
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
    float4 shadowCoord = TransformWorldToShadowCoord(posWS);
#else
    float4 shadowCoord = float4(0, 0, 0, 0);
#endif

    Light mainLight = GetMainLight(shadowCoord);
    // URP кладёт в color уже * intensity, но в HDR/Linear значения часто >1 — без clamp «белит» всё независимо от слайдера солнца.
    half3 mainCol = CelClampHdrRgb(mainLight.color, 1.0h);
    half3 L = mainLight.direction;
    half NdotL = dot(N, L);
#if defined(_RECEIVE_SHADOWS_OFF)
    half shadowAtten = 1.0h;
#else
    half shadowAtten = mainLight.shadowAttenuation;
#endif
    half nd = NdotL * shadowAtten;
    half mainLightAtten = half(mainLight.distanceAttenuation);

    // Step-based ramp (как в Shader Graph из референса): чёткие мультяшные полосы света.
    half feather = max(_LightBandUpper, 0.02h);
    half midMul = (_CelMidMul > 0.01h) ? _CelMidMul : 0.58h;
    half hi0 = (_CelHighlightStart > 0.01h) ? _CelHighlightStart : 0.42h;
    half dirScale = (_DirectLightScale > 0.01h) ? _DirectLightScale : 0.72h;
    half tTerm = smoothstep(_LightBandLower, _LightBandLower + feather, nd);
    half tHi = smoothstep(hi0, hi0 + feather, nd);
    half cel = lerp(0.0h, lerp(midMul, 1.0h, tHi), tTerm);

    half nd01 = saturate(nd);
    half shStep = saturate(_ShadowStep);
    half hiStep = max(saturate(_HighlightStep), shStep + 0.02h);
    half s1 = step(shStep, nd01);
    half s2 = step(hiStep, nd01);
    half3 shadowTint = CelClampHdrRgb(_ShadowTint.rgb, 1.0h);
    half3 midTint = lerp(shadowTint, half3(1.0h, 1.0h, 1.0h), midMul);
    half3 rampTint = lerp(shadowTint, midTint, s1);
    rampTint = lerp(rampTint, half3(1.0h, 1.0h, 1.0h), s2);

    half3 directLight = cel * rampTint * mainCol * mainLightAtten * dirScale;

    half3 ambient = CelClampHdrRgb(_AmbientColor.rgb, 0.85h);

    half3 V = GetWorldSpaceNormalizeViewDir(posWS);
    float3 H = SafeNormalize(float3(L) + float3(V));
    float nh = saturate(dot(float3(N), H));
    // Если Glossiness = 0, по ожиданиям блик должен исчезать полностью.
    // Раньше показатель искусственно зажимался минимумом -> получался "спекуляр на всём".
    half specOn = (_Glossiness > 0.001h) ? 1.0h : 0.0h;
    float gloss = float(max(_Glossiness, 0.001h));

    // Блик только там, где есть directional cel (ноль в собственной тени персонажа).
    float specLinear =
        pow(nh, gloss)
        * float(cel)
        * float(mainLightAtten)
        * float(max(_SpecularIntensity, 0.0h)) * float(specOn);
    // Второй smoothstep — более «мультяшный» диск вместо размазанного pow.
    half specSmooth = half(smoothstep(float(_SpecularEdgeLower), float(_SpecularEdgeUpper), specLinear));
    float nhToon = smoothstep(0.75, 0.995, float(nh)) * float(cel) * float(mainLightAtten);
    half specToon = half(smoothstep(0.15, 0.75, nhToon));
    half specMask = max(specSmooth, specToon * 0.35h) * specOn * s1;
    half albMix = saturate(_SpecularAlbedoMix);
    half ltMix = saturate(_SpecularLightMix);
    half3 specTint = lerp(CelClampHdrRgb(_SpecularColor.rgb, 1.0h), albedo, albMix);
    half3 specLight = lerp(half3(1.0h, 1.0h, 1.0h), mainCol, ltMix);
    half3 specularContrib = specMask * specTint * specLight * 0.42h;
    // Preserve light-hair gradients: reduce specular when albedo is already bright.
    half albedoLuma = dot(albedo, half3(0.299h, 0.587h, 0.114h));
    half brightAlbedo = smoothstep(0.65h, 1.0h, albedoLuma);
    half brightReduce = lerp(1.0h, 1.0h - saturate(_SpecularBrightAlbedoReduce), brightAlbedo);
    specularContrib *= brightReduce;

    half rimDot = 1.0h - saturate(dot(V, N));
    half rimIntensity = rimDot * pow(saturate(NdotL), max(_RimThreshold, 1e-4h));
    rimIntensity = smoothstep(_RimAmount - _RimEdgeWidth, _RimAmount + _RimEdgeWidth, rimIntensity);
    half rimSun = lerp(0.35h, 1.0h, tTerm);
    half3 rimRgb = CelClampHdrRgb(_RimColor.rgb, 1.0h);
    half3 rim = rimIntensity * rimRgb * _RimStrength * rimSun;

    // "Инк" на границе терминатора (граница свет/тень) — помогает приблизить Zelda-like рисованный контраст.
    half termMid = _LightBandLower + feather * 0.5h;
    half inkMask = 1.0h - smoothstep(0.0h, _InkEdgeWidth, abs(nd - termMid));
    half inkA = inkMask * saturate(_InkEdgeStrength);
    half3 inkRgb = CelClampHdrRgb(_InkColor.rgb, 1.0h);

    half3 irradiance = ambient + directLight + rim;
    irradiance = max(irradiance - inkA * inkRgb, half3(0.0h, 0.0h, 0.0h));
    half cap = (_MaxDiffuseIrradiance > 0.05h) ? _MaxDiffuseIrradiance : 1.05h;
    cap = clamp(cap, 0.75h, 1.35h);
    irradiance = min(irradiance, half3(cap, cap, cap));
    half3 color = albedo * irradiance + specularContrib;

#if defined(_ADDITIONAL_LIGHTS)
    // Forward+/Deferred+: LIGHT_LOOP_BEGIN использует inputData (см. RealtimeLights.hlsl), не Varyings input.
    InputData inputData;
    inputData = (InputData)0;
    inputData.positionWS = posWS;
    inputData.normalWS = N;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, posWS);
        half nl = dot(N, light.direction);
        half atten = light.distanceAttenuation * light.shadowAttenuation;
        half nda = nl * atten;
        half ta = smoothstep(_LightBandLower, _LightBandLower + feather, nda);
        half tHa = smoothstep(hi0, hi0 + feather, nda);
        half cela = lerp(0.0h, lerp(midMul, 1.0h, tHa), ta);
        half3 addCol = CelClampHdrRgb(light.color, 1.0h);
        color += albedo * (cela * addCol * dirScale * saturate(_AdditionalLightsStrength));
    LIGHT_LOOP_END
#endif

    // Доп. огни шли мимо cap на irradiance — суммировались в «квадрат белого».
    half maxOut = (_MaxOutputRgb > 0.02h) ? _MaxOutputRgb : 1.1h;
    maxOut = clamp(maxOut, 0.55h, 2.0h);
    half peak = max(color.r, max(color.g, color.b));
    color = (peak > maxOut && peak > 1e-4h) ? color * (maxOut / peak) : color;

#ifdef _EMISSION
    half3 em = SampleEmission(input.uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    em = CelClampHdrRgb(em, 2.0h);
    color += em;
#endif

    half fogFactor = 0;
#if defined(_FOG_FRAGMENT)
    bool anyFogEnabled = false;
#if defined(FOG_LINEAR_KEYWORD_DECLARED)
    if (FOG_LINEAR)
        anyFogEnabled = true;
#endif
#if defined(FOG_EXP_KEYWORD_DECLARED)
    if (FOG_EXP)
        anyFogEnabled = true;
#endif
#if defined(FOG_EXP2_KEYWORD_DECLARED)
    if (FOG_EXP2)
        anyFogEnabled = true;
#endif
    if (anyFogEnabled)
    {
        float viewZ = -input.fogCoord;
        float nearToFarZ = max(viewZ - _ProjectionParams.y, 0);
        fogFactor = ComputeFogFactorZ0ToFar(nearToFarZ);
    }
#else
    fogFactor = input.fogCoord;
#endif
    color = MixFog(color, fogFactor);

    outColor = half4(color, OutputAlpha(alpha, IsSurfaceTypeTransparent(_Surface)));

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif
