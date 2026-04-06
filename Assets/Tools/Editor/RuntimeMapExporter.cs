namespace Tools
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.RegularExpressions;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.AI;
    using UnityEngine.Networking;

    /// <summary>
    /// Редактор карт: настройка полей карты (уровень 1 по умолчанию), сбор данных со сцены, отправка на сервер.
    /// Расширяемый инструмент для будущих механик.
    /// </summary>
    public sealed class RuntimeMapExporter : EditorWindow
    {
        private const float LabelWidth = 180f;
        private const float SliderMinRadius = 0.05f;
        private const float SliderMaxRadius = 1f;
        private const float SliderMinPos = -50f;
        private const float SliderMaxPos = 50f;

        private Vector2 _scrollPos;

        // --- Основные (предзаполнены для 1 уровня) ---
        private string _mapId = "default";
        private float _playerRadius = 0.35f;
        private float _playerSpawnX = 6.16f;
        private float _playerSpawnY = 0f;
        private float _playerSpawnZ = 0f;
        private bool _playerSpawnFromScene = true;

        // --- Теги врагов ---
        private List<string> _enemyTags = new List<string> { "goblins", "spiders" };
        private string _newTag = "";
        private string _backendApiBaseUrl = "http://127.0.0.1:8000";
        private string _tagsEndpoint = "/api/content/monsters/tags";
        private string _tagsStatus = "";
        private bool _tagsLoaded;

        // --- Спавны врагов (предзаполнено для 1 уровня) ---
        private List<EnemySpawnEntry> _enemySpawns = new List<EnemySpawnEntry> { new EnemySpawnEntry { x = -1.59f, y = 1.59f, max_per_point = 5 } };
        private float _newSpawnX = 0f;
        private float _newSpawnY = 0f;
        private int _newSpawnMaxPerPoint = 5;
        private bool _enemySpawnsFromScene = false;

        // --- Сцена: слои для сбора ---
        private bool _usePlayerSpawnLayer = true;
        private string _playerSpawnLayerName = "PlayerSpawn";
        private bool _useEnemySpawnLayer = true;
        private string _enemySpawnLayerName = "EnemySpawn";

        // --- Сервер ---
        private string _storeMapEndpoint = "/api/content/maps";
        private string _serverStatus = "";
        private bool _sending;

        // --- NavMesh (для коллизий и pathfinding на сервере) ---
        private NavMeshExport _navmeshExport;

        [MenuItem("Tools/Runtime Server/Редактор карт")]
        public static void Open()
        {
            var w = GetWindow<RuntimeMapExporter>("Редактор карт");
            w.minSize = new Vector2(420f, 520f);
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            DrawSectionHeader("Основные параметры");
            DrawBasicFields();

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Спавн игрока");
            DrawPlayerSpawn();

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Теги врагов");
            DrawEnemyTags();

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Точки спавна врагов");
            DrawEnemySpawns();

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Сбор со сцены");
            DrawSceneCollection();

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Предпросмотр карты");
            DrawMapPreview();

            EditorGUILayout.Space(6f);
            DrawSectionHeader("Сервер");
            DrawServerSection();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawSectionHeader(string title)
        {
            var style = new GUIStyle(EditorStyles.boldLabel) { fontStyle = FontStyle.Bold };
            EditorGUILayout.LabelField(title, style);
        }

        private void DrawBasicFields()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("ID карты", GUILayout.Width(LabelWidth));
            _mapId = EditorGUILayout.TextField(string.IsNullOrWhiteSpace(_mapId) ? GetDefaultMapId() : _mapId);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Радиус игрока", GUILayout.Width(LabelWidth));
            _playerRadius = EditorGUILayout.Slider(_playerRadius, SliderMinRadius, SliderMaxRadius);
            _playerRadius = Mathf.Clamp(EditorGUILayout.FloatField(_playerRadius, GUILayout.Width(60)), SliderMinRadius, SliderMaxRadius);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayerSpawn()
        {
            _playerSpawnFromScene = EditorGUILayout.Toggle("Брать со сцены (слой)", _playerSpawnFromScene);
            if (!_playerSpawnFromScene)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("X", GUILayout.Width(LabelWidth));
                _playerSpawnX = EditorGUILayout.Slider(_playerSpawnX, SliderMinPos, SliderMaxPos);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Y", GUILayout.Width(LabelWidth));
                _playerSpawnY = EditorGUILayout.Slider(_playerSpawnY, SliderMinPos, SliderMaxPos);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(LabelWidth));
                _playerSpawnX = EditorGUILayout.FloatField("X (точное)", _playerSpawnX);
                _playerSpawnY = EditorGUILayout.FloatField("Z гориз. (точное)", _playerSpawnY);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Высота Y (мир)", GUILayout.Width(LabelWidth));
                _playerSpawnZ = EditorGUILayout.FloatField(_playerSpawnZ);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawEnemyTags()
        {
            _backendApiBaseUrl = EditorGUILayout.TextField("URL сервера", _backendApiBaseUrl);
            _tagsEndpoint = EditorGUILayout.TextField("Эндпоинт тегов", _tagsEndpoint);

            if (GUILayout.Button("Загрузить теги с сервера"))
            {
                _ = LoadEnemyTagsFromBackend();
            }

            if (!string.IsNullOrWhiteSpace(_tagsStatus))
            {
                var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                EditorGUILayout.LabelField(_tagsStatus, style);
            }

            EditorGUILayout.LabelField("Текущие теги: " + string.Join(", ", _enemyTags));

            EditorGUILayout.BeginHorizontal();
            _newTag = EditorGUILayout.TextField("Добавить тег", _newTag);
            if (GUILayout.Button("+", GUILayout.Width(24)) && !string.IsNullOrWhiteSpace(_newTag))
            {
                var t = _newTag.Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(t) && !_enemyTags.Contains(t))
                {
                    _enemyTags.Add(t);
                    _newTag = "";
                }
            }
            EditorGUILayout.EndHorizontal();

            for (int i = _enemyTags.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_enemyTags[i]);
                if (GUILayout.Button("−", GUILayout.Width(24)))
                {
                    _enemyTags.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawEnemySpawns()
        {
            _enemySpawnsFromScene = EditorGUILayout.Toggle("Брать со сцены (слой)", _enemySpawnsFromScene);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Добавить точку X", GUILayout.Width(LabelWidth));
            _newSpawnX = EditorGUILayout.FloatField(_newSpawnX);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Добавить точку Y", GUILayout.Width(LabelWidth));
            _newSpawnY = EditorGUILayout.FloatField(_newSpawnY);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Макс. врагов в точке", GUILayout.Width(LabelWidth));
            _newSpawnMaxPerPoint = Mathf.Max(0, EditorGUILayout.IntField(_newSpawnMaxPerPoint));
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Добавить точку спавна врагов"))
            {
                _enemySpawns.Add(new EnemySpawnEntry { x = _newSpawnX, y = _newSpawnY, max_per_point = _newSpawnMaxPerPoint > 0 ? _newSpawnMaxPerPoint : 5 });
            }

            for (int i = _enemySpawns.Count - 1; i >= 0; i--)
            {
                var e = _enemySpawns[i];
                EditorGUILayout.BeginHorizontal();
                _enemySpawns[i] = new EnemySpawnEntry
                {
                    x = EditorGUILayout.FloatField(e.x),
                    y = EditorGUILayout.FloatField(e.y),
                    max_per_point = Mathf.Max(0, EditorGUILayout.IntField(e.max_per_point > 0 ? e.max_per_point : 5, GUILayout.Width(40)))
                };
                if (GUILayout.Button("−", GUILayout.Width(24)))
                {
                    _enemySpawns.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSceneCollection()
        {
            _usePlayerSpawnLayer = EditorGUILayout.Toggle("Слой спавна игрока", _usePlayerSpawnLayer);
            if (_usePlayerSpawnLayer)
            {
                _playerSpawnLayerName = EditorGUILayout.TextField("  Имя слоя", _playerSpawnLayerName);
            }
            _useEnemySpawnLayer = EditorGUILayout.Toggle("Слой спавнов врагов", _useEnemySpawnLayer);
            if (_useEnemySpawnLayer)
            {
                _enemySpawnLayerName = EditorGUILayout.TextField("  Имя слоя", _enemySpawnLayerName);
            }

            if (GUILayout.Button("Собрать спавны со сцены"))
            {
                ApplySceneData();
            }

            EditorGUILayout.Space(6f);
            DrawSectionHeader("NavMesh (сервер)");
            DrawNavMeshSection();
        }

        private void DrawNavMeshSection()
        {
            EditorGUILayout.HelpBox(
                "Запекание: добавьте Navigation > NavMesh Surface, в Use Geometry выберите Physics Colliders (только объекты с коллайдерами; Render Meshes учитывает и объекты без коллайдеров). Нажмите Bake. " +
                "Затем «Экспорт NavMesh» — данные уйдут на сервер при сохранении карты.",
                MessageType.None);
            if (GUILayout.Button("Экспорт NavMesh сцены"))
            {
                ExportSceneNavMesh();
            }
            if (_navmeshExport != null && _navmeshExport.vertices != null)
            {
                EditorGUILayout.LabelField($"NavMesh: {_navmeshExport.vertices.Length} вершин, {_navmeshExport.triangles?.Length / 3 ?? 0} треугольников.");
            }
            else
            {
                EditorGUILayout.LabelField("NavMesh не экспортирован.");
            }
        }

        private void ExportSceneNavMesh()
        {
#if UNITY_EDITOR
            var tri = NavMesh.CalculateTriangulation();
            if (tri.vertices == null || tri.vertices.Length == 0)
            {
                Debug.LogWarning("NavMesh пуст. Добавьте NavMesh Surface на объект и нажмите Bake в Inspector (или Window > AI > Navigation > Bake в старых версиях).");
                return;
            }
            var vertices = new List<NavMeshVertexExport>();
            for (int i = 0; i < tri.vertices.Length; i++)
            {
                var v = tri.vertices[i];
                vertices.Add(new NavMeshVertexExport { x = v.x, y = v.z, z = v.y });
            }
            var triangles = tri.indices != null ? new List<int>(tri.indices) : new List<int>();
            var neighbours = BuildNavMeshNeighbours(vertices.Count, triangles);
            _navmeshExport = new NavMeshExport
            {
                vertices = vertices.ToArray(),
                triangles = triangles.ToArray(),
                neighbours = neighbours.ToArray()
            };
            Debug.Log($"NavMesh экспортирован: {vertices.Count} вершин, {triangles.Count / 3} треугольников.");
#endif
        }

        private static List<int> BuildNavMeshNeighbours(int numVertices, List<int> triangles)
        {
            var triCount = triangles.Count / 3;
            var neighbours = new List<int>(triCount * 3);
            for (int t = 0; t < triCount; t++)
            {
                int i0 = triangles[t * 3], i1 = triangles[t * 3 + 1], i2 = triangles[t * 3 + 2];
                neighbours.Add(FindNeighbour(triangles, t, i1, i2));
                neighbours.Add(FindNeighbour(triangles, t, i2, i0));
                neighbours.Add(FindNeighbour(triangles, t, i0, i1));
            }
            return neighbours;
        }

        private static int FindNeighbour(List<int> triangles, int skipTri, int va, int vb)
        {
            int triCount = triangles.Count / 3;
            for (int t = 0; t < triCount; t++)
            {
                if (t == skipTri) continue;
                int a = triangles[t * 3], b = triangles[t * 3 + 1], c = triangles[t * 3 + 2];
                bool hasA = a == va || b == va || c == va;
                bool hasB = a == vb || b == vb || c == vb;
                if (hasA && hasB) return t;
            }
            return -1;
        }

        private const float PreviewWidth = 320f;
        private const float PreviewHeight = 200f;
        private const float PreviewPadding = 4f;

        private void DrawMapPreview()
        {
            if (_enemySpawns.Count == 0)
            {
                EditorGUILayout.HelpBox("Добавьте точки спавна или нажмите «Собрать спавны со сцены», чтобы увидеть предпросмотр.", MessageType.Info);
                return;
            }

            var rect = GUILayoutUtility.GetRect(PreviewWidth, PreviewHeight);
            var inner = new Rect(rect.x + PreviewPadding, rect.y + PreviewPadding, rect.width - PreviewPadding * 2f, rect.height - PreviewPadding * 2f);

            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            void Expand(float x, float y)
            {
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            Expand(_playerSpawnX, _playerSpawnY);
            foreach (var s in _enemySpawns)
                Expand(s.x, s.y);

            if (minX == float.MaxValue)
            {
                minX = maxX = _playerSpawnX;
                minY = maxY = _playerSpawnY;
            }
            float spanX = maxX - minX;
            float spanY = maxY - minY;
            if (spanX < 1f) spanX = 1f;
            if (spanY < 1f) spanY = 1f;
            float margin = Mathf.Max(spanX, spanY) * 0.15f;
            minX -= margin;
            minY -= margin;
            spanX += margin * 2f;
            spanY += margin * 2f;
            float scale = Mathf.Min(inner.width / spanX, inner.height / spanY);
            float centerX = minX + spanX * 0.5f;
            float centerY = minY + spanY * 0.5f;

            Vector2 WorldToPreview(float wx, float wy)
            {
                float px = inner.x + inner.width * 0.5f + (wx - centerX) * scale;
                float py = inner.y + inner.height * 0.5f - (wy - centerY) * scale;
                return new Vector2(px, py);
            }

            EditorGUI.DrawRect(rect, new Color(0.22f, 0.22f, 0.22f));
            EditorGUI.DrawRect(inner, new Color(0.16f, 0.2f, 0.18f));

            Handles.BeginGUI();
            float r = 5f;
            Handles.color = new Color(0.3f, 0.6f, 1f);
            var pCenter = WorldToPreview(_playerSpawnX, _playerSpawnY);
            Handles.DrawSolidDisc(new Vector3(pCenter.x, pCenter.y, 0f), Vector3.forward, r);
            Handles.color = new Color(1f, 0.45f, 0.35f);
            foreach (var s in _enemySpawns)
            {
                var ep = WorldToPreview(s.x, s.y);
                Handles.DrawSolidDisc(new Vector3(ep.x, ep.y, 0f), Vector3.forward, r * 0.8f);
            }
            Handles.EndGUI();

            GUILayout.Label("Синий — игрок, красные — спавны врагов.");
        }

        private void DrawServerSection()
        {
            _storeMapEndpoint = EditorGUILayout.TextField("Эндпоинт сохранения карты", _storeMapEndpoint);

            EditorGUI.BeginDisabledGroup(_sending);
            if (GUILayout.Button(_sending ? "Отправка…" : "Сохранить карту на сервер", GUILayout.Height(28)))
            {
                SaveMapToServer();
            }
            EditorGUI.EndDisabledGroup();

            if (!string.IsNullOrWhiteSpace(_serverStatus))
            {
                var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                EditorGUILayout.LabelField(_serverStatus, style);
            }
        }

        private void ApplySceneData()
        {
            if (_playerSpawnFromScene)
            {
                var ps = CollectPlayerSpawn();
                if (ps != null)
                {
                    _playerSpawnX = ps.x;
                    _playerSpawnY = ps.y;
                    _playerSpawnZ = ps.z;
                }
            }
            if (_enemySpawnsFromScene)
            {
                var spawns = CollectEnemySpawns();
                if (spawns.Count > 0)
                {
                    _enemySpawns = spawns.ConvertAll(v => new EnemySpawnEntry { x = v.x, y = v.y, max_per_point = 5 });
                }
            }
            Debug.Log($"Спавн игрока (карта): {_playerSpawnX:F2}, {_playerSpawnY:F2}, z={_playerSpawnZ:F2}. Точек врагов: {_enemySpawns.Count}");
        }

        private void SaveMapToServer()
        {
            var mapId = string.IsNullOrWhiteSpace(_mapId) ? GetDefaultMapId() : _mapId;
            MapPoint playerSpawn = null;
            if (_playerSpawnFromScene)
            {
                playerSpawn = CollectPlayerSpawn();
            }
            if (playerSpawn == null)
            {
                playerSpawn = new MapPoint { x = _playerSpawnX, y = _playerSpawnY, z = _playerSpawnZ };
            }

            if (!_tagsLoaded && _enemyTags.Count == 0)
            {
                if (!LoadEnemyTagsFromBackend())
                {
                    _serverStatus = "Не загружены теги врагов. Загрузите теги или добавьте вручную.";
                    return;
                }
            }

            var enemyTags = NormalizeEnemyTags(_enemyTags);
            if (enemyTags.Count == 0)
            {
                _serverStatus = "Добавьте хотя бы один тег врагов.";
                return;
            }

            var enemySpawnsForPayload = _enemySpawnsFromScene ? CollectEnemySpawns().ConvertAll(v => new EnemySpawnEntry { x = v.x, y = v.y, max_per_point = 5 }) : _enemySpawns;
            if (_enemySpawnsFromScene && enemySpawnsForPayload.Count == 0)
            {
                enemySpawnsForPayload = _enemySpawns;
            }

            var payload = new MapPayload
            {
                id = mapId,
                playerRadius = _playerRadius,
                playerSpawn = playerSpawn,
                enemyTags = enemyTags,
                enemySpawns = enemySpawnsForPayload,
                navmesh = _navmeshExport
            };

            var json = JsonUtility.ToJson(payload);
            // JsonUtility omits float fields equal to 0 — Laravel then gets playerSpawn {} / missing keys → null in DB.
            json = EnsurePlayerSpawnInJson(json, playerSpawn);
            if (_navmeshExport != null && _navmeshExport.vertices != null && _navmeshExport.vertices.Length > 0)
            {
                var navmeshStr = JsonUtility.ToJson(_navmeshExport);
                if (json.IndexOf("\"navmesh\":null", StringComparison.Ordinal) is int nmPos && nmPos >= 0)
                    json = json.Substring(0, nmPos + "\"navmesh\":".Length) + navmeshStr + json.Substring(nmPos + "\"navmesh\":null".Length);
            }
            var url = BuildUrl(_backendApiBaseUrl, _storeMapEndpoint);
            if (string.IsNullOrWhiteSpace(url))
            {
                _serverStatus = "Укажите URL сервера.";
                return;
            }

            _sending = true;
            _serverStatus = "";
            SendMapToServer(url, json);
        }

        private void SendMapToServer(string url, string json)
        {
            using var request = new UnityWebRequest(url, "POST");
            var body = new System.Text.UTF8Encoding().GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                // Block editor until done
            }

            _sending = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                _serverStatus = "Карта сохранена на сервере.";
                Debug.Log($"Map saved: {url}");
            }
            else
            {
                _serverStatus = $"Ошибка: {request.responseCode} {request.error}. {request.downloadHandler?.text ?? ""}";
            }
        }

        private static string GetDefaultMapId()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return string.IsNullOrWhiteSpace(scene.name) ? "default" : scene.name;
        }

        /// <summary>
        /// JsonUtility.ToJson skips default (0) floats, so nested playerSpawn can become {} — replace with explicit x,y,z.
        /// </summary>
        private static string EnsurePlayerSpawnInJson(string json, MapPoint spawn)
        {
            if (string.IsNullOrEmpty(json) || spawn == null) return json;
            var inv = CultureInfo.InvariantCulture;
            var block = string.Format(inv, "\"playerSpawn\":{{\"x\":{0},\"y\":{1},\"z\":{2}}}", spawn.x, spawn.y, spawn.z);
            const string pattern = "\"playerSpawn\"\\s*:\\s*(\\{[^}]*\\}|null)";
            if (Regex.IsMatch(json, pattern))
                return Regex.Replace(json, pattern, block);

            // Rare: field missing entirely — insert after playerRadius.
            var m = Regex.Match(json, "\"playerRadius\"\\s*:\\s*([0-9]+\\.?[0-9]*(?:[eE][+-]?[0-9]+)?)");
            if (m.Success)
            {
                var insertAt = m.Index + m.Length;
                return json.Insert(insertAt, "," + block);
            }

            return json;
        }

        private MapPoint CollectPlayerSpawn()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            // Transform.position — всегда мировые координаты, глубина иерархии не важна.
            // Раньше слой шёл первым: на одном слое мог попасться «чужой» объект раньше маркера PlayerSpawn.
            foreach (var r in roots)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (IsPreferredPlayerSpawnName(t.gameObject))
                        return LogAndMapPlayerSpawn(t);
                }
            }

            foreach (var r in roots)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (SafeCompareTag(t.gameObject, "PlayerSpawn"))
                        return LogAndMapPlayerSpawn(t);
                }
            }

            if (_usePlayerSpawnLayer)
            {
                var layer = LayerMask.NameToLayer(_playerSpawnLayerName);
                if (layer < 0)
                {
                    Debug.LogWarning(
                        $"[RuntimeMapExporter] Слой «{_playerSpawnLayerName}» не найден (Edit → Project Settings → Tags and Layers). " +
                        "Создайте слой и назначьте его маркеру, либо отключите «Слой спавна игрока» и используйте объект с именем PlayerSpawn.");
                }
                else
                {
                    foreach (var r in roots)
                    {
                        foreach (var t in r.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.gameObject.layer != layer) continue;
                            return LogAndMapPlayerSpawn(t);
                        }
                    }
                }
            }

            foreach (var r in roots)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (IsLoosePlayerSpawnName(t.gameObject))
                        return LogAndMapPlayerSpawn(t);
                }
            }

            Debug.LogWarning("[RuntimeMapExporter] Маркер спавна игрока не найден: имя/тег PlayerSpawn, затем слой «" + _playerSpawnLayerName + "».");
            return null;
        }

        private static MapPoint LogAndMapPlayerSpawn(Transform t)
        {
            var world = t.position;
            var map = WorldXZToMapPoint(world);
            Debug.Log(
                "[RuntimeMapExporter] Спавн игрока: объект «" + t.name + "» (" + GetTransformHierarchyPath(t) + "), " +
                "мир Unity (x,y,z)=(" + world.x.ToString("F2", CultureInfo.InvariantCulture) + "," +
                world.y.ToString("F2", CultureInfo.InvariantCulture) + "," +
                world.z.ToString("F2", CultureInfo.InvariantCulture) + ") → на сервер x=" +
                map.x.ToString("F2", CultureInfo.InvariantCulture) + ", y(гориз.)=" +
                map.y.ToString("F2", CultureInfo.InvariantCulture) + ", z(высота)=" +
                map.z.ToString("F2", CultureInfo.InvariantCulture) + ")");
            return map;
        }

        private static string GetTransformHierarchyPath(Transform t)
        {
            var s = t.name ?? "";
            while (t.parent != null)
            {
                t = t.parent;
                s = (t.name ?? "") + "/" + s;
            }

            return s;
        }

        /// <summary>Имя «PlayerSpawn» или дубликат Unity «PlayerSpawn (1)».</summary>
        private static bool IsPreferredPlayerSpawnName(GameObject go)
        {
            var n = go.name ?? "";
            if (string.Equals(n, "PlayerSpawn", StringComparison.OrdinalIgnoreCase)) return true;
            return n.StartsWith("PlayerSpawn", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Любое вхождение подстроки (после точного имени и слоя).</summary>
        private static bool IsLoosePlayerSpawnName(GameObject go)
        {
            var n = go.name ?? "";
            return n.IndexOf("PlayerSpawn", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static MapPoint WorldXZToMapPoint(Vector3 world)
        {
            var pos = new Vector2(world.x, world.z);
            return new MapPoint { x = pos.x, y = pos.y, z = world.y };
        }

        private static bool SafeCompareTag(GameObject go, string tag)
        {
            try
            {
                return go.CompareTag(tag);
            }
            catch (UnityException)
            {
                return false;
            }
        }

        private List<Vector2> CollectEnemySpawns()
        {
            var result = new List<Vector2>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            if (_useEnemySpawnLayer)
            {
                var layer = LayerMask.NameToLayer(_enemySpawnLayerName);
                if (layer < 0)
                {
                    Debug.LogWarning(
                        $"[RuntimeMapExporter] Слой «{_enemySpawnLayerName}» не найден. Точки врагов по слою не собраны; добавьте слой или отключите фильтр.");
                }
                else
                {
                    foreach (var r in roots)
                    {
                        foreach (var t in r.GetComponentsInChildren<Transform>(true))
                        {
                            if (t.gameObject.layer != layer) continue;
                            result.Add(new Vector2(t.position.x, t.position.z));
                        }
                    }
                }
            }

            if (result.Count == 0)
            {
                foreach (var r in roots)
                {
                    foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    {
                        if (IsEnemySpawnByName(t.gameObject))
                            result.Add(new Vector2(t.position.x, t.position.z));
                    }
                }
            }

            if (result.Count == 0)
            {
                foreach (var r in roots)
                {
                    foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    {
                        if (SafeCompareTag(t.gameObject, "EnemySpawn"))
                            result.Add(new Vector2(t.position.x, t.position.z));
                    }
                }
            }

            return result;
        }

        private static bool IsEnemySpawnByName(GameObject go)
        {
            var n = go.name ?? "";
            return string.Equals(n, "EnemySpawn", StringComparison.OrdinalIgnoreCase)
                   || n.StartsWith("EnemySpawn", StringComparison.OrdinalIgnoreCase);
        }

        private bool LoadEnemyTagsFromBackend()
        {
            var url = BuildUrl(_backendApiBaseUrl, _tagsEndpoint);
            if (string.IsNullOrWhiteSpace(url))
            {
                _tagsStatus = "URL пустой.";
                return false;
            }
            using var request = UnityWebRequest.Get(url);
            request.timeout = 10;
            var operation = request.SendWebRequest();
            while (!operation.isDone) { }
            if (request.result != UnityWebRequest.Result.Success)
            {
                _tagsStatus = $"{url} — {request.responseCode} {request.error}";
                return false;
            }
            EnemyTagsResponse payload;
            try
            {
                payload = JsonUtility.FromJson<EnemyTagsResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                _tagsStatus = ex.Message;
                return false;
            }
            if (payload == null || !payload.ok || payload.tags == null)
            {
                _tagsStatus = "Некорректный ответ сервера.";
                return false;
            }
            _enemyTags = NormalizeEnemyTags(payload.tags);
            _tagsLoaded = true;
            _tagsStatus = $"Загружено тегов: {_enemyTags.Count}";
            return true;
        }

        private static string BuildUrl(string baseUrl, string endpoint)
        {
            var b = (baseUrl ?? "").Trim();
            var e = (endpoint ?? "").Trim();
            if (string.IsNullOrWhiteSpace(b)) return "";
            if (string.IsNullOrWhiteSpace(e)) return b.TrimEnd('/');
            return $"{b.TrimEnd('/')}/{e.TrimStart('/')}";
        }

        private static List<string> NormalizeEnemyTags(List<string> tags)
        {
            return tags
                .Select(t => (t ?? "").Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
        }

        [Serializable]
        private sealed class MapPayload
        {
            public string id;
            public float playerRadius;
            public MapPoint playerSpawn;
            public List<string> enemyTags;
            public List<EnemySpawnEntry> enemySpawns;
            public NavMeshExport navmesh;
        }

        [Serializable]
        private sealed class NavMeshExport
        {
            public NavMeshVertexExport[] vertices;
            public int[] triangles;
            public int[] neighbours;
        }

        [Serializable]
        private sealed class NavMeshVertexExport
        {
            public float x;
            public float y;
            public float z;
        }

        [Serializable]
        private sealed class EnemySpawnEntry
        {
            public float x;
            public float y;
            public int max_per_point = 5;
        }

        [Serializable]
        private sealed class EnemyTagsResponse
        {
            public bool ok;
            public List<string> tags;
        }

        [Serializable]
        private sealed class MapPoint
        {
            public float x;
            public float y;
            public float z;
        }
    }
}
