Shader "GameClient/CelShadingLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1, 1, 1, 1)

        [HDR] _AmbientColor("Ambient Color", Color) = (0.28, 0.30, 0.36, 1)
        [HDR] _SpecularColor("Specular Color", Color) = (0.75, 0.76, 0.8, 1)
        _Glossiness("Glossiness", Range(4, 128)) = 14
        _SpecularIntensity("Specular Intensity", Float) = 0.85
        _SpecularAlbedoMix("Specular To Albedo Mix", Range(0, 1)) = 0.55
        _SpecularLightMix("Specular From Light Mix", Range(0, 1)) = 0.35
        _SpecularBrightAlbedoReduce("Reduce Specular On Bright Albedo", Range(0, 1)) = 0.45

        [HDR] _RimColor("Rim Color", Color) = (0.85, 0.9, 1, 1)
        _RimAmount("Rim Amount", Range(0, 1)) = 0.716
        _RimThreshold("Rim Threshold", Range(0, 1)) = 0.1
        _RimStrength("Rim Strength", Range(0, 2)) = 0.28

        [Header(Zelda Outline Ink)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width (pixels)", Range(0, 8)) = 1.6
        _InkColor("Ink Color", Color) = (0, 0, 0, 1)
        _InkEdgeStrength("Ink Edge Strength", Range(0, 1)) = 0.18
        _InkEdgeWidth("Ink Edge Width (NdotL)", Range(0.001, 0.08)) = 0.02

        [Header(Cel Ramp BotW Style)]
        [HDR] _ShadowTint("Shadow Tint", Color) = (0.30, 0.35, 0.45, 1)
        _ShadowStep("Shadow Step", Range(0, 1)) = 0.22
        _HighlightStep("Highlight Step", Range(0, 1)) = 0.62
        _LightBandLower("Terminator Start (N dot L)", Range(-0.35, 0.2)) = -0.08
        _LightBandUpper("Terminator Feather", Range(0.02, 0.35)) = 0.09
        _CelMidMul("Mid-Tone Brightness", Range(0.35, 1)) = 0.58
        _CelHighlightStart("Highlight Start (N dot L)", Range(0, 0.95)) = 0.42
        _DirectLightScale("Sun Strength On Material", Range(0.25, 1.5)) = 0.72
        _MaxDiffuseIrradiance("Max Diffuse (антизасвет)", Range(0.7, 1.35)) = 1.0
        _MaxOutputRgb("Max RGB (весь свет кроме emission)", Range(0.6, 2)) = 1.1
        _AdditionalLightsStrength("Additional Lights Strength", Range(0, 1)) = 0.25

        _SpecularEdgeLower("Specular Edge Lower", Range(0, 0.2)) = 0.0005
        _SpecularEdgeUpper("Specular Edge Upper", Range(0, 0.2)) = 0.1
        _RimEdgeWidth("Rim Edge Width", Range(0, 0.2)) = 0.01

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        _Surface("__surface", Float) = 0
        _Blend("__blend", Float) = 0
        _Cull("__cull", Float) = 2
        [ToggleUI] _AlphaClip("__clip", Float) = 0
        [HideInInspector] _SrcBlend("__src", Float) = 1
        [HideInInspector] _DstBlend("__dst", Float) = 0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0
        [HideInInspector] _ZWrite("__zw", Float) = 1
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0
        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1
        _QueueOffset("Queue offset", Float) = 0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            // Deferred: без UniversalGBuffer + тип Lit URP не кладёт материал в GBuffer → розовый/битый кадр.
            // Как URP/Unlit: GBuffer + проход без LightMode (неявный SRPDefaultUnlit) — так же рисуется и после deferred.
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }
        LOD 100

        // Outline pass disabled to avoid transparency regressions.

        // Без Tags LightMode — как URP/Unlit: SRPDefaultUnlit (Forward opaque + Draw Opaques Forward Only в deferred).
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" "RenderType" = "Opaque" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex CelShadingLitPassVertex
            #pragma fragment CelShadingLitPassFragment

            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "CelShadingLitInput.hlsl"
            #include "CelShadingLitForwardPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore

            #pragma vertex CelShadingLitGBufferVertex
            #pragma fragment CelShadingLitGBufferFragment

            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON

            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_fragment _ SHADOWS_SHADOWMASK
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "CelShadingLitGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "CelShadingLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "CelShadingLitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "CelShadingDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment CelShadingUniversalFragmentMeta
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "CelShadingLitMetaPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Simple Lit"
}
