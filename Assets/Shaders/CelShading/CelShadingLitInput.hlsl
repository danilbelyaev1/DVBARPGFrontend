#ifndef GAMECLIENT_CEL_SHADING_LIT_INPUT_INCLUDED
#define GAMECLIENT_CEL_SHADING_LIT_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    float4 _BaseMap_TexelSize;
    half4 _BaseColor;
    // Must mirror [HideInInspector] _Color in CelShadingLit.shader — otherwise GPU ignores per-material tint
    // (Standard / legacy materials often only set _Color; URP uses _BaseColor as Main Color).
    half4 _Color;
    half4 _AmbientColor;
    half4 _SpecularColor;
    half _Glossiness;
    half _SpecularIntensity;
    half _SpecularAlbedoMix;
    half _SpecularLightMix;
    half _SpecularBrightAlbedoReduce;
    half4 _RimColor;
    half _RimAmount;
    half _RimThreshold;
    half4 _ShadowTint;
    half _ShadowStep;
    half _HighlightStep;
    half _LightBandLower;
    half _LightBandUpper;
    half _CelMidMul;
    half _CelHighlightStart;
    half _DirectLightScale;
    half _MaxDiffuseIrradiance;
    half _MaxOutputRgb;
    half _AdditionalLightsStrength;
    half _SpecularEdgeLower;
    half _SpecularEdgeUpper;
    half _RimEdgeWidth;
    half _RimStrength;
    half4 _OutlineColor;
    half _OutlineWidth;
    half4 _InkColor;
    half _InkEdgeStrength;
    half _InkEdgeWidth;
    half4 _EmissionColor;
    half _Cutoff;
    half _Surface;
    half _SrcBlend;
    half _DstBlend;
    half _ZWrite;
    half _LightingEnabled;
    half4 _TintColor;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS;
CBUFFER_END

half4 CelMaterialColorFactor()
{
    return _BaseColor * _Color;
}

#endif
