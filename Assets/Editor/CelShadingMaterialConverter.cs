using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Converts arbitrary materials (Synty Shader Graph, URP Lit, Standard) to GameClient/Cel Shading Lit
/// by copying the best-matching albedo, color, emission, alpha clip, and cull state.
/// </summary>
public static class CelShadingMaterialConverter
{
    public const string CelShaderName = "GameClient/CelShadingLit";

    static readonly string[] AlbedoTexturePropertyCandidates =
    {
        "_BaseMap",
        "_ColorMap",
        "_Albedo_Map",
        "_AlbedoMap",
        "_BaseColorMap",
        "_MainTex",
        "_MainTexture",
        "_Diffuse",
        "_DiffuseMap",
        "_DiffuseTexture",
        "_Texture",
        "_Albedo",
        "_Tex",
        "_TilingTex",
    };

    static readonly string[] EmissionTexturePropertyCandidates =
    {
        "_EmissionMap",
        "_Emission_Map",
        "_EmissiveMap",
        "_EmissionTex",
    };

    static readonly string[] BaseColorPropertyCandidates =
    {
        "_BaseColor",
        "_Color",
        "_TintColor",
        "_Base_Color",
    };

    static readonly string[] EmissionColorPropertyCandidates =
    {
        "_EmissionColor",
        "_EmissiveColor",
        "_Emission_Color",
    };

    static readonly string[] CutoffPropertyCandidates =
    {
        "_Cutoff",
        "_AlphaCutoff",
        "_Alpha_Clip_Threshold",
    };

    public struct ConversionOptions
    {
        public bool SkipIfAlreadyCel;
        public bool SkipUiHudFontsTmpPaths;
        public bool SkipParticleShaders;
        public bool SkipSkyboxShaders;
        public bool SkipTransparentRenderQueue;

        public static ConversionOptions ForBulkSyntyDefault => new ConversionOptions
        {
            SkipIfAlreadyCel = true,
            SkipUiHudFontsTmpPaths = true,
            SkipParticleShaders = true,
            SkipSkyboxShaders = true,
            SkipTransparentRenderQueue = false,
        };

        public static ConversionOptions ForSelectionOnly => new ConversionOptions
        {
            SkipIfAlreadyCel = true,
            SkipUiHudFontsTmpPaths = false,
            SkipParticleShaders = false,
            SkipSkyboxShaders = false,
            SkipTransparentRenderQueue = false,
        };
    }

    public static Shader FindCelShader()
    {
        return Shader.Find(CelShaderName);
    }

    /// <summary>Non-null string means the material should be skipped (filters only, before conversion).</summary>
    public static string GetFilterSkipReason(Material mat, string assetPath, in ConversionOptions options)
    {
        if (mat == null)
            return "material is null";

        var cel = FindCelShader();
        if (cel == null)
            return $"shader not found: {CelShaderName}";

        if (options.SkipIfAlreadyCel && mat.shader == cel)
            return "already cel";

        if (string.IsNullOrEmpty(assetPath))
            return "not a saved asset (scene/prefab instance?)";

        if (options.SkipUiHudFontsTmpPaths && ShouldSkipByAssetPath(assetPath))
            return "skipped path (UI/HUD/Fonts/TMP)";

        var shaderName = mat.shader != null ? mat.shader.name : "";
        if (options.SkipParticleShaders && ShouldSkipByShaderNameParticle(shaderName))
            return "skipped shader (particle/vfx)";

        if (options.SkipSkyboxShaders && ShouldSkipSkybox(mat, shaderName))
            return "skipped skybox";

        if (options.SkipTransparentRenderQueue && mat.renderQueue >= (int)RenderQueue.Transparent)
            return "skipped transparent render queue";

        return null;
    }

    public static bool TryConvert(Material mat, in ConversionOptions options, out string message)
    {
        message = null;
        if (mat == null)
        {
            message = "material is null";
            return false;
        }

        var path = AssetDatabase.GetAssetPath(mat);
        var skip = GetFilterSkipReason(mat, path, options);
        if (skip != null)
        {
            message = skip;
            return false;
        }

        var cel = FindCelShader();
        var oldShader = mat.shader;
        var oldShaderName = oldShader != null ? oldShader.name : "";

        var albedoProp = FindFirstAssignedTextureProperty(mat, AlbedoTexturePropertyCandidates, out var albedoTex);
        Vector2 scale = Vector2.one;
        Vector2 offset = Vector2.zero;
        if (albedoProp != null)
        {
            scale = mat.GetTextureScale(albedoProp);
            offset = mat.GetTextureOffset(albedoProp);
        }

        var baseColor = PickFirstColor(mat, BaseColorPropertyCandidates, Color.white);

        _ = FindFirstAssignedTextureProperty(mat, EmissionTexturePropertyCandidates, out var emissiveTex);
        var emissionColor = PickFirstColor(mat, EmissionColorPropertyCandidates, Color.black);
        var useEmission = emissiveTex != null || EmissionColorIsNonBlack(emissionColor);

        var wantsAlphaClip = DetectAlphaClip(mat);
        var cutoff = PickFirstFloat(mat, CutoffPropertyCandidates, 0.5f);

        float cull = 2f;
        if (mat.HasProperty("_Cull"))
            cull = mat.GetFloat("_Cull");

        var prevQueue = mat.renderQueue;

        mat.shader = cel;

        if (albedoTex != null && mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", albedoTex);
            mat.SetTextureScale("_BaseMap", scale);
            mat.SetTextureOffset("_BaseMap", offset);
        }

        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", baseColor);

        if (mat.HasProperty("_EmissionColor"))
            mat.SetColor("_EmissionColor", emissionColor);

        if (emissiveTex != null && mat.HasProperty("_EmissionMap"))
            mat.SetTexture("_EmissionMap", emissiveTex);

        CoreUtils.SetKeyword(mat, "_EMISSION", useEmission);

        if (mat.HasProperty("_AlphaClip"))
            mat.SetFloat("_AlphaClip", wantsAlphaClip ? 1f : 0f);
        CoreUtils.SetKeyword(mat, "_ALPHATEST_ON", wantsAlphaClip);
        if (mat.HasProperty("_Cutoff"))
            mat.SetFloat("_Cutoff", cutoff);

        if (mat.HasProperty("_Cull"))
            mat.SetFloat("_Cull", cull);

        if (mat.HasProperty("_ReceiveShadows"))
            mat.SetFloat("_ReceiveShadows", 1f);

        if (wantsAlphaClip && mat.HasProperty("_AlphaToMask"))
            mat.SetFloat("_AlphaToMask", 1f);
        else if (mat.HasProperty("_AlphaToMask"))
            mat.SetFloat("_AlphaToMask", 0f);

        if (mat.renderQueue != prevQueue && prevQueue > 0)
            mat.renderQueue = prevQueue;

        message = $"converted from [{oldShaderName}] albedo={albedoProp ?? "(none)"}";
        return true;
    }

    static bool ShouldSkipByAssetPath(string path)
    {
        var p = path.Replace('\\', '/');
        if (p.IndexOf("/fonts/", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (p.IndexOf("TextMesh Pro", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (ContainsAnyOrdinalIgnoreCase(p,
                "/InterfaceCore/",
                "InterfaceDarkFantasy",
                "/Interface/",
                "/TMP/",
                "TMP_"))
            return true;
        return false;
    }

    static bool ContainsAnyOrdinalIgnoreCase(string haystack, params string[] needles)
    {
        foreach (var n in needles)
        {
            if (haystack.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    static bool ShouldSkipByShaderNameParticle(string shaderName)
    {
        if (string.IsNullOrEmpty(shaderName))
            return false;
        return shaderName.IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0
               || shaderName.IndexOf("vfx", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool ShouldSkipSkybox(Material mat, string shaderName)
    {
        if (!string.IsNullOrEmpty(shaderName) && shaderName.IndexOf("skybox", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return mat.renderQueue == (int)RenderQueue.Background;
    }

    static string FindFirstAssignedTextureProperty(Material mat, string[] propertyNames, out Texture tex)
    {
        foreach (var name in propertyNames)
        {
            if (!mat.HasProperty(name))
                continue;
            var t = mat.GetTexture(name);
            if (t != null)
            {
                tex = t;
                return name;
            }
        }

        tex = null;
        return null;
    }

    static Color PickFirstColor(Material mat, string[] propertyNames, Color fallback)
    {
        foreach (var name in propertyNames)
        {
            if (mat.HasProperty(name))
                return mat.GetColor(name);
        }

        return fallback;
    }

    static float PickFirstFloat(Material mat, string[] propertyNames, float fallback)
    {
        foreach (var name in propertyNames)
        {
            if (mat.HasProperty(name))
                return mat.GetFloat(name);
        }

        return fallback;
    }

    static bool EmissionColorIsNonBlack(Color c)
    {
        const float eps = 0.004f;
        return c.r > eps || c.g > eps || c.b > eps;
    }

    static bool DetectAlphaClip(Material mat)
    {
        if (mat.IsKeywordEnabled("_ALPHATEST_ON"))
            return true;
        if (mat.HasProperty("_AlphaClip") && mat.GetFloat("_AlphaClip") >= 0.5f)
            return true;
        if (mat.HasProperty("_BUILTIN_AlphaClip") && mat.GetFloat("_BUILTIN_AlphaClip") >= 0.5f)
            return true;
        var renderType = mat.GetTag("RenderType", false, "Opaque");
        if (renderType.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return false;
    }
}
