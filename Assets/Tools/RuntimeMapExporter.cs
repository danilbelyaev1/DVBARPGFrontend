namespace Tools
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;

    public sealed class RuntimeMapExporter : EditorWindow
    {
        private string _mapId = "";
        private float _playerRadius = 0.35f;
        private string _outputFile = "default.json";
        private bool _useObstacleLayer = true;
        private string _obstacleLayerName = "Obstacle";
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
            _useObstacleLayer = EditorGUILayout.Toggle("Use Obstacle Layer", _useObstacleLayer);
            if (_useObstacleLayer)
            {
                _obstacleLayerName = EditorGUILayout.TextField("Obstacle Layer", _obstacleLayerName);
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
            var map = new MapDef
            {
                id = mapId,
                playerRadius = _playerRadius,
                playerSpawn = playerSpawn,
                enemySpawns = enemySpawns,
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
                    var center = box2d.transform.TransformPoint(box2d.offset);
                    var size = Vector2.Scale(box2d.size, box2d.transform.lossyScale);
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
                    var center = box3d.transform.TransformPoint(box3d.center);
                    var size = Vector3.Scale(box3d.size, box3d.transform.lossyScale);
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

        [System.Serializable]
        private sealed class MapDef
        {
            public string id;
            public float playerRadius;
            public MapPoint playerSpawn;
            public List<MapPoint> enemySpawns;
            public List<MapObstacle> obstacles;
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
