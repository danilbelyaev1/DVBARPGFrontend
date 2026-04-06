#ifndef GAMECLIENT_CEL_SHADING_META_PASS_INCLUDED
#define GAMECLIENT_CEL_SHADING_META_PASS_INCLUDED

#include "CelShadingLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

half4 CelShadingUniversalFragmentMeta(Varyings input) : SV_Target
{
    float2 uv = input.uv;
    MetaInput metaInput;
    metaInput.Albedo = CelMaterialColorFactor().rgb * SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
    metaInput.Emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));
    return UniversalFragmentMeta(input, metaInput);
}

#endif
