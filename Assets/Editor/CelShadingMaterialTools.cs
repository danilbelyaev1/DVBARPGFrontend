using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class CelShadingMaterialTools
{
    public const string CelShaderAssetPath = "Assets/Shaders/CelShading/CelShadingLit.shader";

    [MenuItem("GameClient/Rendering/Assign Cel Shading To Selected Materials")]
    static void AssignCelShadingToSelection()
    {
        if (CelShadingMaterialConverter.FindCelShader() == null)
        {
            Debug.LogError(
                $"Shader not found: {CelShadingMaterialConverter.CelShaderName}. Reimport {CelShaderAssetPath}.");
            return;
        }

        var opts = CelShadingMaterialConverter.ConversionOptions.ForSelectionOnly;
        var count = 0;
        foreach (var obj in Selection.objects)
        {
            if (obj is not Material mat)
                continue;

            if (CelShadingMaterialConverter.TryConvert(mat, opts, out var msg))
            {
                count++;
                EditorUtility.SetDirty(mat);
            }
            else
                Debug.Log($"Skipped material '{mat.name}': {msg}", mat);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Assigned {CelShadingMaterialConverter.CelShaderName} to {count} material(s).");
    }

    [MenuItem("GameClient/Rendering/Assign Cel Shading To Selected Materials", true)]
    static bool ValidateAssignCelShadingToSelection()
    {
        foreach (var obj in Selection.objects)
        {
            if (obj is Material)
                return true;
        }

        return false;
    }

    [MenuItem("GameClient/Rendering/Reimport Cel Shading Shaders")]
    static void ReimportCelShaders()
    {
        AssetDatabase.ImportAsset(CelShaderAssetPath, ImportAssetOptions.ForceUpdate);
        var guids = AssetDatabase.FindAssets("", new[] { "Assets/Shaders/CelShading" });
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (p.EndsWith(".hlsl") || p.EndsWith(".shader"))
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        }

        Debug.Log("Reimport Cel Shading: done.");
    }

    [MenuItem("GameClient/Rendering/Log Cel Shader Compile Messages")]
    static void LogCelShaderCompileMessages()
    {
        var byPath = AssetDatabase.LoadAssetAtPath<Shader>(CelShaderAssetPath);
        var byName = Shader.Find(CelShadingMaterialConverter.CelShaderName);

        var sb = new StringBuilder();
        sb.AppendLine("=== Cel shading shader diagnostics ===");
        sb.AppendLine($"CelShaderName (Shader.Find): «{CelShadingMaterialConverter.CelShaderName}»");
        sb.AppendLine($"LoadAssetAtPath: {(byPath != null ? "OK: " + byPath.name : "NULL — проверьте путь " + CelShaderAssetPath)}");
        sb.AppendLine($"Shader.Find: {(byName != null ? "OK: " + byName.name : "NULL — имя шейдера в .shader не совпадает с CelShaderName в коде")}");
        if (byPath != null && byName != null && byPath != byName)
            sb.AppendLine("ВНИМАНИЕ: объект по пути и Shader.Find — разные ассеты.");

        var shader = byPath != null ? byPath : byName;
        if (shader == null)
        {
            Debug.LogError(sb + "\nОткройте Window → General → Console и проверьте ошибки импорта шейдера.");
            return;
        }

        var hasError = ShaderUtil.ShaderHasError(shader);
        sb.AppendLine($"ShaderUtil.ShaderHasError: {hasError}");

        var messages = ShaderUtil.GetShaderMessages(shader);
        if (messages != null && messages.Length > 0)
        {
            sb.AppendLine($"GetShaderMessages ({messages.Length}):");
            foreach (var m in messages)
                sb.AppendLine("  " + m);
        }
        else
        {
            sb.AppendLine("GetShaderMessages: пусто (часто так, пока GPU не запросит конкретный вариант).");
            sb.AppendLine("Сделайте: Reimport Cel Shading Shaders → откройте сцену с мешем → кликните материал в Inspector.");
            sb.AppendLine("Ошибки компиляции смотрите в Console при импорте или при первом рендере.");
        }

        AppendActiveUrpRendererDiagnostics(sb);

        if (hasError)
            Debug.LogError(sb.ToString());
        else
            Debug.Log(sb.ToString());
    }

    /// <summary>
    /// Forward+/Deferred+ включают кластерный обход огней; без #pragma _CLUSTER_LIGHT_LOOP и без InputData в LIGHT_LOOP шейдер даёт битый вариант (розовый).
    /// </summary>
    static void AppendActiveUrpRendererDiagnostics(StringBuilder sb)
    {
        sb.AppendLine("--- URP (активное качество) ---");
        var rpa = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : GraphicsSettings.defaultRenderPipeline as RenderPipelineAsset;

        if (rpa is not UniversalRenderPipelineAsset urp)
        {
            sb.AppendLine(
                rpa == null
                    ? "Render Pipeline asset не назначен (Graphics/Quality)."
                    : $"Активный pipeline: {rpa.GetType().Name} (ожидался UniversalRenderPipelineAsset).");
            return;
        }

        var path = AssetDatabase.GetAssetPath(urp);
        sb.AppendLine(string.IsNullOrEmpty(path) ? $"URP asset: {urp.name} (встроенный/без пути)" : $"URP asset: {path}");

        var list = urp.rendererDataList;
        if (list == null || list.Length == 0)
        {
            sb.AppendLine("rendererDataList пуст — проверьте URP Renderer.");
            return;
        }

        for (var i = 0; i < list.Length; i++)
        {
            var rd = list[i];
            if (rd is UniversalRendererData urd)
            {
                sb.AppendLine(
                    $"  [{i}] {urd.name}: RenderingMode={urd.renderingMode}, clusterLoop={urd.usesClusterLightLoop}, deferredLighting={urd.usesDeferredLighting}");
            }
            else if (rd != null)
            {
                sb.AppendLine($"  [{i}] {rd.name}: {rd.GetType().Name}");
            }
        }
    }
}
