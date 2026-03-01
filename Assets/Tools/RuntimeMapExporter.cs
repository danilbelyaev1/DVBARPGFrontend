namespace Tools
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Networking;

    public sealed class RuntimeMapExporter : EditorWindow
    {
        private string _mapId = "";
        private float _playerRadius = 0.35f;
        private string _outputFile = "default.json";
        private List<string> _enemyTags = new();
        private string _backendApiBaseUrl = "http://127.0.0.1:8000";
        private string _enemyTagsEndpoint = "/content/monsters/tags";
        private string _enemyTagsStatus = "";
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

        [MenuItem("Tools/Runtime Server/Export Map JSON")]
        public static void Open()
        {
            GetWindow<RuntimeMapExporter>("Map Export");
        }

        private void OnGUI()
        {
            _mapId = EditorGUILayout.TextField("Map Id", string.IsNullOrWhiteSpace(_mapId) ? GetDefaultMapId() : _mapId);
            _playerRadius = EditorGUILayout.FloatField("Player Radius", _playerRadius);
            _outputFile = EditorGUILayout.TextField("Output File", _outputFile);
            DrawEnemyTagsBackendControls();
            _useObstacleLayer = EditorGUILayout.Toggle("Use Obstacle Layer", _useObstacleLayer);
            if (_useObstacleLayer)
            {
                _obstacleLayerName = EditorGUILayout.TextField("Obstacle Layer", _obstacleLayerName);
            }
            _skipTriggerColliders = EditorGUILayout.Toggle("Skip Trigger Colliders", _skipTriggerColliders);
            _excludeLongBoundaryObstacles = EditorGUILayout.Toggle("Exclude Long Boundary Obstacles", _excludeLongBoundaryObstacles);
            if (_excludeLongBoundaryObstacles)
            {
                _boundaryLengthThreshold = Mathf.Max(1f, EditorGUILayout.FloatField("Boundary Length >=", _boundaryLengthThreshold));
                _boundaryThicknessThreshold = Mathf.Max(0.01f, EditorGUILayout.FloatField("Boundary Thickness <=", _boundaryThicknessThreshold));
            }
            _usePlayerSpawnLayer = EditorGUILayout.Toggle("Use Player Spawn Layer", _usePlayerSpawnLayer);
            if (_usePlayerSpawnLayer)
            {
                _playerSpawnLayerName = EditorGUILayout.TextField("Player Spawn Layer", _playerSpawnLayerName);
            }
            _useEnemySpawnLayer = EditorGUILayout.Toggle("Use Enemy Spawn Layer", _useEnemySpawnLayer);
            if (_useEnemySpawnLayer)
            {
                _enemySpawnLayerName = EditorGUILayout.TextField("Enemy Spawn Layer", _enemySpawnLayerName);
            }

            if (GUILayout.Button("Export"))
            {
                Export();
            }
        }

        private static string GetDefaultMapId()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            return string.IsNullOrWhiteSpace(scene.name) ? "default" : scene.name;
        }

        private void Export()
        {
            var mapId = string.IsNullOrWhiteSpace(_mapId) ? GetDefaultMapId() : _mapId;
            var obstacles = CollectObstacles();
            var playerSpawn = CollectPlayerSpawn();
            var enemySpawns = CollectEnemySpawns();

            if (!LoadEnemyTagsFromBackend())
            {
                Debug.LogWarning($"Enemy tags were not loaded from backend: {_enemyTagsStatus}. Export canceled.");
                return;
            }

            var enemyTags = NormalizeEnemyTags(_enemyTags);
            var map = new MapDef
            {
                id = mapId,
                playerRadius = _playerRadius,
                playerSpawn = playerSpawn,
                enemySpawns = enemySpawns,
                enemyTags = enemyTags,
                obstacles = obstacles
            };

            var json = JsonUtility.ToJson(map, true);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputPath = Path.Combine(projectRoot, _outputFile);
            File.WriteAllText(outputPath, json);
            Debug.Log($"Map exported to {outputPath}");
        }

        private List<MapObstacle> CollectObstacles()
        {
            var result = new List<MapObstacle>();

            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var allTransforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

            foreach (var t in allTransforms)
            {
                if (!IsLayerMatch(t.gameObject, _obstacleLayerName, _useObstacleLayer))
                {
                    continue;
                }

                var box2d = t.GetComponent<BoxCollider2D>();
                if (box2d != null)
                {
                    if (_skipTriggerColliders && box2d.isTrigger)
                    {
                        continue;
                    }

                    var center = box2d.transform.TransformPoint(box2d.offset);
                    var size = Vector2.Scale(box2d.size, box2d.transform.lossyScale);
                    if (ShouldSkipAsBoundaryObstacle(Mathf.Abs(size.x), Mathf.Abs(size.y)))
                    {
                        continue;
                    }

                    result.Add(new MapObstacle
                    {
                        x = center.x,
                        y = center.y,
                        w = Mathf.Abs(size.x),
                        h = Mathf.Abs(size.y),
                        rot = box2d.transform.eulerAngles.z
                    });
                    continue;
                }

                var box3d = t.GetComponent<BoxCollider>();
                if (box3d != null)
                {
                    if (_skipTriggerColliders && box3d.isTrigger)
                    {
                        continue;
                    }

                    var center = box3d.transform.TransformPoint(box3d.center);
                    var size = Vector3.Scale(box3d.size, box3d.transform.lossyScale);
                    if (ShouldSkipAsBoundaryObstacle(Mathf.Abs(size.x), Mathf.Abs(size.z)))
                    {
                        continue;
                    }

                    result.Add(new MapObstacle
                    {
                        x = center.x,
                        y = center.z,
                        w = Mathf.Abs(size.x),
                        h = Mathf.Abs(size.z),
                        rot = box3d.transform.eulerAngles.y
                    });
                }
            }

            return result;
        }

        private MapPoint CollectPlayerSpawn()
        {
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var allTransforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

            foreach (var t in allTransforms)
            {
                if (!IsLayerMatch(t.gameObject, _playerSpawnLayerName, _usePlayerSpawnLayer))
                {
                    continue;
                }

                var pos = GetSpawnPosition(t.position);
                return new MapPoint { x = pos.x, y = pos.y };
            }

            return null;
        }

        private List<MapPoint> CollectEnemySpawns()
        {
            var result = new List<MapPoint>();
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            var allTransforms = roots.SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();

            foreach (var t in allTransforms)
            {
                if (!IsLayerMatch(t.gameObject, _enemySpawnLayerName, _useEnemySpawnLayer))
                {
                    continue;
                }

                var pos = GetSpawnPosition(t.position);
                result.Add(new MapPoint { x = pos.x, y = pos.y });
            }

            return result;
        }

        private bool IsLayerMatch(GameObject go, string layerName, bool useFilter)
        {
            if (!useFilter)
            {
                return true;
            }

            var layer = LayerMask.NameToLayer(layerName);
            if (layer < 0)
            {
                Debug.LogWarning($"Layer '{layerName}' not found. Ignoring filter.");
                return true;
            }

            return go.layer == layer;
        }

        private Vector2 GetSpawnPosition(Vector3 worldPosition)
        {
            return new Vector2(worldPosition.x, worldPosition.z);
        }

        private void DrawEnemyTagsBackendControls()
        {
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("Enemy Tags Source", EditorStyles.boldLabel);
            _backendApiBaseUrl = EditorGUILayout.TextField("Backend API Base URL", _backendApiBaseUrl);
            _enemyTagsEndpoint = EditorGUILayout.TextField("Tags Endpoint", _enemyTagsEndpoint);

            if (GUILayout.Button("Load Enemy Tags From Backend"))
            {
                _ = LoadEnemyTagsFromBackend();
            }

            if (!string.IsNullOrWhiteSpace(_enemyTagsStatus))
            {
                var style = new GUIStyle(EditorStyles.helpBox) { wordWrap = true };
                EditorGUILayout.LabelField(_enemyTagsStatus, style);
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Resolved Enemy Tags", string.Join(", ", _enemyTags));
            EditorGUI.EndDisabledGroup();
        }

        private bool LoadEnemyTagsFromBackend()
        {
            var url = BuildUrl(_backendApiBaseUrl, _enemyTagsEndpoint);
            if (string.IsNullOrWhiteSpace(url))
            {
                _enemyTagsStatus = "Backend URL is empty.";
                return false;
            }

            using var request = UnityWebRequest.Get(url);
            request.timeout = 10;

            try
            {
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    // Block only editor window flow; keeps implementation simple for tooling.
                }
            }
            catch (Exception ex)
            {
                _enemyTagsStatus = $"Failed to call backend: {ex.Message}";
                return false;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                _enemyTagsStatus = $"Failed to load tags from {url} ({request.responseCode}): {request.error}";
                return false;
            }

            EnemyTagsResponse payload;
            try
            {
                payload = JsonUtility.FromJson<EnemyTagsResponse>(request.downloadHandler.text);
            }
            catch (Exception ex)
            {
                _enemyTagsStatus = $"Invalid JSON from backend: {ex.Message}";
                return false;
            }

            if (payload == null || !payload.ok || payload.tags == null)
            {
                _enemyTagsStatus = "Backend returned unexpected payload.";
                return false;
            }

            var normalized = NormalizeEnemyTags(payload.tags);
            if (normalized.Count == 0)
            {
                _enemyTagsStatus = "Backend returned empty tag list.";
                return false;
            }

            _enemyTags = normalized;
            _enemyTagsStatus = $"Loaded {_enemyTags.Count} tags from backend.";
            return true;
        }

        private static string BuildUrl(string baseUrl, string endpoint)
        {
            var trimmedBase = (baseUrl ?? string.Empty).Trim();
            var trimmedEndpoint = (endpoint ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmedBase))
            {
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(trimmedEndpoint))
            {
                return trimmedBase.TrimEnd('/');
            }

            return $"{trimmedBase.TrimEnd('/')}/{trimmedEndpoint.TrimStart('/')}";
        }

        private List<string> NormalizeEnemyTags(List<string> tags)
        {
            return tags
                .Select(t => (t ?? string.Empty).Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct()
                .ToList();
        }

        private bool ShouldSkipAsBoundaryObstacle(float width, float height)
        {
            if (!_excludeLongBoundaryObstacles)
            {
                return false;
            }

            var longHorizontal = width >= _boundaryLengthThreshold && height <= _boundaryThicknessThreshold;
            var longVertical = height >= _boundaryLengthThreshold && width <= _boundaryThicknessThreshold;
            return longHorizontal || longVertical;
        }

        [System.Serializable]
        private sealed class MapDef
        {
            public string id;
            public float playerRadius;
            public MapPoint playerSpawn;
            public List<MapPoint> enemySpawns;
            public List<string> enemyTags;
            public List<MapObstacle> obstacles;
        }

        [Serializable]
        private sealed class EnemyTagsResponse
        {
            public bool ok;
            public List<string> tags;
        }

        [System.Serializable]
        private sealed class MapPoint
        {
            public float x;
            public float y;
        }

        [System.Serializable]
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
