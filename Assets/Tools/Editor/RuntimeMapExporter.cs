namespace Tools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
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
        private bool _playerSpawnFromScene = true;

        // --- Теги врагов ---
        private List<string> _enemyTags = new List<string> { "goblins", "spiders" };
        private string _newTag = "";
        private string _backendApiBaseUrl = "http://127.0.0.1:8000";
        private string _tagsEndpoint = "/api/content/monsters/tags";
        private string _tagsStatus = "";
        private bool _tagsLoaded;

        // --- Спавны врагов (предзаполнено для 1 уровня) ---
        private List<Vector2> _enemySpawns = new List<Vector2> { new Vector2(-1.59f, 1.59f) };
        private float _newSpawnX = 0f;
        private float _newSpawnY = 0f;
        private bool _enemySpawnsFromScene = false;

        // --- Сцена: слои для сбора ---
        private bool _useObstacleLayer = true;
        private string _obstacleLayerName = "Obstacle";
        private bool _skipTriggerColliders = true;
        private bool _excludeLongBoundaryObstacles = true;
        private float _boundaryLengthThreshold = 80f;
        private float _boundaryThicknessThreshold = 2f;
        private bool _usePlayerSpawnLayer = true;
        private string _playerSpawnLayerName = "PlayerSpawn";
        private bool _useEnemySpawnLayer = true;
        private string _enemySpawnLayerName = "EnemySpawn";

        // --- Сервер ---
        private string _storeMapEndpoint = "/api/content/maps";
        private string _serverStatus = "";
        private bool _sending;

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
                _playerSpawnY = EditorGUILayout.FloatField("Y (точное)", _playerSpawnY);
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
            if (GUILayout.Button("Добавить точку спавна врагов"))
            {
                _enemySpawns.Add(new Vector2(_newSpawnX, _newSpawnY));
            }

            for (int i = _enemySpawns.Count - 1; i >= 0; i--)
            {
                EditorGUILayout.BeginHorizontal();
                var v = _enemySpawns[i];
                _enemySpawns[i] = new Vector2(
                    EditorGUILayout.FloatField(v.x),
                    EditorGUILayout.FloatField(v.y)
                );
                if (GUILayout.Button("−", GUILayout.Width(24)))
                {
                    _enemySpawns.RemoveAt(i);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSceneCollection()
        {
            _useObstacleLayer = EditorGUILayout.Toggle("Слой препятствий", _useObstacleLayer);
            if (_useObstacleLayer)
            {
                _obstacleLayerName = EditorGUILayout.TextField("  Имя слоя", _obstacleLayerName);
            }
            _skipTriggerColliders = EditorGUILayout.Toggle("Игнорировать триггеры", _skipTriggerColliders);
            _excludeLongBoundaryObstacles = EditorGUILayout.Toggle("Исключать длинные границы", _excludeLongBoundaryObstacles);
            if (_excludeLongBoundaryObstacles)
            {
                _boundaryLengthThreshold = Mathf.Max(1f, EditorGUILayout.FloatField("  Длина границы >=", _boundaryLengthThreshold));
                _boundaryThicknessThreshold = Mathf.Max(0.01f, EditorGUILayout.FloatField("  Толщина <=", _boundaryThicknessThreshold));
            }
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

            if (GUILayout.Button("Собрать препятствия и спавны со сцены"))
            {
                ApplySceneData();
            }
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
            var obstacles = CollectObstacles();
            if (_playerSpawnFromScene)
            {
                var ps = CollectPlayerSpawn();
                if (ps != null)
                {
                    _playerSpawnX = ps.x;
                    _playerSpawnY = ps.y;
                }
            }
            if (_enemySpawnsFromScene)
            {
                var spawns = CollectEnemySpawns();
                if (spawns.Count > 0)
                {
                    _enemySpawns = spawns;
                }
            }
            Debug.Log($"Собрано: препятствий {obstacles.Count}. Спавн игрока: {_playerSpawnX:F2}, {_playerSpawnY:F2}. Точек врагов: {_enemySpawns.Count}");
        }

        private void SaveMapToServer()
        {
            var mapId = string.IsNullOrWhiteSpace(_mapId) ? GetDefaultMapId() : _mapId;
            var obstacles = CollectObstacles();
            MapPoint playerSpawn = null;
            if (_playerSpawnFromScene)
            {
                playerSpawn = CollectPlayerSpawn();
            }
            if (playerSpawn == null)
            {
                playerSpawn = new MapPoint { x = _playerSpawnX, y = _playerSpawnY };
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

            var enemySpawns = _enemySpawnsFromScene ? CollectEnemySpawns() : _enemySpawns;
            if (_enemySpawnsFromScene && enemySpawns.Count == 0)
            {
                enemySpawns = _enemySpawns;
            }

            var payload = new MapPayload
            {
                id = mapId,
                playerRadius = _playerRadius,
                playerSpawn = playerSpawn,
                enemyTags = enemyTags,
                enemySpawns = enemySpawns.Select(v => new MapPoint { x = v.x, y = v.y }).ToList(),
                obstacles = obstacles
            };

            var json = JsonUtility.ToJson(payload);
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

        private List<MapObstacle> CollectObstacles()
        {
            var result = new List<MapObstacle>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var allTransforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

            foreach (var t in allTransforms)
            {
                if (!IsLayerMatch(t.gameObject, _obstacleLayerName, _useObstacleLayer))
                    continue;

                var box2d = t.GetComponent<BoxCollider2D>();
                if (box2d != null)
                {
                    if (_skipTriggerColliders && box2d.isTrigger) continue;
                    var center = box2d.transform.TransformPoint(box2d.offset);
                    var size = Vector2.Scale(box2d.size, box2d.transform.lossyScale);
                    if (ShouldSkipAsBoundaryObstacle(Mathf.Abs(size.x), Mathf.Abs(size.y))) continue;
                    result.Add(new MapObstacle
                    {
                        x = center.x, y = center.y,
                        w = Mathf.Abs(size.x), h = Mathf.Abs(size.y),
                        rot = box2d.transform.eulerAngles.z
                    });
                    continue;
                }

                var box3d = t.GetComponent<BoxCollider>();
                if (box3d != null)
                {
                    if (_skipTriggerColliders && box3d.isTrigger) continue;
                    var center = box3d.transform.TransformPoint(box3d.center);
                    var size = Vector3.Scale(box3d.size, box3d.transform.lossyScale);
                    if (ShouldSkipAsBoundaryObstacle(Mathf.Abs(size.x), Mathf.Abs(size.z))) continue;
                    result.Add(new MapObstacle
                    {
                        x = center.x, y = center.z,
                        w = Mathf.Abs(size.x), h = Mathf.Abs(size.z),
                        rot = box3d.transform.eulerAngles.y
                    });
                }
            }
            return result;
        }

        private MapPoint CollectPlayerSpawn()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsLayerMatch(t.gameObject, _playerSpawnLayerName, _usePlayerSpawnLayer))
                        continue;
                    var pos = new Vector2(t.position.x, t.position.z);
                    return new MapPoint { x = pos.x, y = pos.y };
                }
            }
            return null;
        }

        private List<Vector2> CollectEnemySpawns()
        {
            var result = new List<Vector2>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsLayerMatch(t.gameObject, _enemySpawnLayerName, _useEnemySpawnLayer))
                        continue;
                    result.Add(new Vector2(t.position.x, t.position.z));
                }
            }
            return result;
        }

        private static bool IsLayerMatch(GameObject go, string layerName, bool useFilter)
        {
            if (!useFilter) return true;
            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0) return true;
            return go.layer == layer;
        }

        private bool ShouldSkipAsBoundaryObstacle(float width, float height)
        {
            if (!_excludeLongBoundaryObstacles) return false;
            var longH = width >= _boundaryLengthThreshold && height <= _boundaryThicknessThreshold;
            var longV = height >= _boundaryLengthThreshold && width <= _boundaryThicknessThreshold;
            return longH || longV;
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
            public List<MapPoint> enemySpawns;
            public List<MapObstacle> obstacles;
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
        }

        [Serializable]
        private sealed class MapObstacle
        {
            public float x;
            public float y;
            public float w;
            public float h;
            public float rot;
        }
    }
}
