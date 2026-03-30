using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch-convert any materials under chosen project folders to GameClient/Cel Shading Lit.
/// </summary>
public class CelShadingBulkConvertWindow : EditorWindow
{
    const string FolderAssets = "Assets";
    const string FolderSynty = "Assets/Synty";
    const string FolderSyntyAssets = "Assets/SyntyAssets";

    [SerializeField] string[] _searchFolders = { FolderAssets };

    bool _skipUiPaths;
    bool _skipParticles = true;
    bool _skipSkybox = true;
    bool _skipTransparentQueue;
    bool _skipAlreadyCel = true;

    Vector2 _scroll;
    string _log = "";

    [MenuItem("GameClient/Rendering/Bulk Convert Materials to Cel Shading…")]
    [MenuItem("GameClient/Rendering/Bulk Convert Synty Materials to Cel Shading…")]
    static void Open()
    {
        var w = GetWindow<CelShadingBulkConvertWindow>();
        w.titleContent = new GUIContent("Cel Shading: bulk");
        w.minSize = new Vector2(440, 360);
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Папки с материалами (относительно проекта)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Ищет все .mat внутри указанных папок. По умолчанию — весь «Assets». Можно сузить до подпапок (например только персонажи). Shader Graph: подставляется первая найденная альбедо-текстура по списку имён в конвертере; сложные маски не воспроизводятся.",
            MessageType.Info);

        for (var i = 0; i < _searchFolders.Length; i++)
            _searchFolders[i] = EditorGUILayout.TextField($"Папка {i + 1}", _searchFolders[i]);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Только Assets"))
                _searchFolders = new[] { FolderAssets };
            if (GUILayout.Button("Synty + SyntyAssets"))
                _searchFolders = new[] { FolderSynty, FolderSyntyAssets };
            if (GUILayout.Button("+ строка"))
            {
                var list = new List<string>(_searchFolders) { "" };
                _searchFolders = list.ToArray();
            }
        }

        EditorGUILayout.Space(6);
        _skipUiPaths = EditorGUILayout.ToggleLeft("Пропускать UI / HUD / Fonts / TMP (по пути ассета)", _skipUiPaths);
        _skipParticles = EditorGUILayout.ToggleLeft("Пропускать шейдеры Particle / VFX (по имени)", _skipParticles);
        _skipSkybox = EditorGUILayout.ToggleLeft("Пропускать Skybox и очередь Background", _skipSkybox);
        _skipTransparentQueue = EditorGUILayout.ToggleLeft("Пропускать очередь Transparent", _skipTransparentQueue);
        _skipAlreadyCel = EditorGUILayout.ToggleLeft("Пропускать уже Cel Shading Lit", _skipAlreadyCel);

        EditorGUILayout.Space(10);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Только посчитать", GUILayout.Height(28)))
                Run(dryRun: true);

            if (GUILayout.Button("Конвертировать всё", GUILayout.Height(28)))
            {
                if (EditorUtility.DisplayDialog(
                        "Массовая конвертация в Cel Shading",
                        "Перезаписать шейдер у всех подходящих материалов в указанных папках? Сделайте backup / commit.",
                        "Конвертировать",
                        "Отмена"))
                {
                    Run(dryRun: false);
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Журнал", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
        EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    void Run(bool dryRun)
    {
        var cel = CelShadingMaterialConverter.FindCelShader();
        if (cel == null)
        {
            _log = $"Ошибка: не найден шейдер «{CelShadingMaterialConverter.CelShaderName}». Импортируйте Assets/Shaders/CelShading.";
            return;
        }

        var options = new CelShadingMaterialConverter.ConversionOptions
        {
            SkipIfAlreadyCel = _skipAlreadyCel,
            SkipUiHudFontsTmpPaths = _skipUiPaths,
            SkipParticleShaders = _skipParticles,
            SkipSkyboxShaders = _skipSkybox,
            SkipTransparentRenderQueue = _skipTransparentQueue,
        };

        var folders = new List<string>();
        foreach (var f in _searchFolders)
        {
            if (string.IsNullOrWhiteSpace(f))
                continue;
            var trimmed = f.Trim().Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(trimmed))
                folders.Add(trimmed);
        }

        if (folders.Count == 0)
        {
            _log = "Нет валидных папок. Примеры: Assets, Assets/MyPack/Materials";
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Material", folders.ToArray());
        var sb = new StringBuilder();
        var converted = 0;
        var skipped = 0;
        var failed = 0;

        try
        {
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (EditorUtility.DisplayCancelableProgressBar(
                        dryRun ? "Подсчёт материалов" : "Конвертация в Cel Shading",
                        path,
                        guids.Length > 0 ? (float)i / guids.Length : 1f))
                {
                    sb.AppendLine("--- прервано пользователем ---");
                    break;
                }

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    failed++;
                    sb.AppendLine($"FAIL load: {path}");
                    continue;
                }

                if (dryRun)
                {
                    var filter = CelShadingMaterialConverter.GetFilterSkipReason(mat, path, options);
                    if (filter != null)
                    {
                        skipped++;
                        sb.AppendLine($"[skip] {path}  ({filter})");
                    }
                    else
                    {
                        converted++;
                        var sn = mat.shader != null ? mat.shader.name : "";
                        sb.AppendLine($"[ok] {path}  ← [{sn}]");
                    }
                }
                else
                {
                    if (CelShadingMaterialConverter.TryConvert(mat, options, out var msg))
                    {
                        converted++;
                        EditorUtility.SetDirty(mat);
                        sb.AppendLine($"[converted] {path}  ({msg})");
                    }
                    else
                    {
                        skipped++;
                        sb.AppendLine($"[skip] {path}  ({msg})");
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!dryRun && converted > 0)
            AssetDatabase.SaveAssets();

        _log = $"Готово. {(dryRun ? "Предпросмотр" : "Результат")}: подходит/конвертировано: {converted}, пропущено: {skipped}, ошибок загрузки: {failed}, всего .mat: {guids.Length}\n\n" + sb;
        Repaint();
    }
}
