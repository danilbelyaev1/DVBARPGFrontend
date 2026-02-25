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
        private float _radius = 12f;
        private float _playerRadius = 0.35f;
        private string _outputFile = "default.json";
        private bool _useObstacleLayer = true;
        private string _obstacleLayerName = "Obstacle";

        [MenuItem("Tools/Runtime Server/Export Map JSON")]
        public static void Open()
        {
            GetWindow<RuntimeMapExporter>("Map Export");
        }

        private void OnGUI()
        {
            _mapId = EditorGUILayout.TextField("Map Id", string.IsNullOrWhiteSpace(_mapId) ? GetDefaultMapId() : _mapId);
            _radius = EditorGUILayout.FloatField("Radius", _radius);
            _playerRadius = EditorGUILayout.FloatField("Player Radius", _playerRadius);
            _outputFile = EditorGUILayout.TextField("Output File", _outputFile);
            _useObstacleLayer = EditorGUILayout.Toggle("Use Obstacle Layer", _useObstacleLayer);
            if (_useObstacleLayer)
            {
                _obstacleLayerName = EditorGUILayout.TextField("Obstacle Layer", _obstacleLayerName);
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
            var map = new MapDef
            {
                id = mapId,
                radius = _radius,
                playerRadius = _playerRadius,
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
                if (_useObstacleLayer && LayerMask.NameToLayer(_obstacleLayerName) != t.gameObject.layer)
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
                        h = Mathf.Abs(size.y)
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
                        h = Mathf.Abs(size.z)
                    });
                }
            }

            return result;
        }

        [System.Serializable]
        private sealed class MapDef
        {
            public string id;
            public float radius;
            public float playerRadius;
            public List<MapObstacle> obstacles;
        }

        [System.Serializable]
        private sealed class MapObstacle
        {
            public float x;
            public float y;
            public float w;
            public float h;
        }
    }
}