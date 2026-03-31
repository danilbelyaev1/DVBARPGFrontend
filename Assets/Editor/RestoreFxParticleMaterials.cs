using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class RestoreFxParticleMaterials
{
    const string CelShaderName = "GameClient/CelShadingLit";
    const string TargetParticleShader = "Universal Render Pipeline/Particles/Unlit";

    [MenuItem("Tools/Cel Shading/Restore FX + Particle Materials")]
    public static void Restore()
    {
        var particleShader = Shader.Find(TargetParticleShader);
        if (particleShader == null)
        {
            Debug.LogError($"Shader not found: {TargetParticleShader}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material");
        int scanned = 0, converted = 0, skipped = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in guids)
            {
                scanned++;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    skipped++;
                    continue;
                }

                if (!ShouldRestore(path, mat))
                {
                    skipped++;
                    continue;
                }

                // Preserve key visual properties before switching shader.
                Texture baseMap = null;
                Vector2 scale = Vector2.one;
                Vector2 offset = Vector2.zero;
                if (mat.HasProperty("_BaseMap"))
                {
                    baseMap = mat.GetTexture("_BaseMap");
                    scale = mat.GetTextureScale("_BaseMap");
                    offset = mat.GetTextureOffset("_BaseMap");
                }
                else if (mat.HasProperty("_MainTex"))
                {
                    baseMap = mat.GetTexture("_MainTex");
                    scale = mat.GetTextureScale("_MainTex");
                    offset = mat.GetTextureOffset("_MainTex");
                }

                var tint = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor") : Color.white;
                var baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                var combinedColor = BuildFxColor(baseColor, color, tint);

                ResolveFxBlend(path, out var srcBlend, out var dstBlend, out var alphaClipLikely);
                var zWrite = mat.HasProperty("_ZWrite") ? mat.GetFloat("_ZWrite") : 0f;
                var cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

                mat.shader = particleShader;

                if (baseMap != null)
                {
                    if (mat.HasProperty("_BaseMap"))
                    {
                        mat.SetTexture("_BaseMap", baseMap);
                        mat.SetTextureScale("_BaseMap", scale);
                        mat.SetTextureOffset("_BaseMap", offset);
                    }
                    if (mat.HasProperty("_MainTex"))
                    {
                        mat.SetTexture("_MainTex", baseMap);
                        mat.SetTextureScale("_MainTex", scale);
                        mat.SetTextureOffset("_MainTex", offset);
                    }
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", combinedColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", combinedColor);

                // Keep transparent behavior for FX.
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", srcBlend);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", dstBlend);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", zWrite < 0.5f ? 0f : 0f);
                mat.SetOverrideTag("RenderType", "Transparent");

                bool wantsClip = (cutoff > 0.01f && IsCutoutLike(path)) || alphaClipLikely;
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", wantsClip ? 1f : 0f);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", cutoff);
                CoreUtils.SetKeyword(mat, "_ALPHATEST_ON", wantsClip);
                CoreUtils.SetKeyword(mat, "_SURFACE_TYPE_TRANSPARENT", true);

                // URP particle materials are usually in transparent queue.
                mat.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(mat);
                converted++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[Restore FX/Particles] scanned={scanned}, converted={converted}, skipped={skipped}");
    }

    [MenuItem("Tools/Cel Shading/Repair Existing FX Particle Materials")]
    public static void RepairExisting()
    {
        var particleShader = Shader.Find(TargetParticleShader);
        if (particleShader == null)
        {
            Debug.LogError($"Shader not found: {TargetParticleShader}");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material");
        int scanned = 0, fixedCount = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var guid in guids)
            {
                scanned++;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || !IsFxPath(path))
                    continue;

                if (mat.shader == null || mat.shader.name != TargetParticleShader)
                    continue;

                var tint = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor") : Color.white;
                var baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
                var color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                var fixedColor = BuildFxColor(baseColor, color, tint);

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", fixedColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", fixedColor);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                ResolveFxBlend(path, out var srcBlend, out var dstBlend, out var alphaClipLikely);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", srcBlend);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", dstBlend);
                mat.SetOverrideTag("RenderType", "Transparent");
                bool wantsClip = IsCutoutLike(path) || alphaClipLikely;
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", wantsClip ? 1f : 0f);
                CoreUtils.SetKeyword(mat, "_ALPHATEST_ON", wantsClip);
                CoreUtils.SetKeyword(mat, "_SURFACE_TYPE_TRANSPARENT", true);
                mat.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(mat);
                fixedCount++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[Repair FX/Particles] scanned={scanned}, fixed={fixedCount}");
    }

    static bool ShouldRestore(string path, Material mat)
    {
        if (!IsFxPath(path)) return false;

        // Restore only if currently Cel shader (the broken converted state).
        return mat.shader != null && string.Equals(mat.shader.name, CelShaderName, StringComparison.Ordinal);
    }

    static bool IsCutoutLike(string path)
    {
        string p = path.ToLowerInvariant();
        return p.Contains("cutout") || p.Contains("leaf") || p.Contains("flies") || p.Contains("fumes");
    }

    static bool IsFxPath(string path)
    {
        string p = path.Replace('\\', '/').ToLowerInvariant();
        return p.Contains("/polygonparticlefx/")
               || p.Contains("/materials/fx/")
               || p.Contains("particlefx")
               || p.Contains("/vfx/")
               || p.Contains("mat_darkfantasy_particlefx");
    }

    static Color BuildFxColor(Color baseColor, Color color, Color tint)
    {
        // Prefer visible color channels; many converted mats have _BaseColor = (0,0,0,0).
        Color rgbSource = Magnitude(baseColor) > 0.01f ? baseColor : (Magnitude(color) > 0.01f ? color : Color.white);
        Color outColor = new Color(
            Mathf.Clamp01(rgbSource.r * Mathf.Max(0.01f, tint.r)),
            Mathf.Clamp01(rgbSource.g * Mathf.Max(0.01f, tint.g)),
            Mathf.Clamp01(rgbSource.b * Mathf.Max(0.01f, tint.b)),
            1f);

        // Keep alpha visible; never let it collapse to zero by bad source values.
        float a = Mathf.Max(baseColor.a, color.a, tint.a);
        outColor.a = a < 0.05f ? 1f : Mathf.Clamp01(a);
        return outColor;
    }

    static float Magnitude(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

    static void ResolveFxBlend(string path, out float srcBlend, out float dstBlend, out bool alphaClipLikely)
    {
        string p = path.ToLowerInvariant();
        alphaClipLikely = IsCutoutLike(path);

        bool additive =
            p.Contains("additive")
            || p.Contains("glow")
            || p.Contains("spark")
            || p.Contains("lightning")
            || p.Contains("beam")
            || p.Contains("fire")
            || p.Contains("portal")
            || p.Contains("sun");

        if (additive)
        {
            srcBlend = (float)BlendMode.SrcAlpha;
            dstBlend = (float)BlendMode.One;
            alphaClipLikely = false;
        }
        else
        {
            srcBlend = (float)BlendMode.SrcAlpha;
            dstBlend = (float)BlendMode.OneMinusSrcAlpha;
        }
    }
}
