using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tools
{
    public sealed class IslandObstacleGenerator : EditorWindow
    {
        [Header("Источник")]
        [Tooltip("Terrain, от которого берём границы и высоту.")]
        [SerializeField] private Terrain terrain;
        [Tooltip("Если Terrain не задан, используем bounds этого объекта (Renderer/Collider).")]
        [SerializeField] private GameObject sourceRoot;
        [Tooltip("Слои, которые считаются землёй (для Raycast).")]
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Параметры карты")]
        [Tooltip("Высота воды. Всё ниже — вода.")]
        [SerializeField] private float seaLevel = 0f;
        [Tooltip("Размер клетки для семплинга (м).")]
        [SerializeField] private float cellSize = 1f;
        [Tooltip("Макс. расстояние луча вниз (м).")]
        [SerializeField] private float rayDistance = 200f;

        [Header("Параметры препятствий")]
        [Tooltip("Высота стены (м).")]
        [SerializeField] private float wallHeight = 4f;
        [Tooltip("Толщина стены (м).")]
        [SerializeField] private float wallThickness = 0.4f;
        [Tooltip("Создавать единый меш вместо множества объектов.")]
        [SerializeField] private bool useCombinedMesh = true;
        [Tooltip("Создавать только экспортные BoxCollider'ы (без меша).")]
        [SerializeField] private bool exportOnly = false;
        [Tooltip("Добавлять MeshCollider на общий меш.")]
        [SerializeField] private bool addMeshCollider = true;
        [Tooltip("Создавать BoxCollider'ы для экспорта в JSON.")]
        [SerializeField] private bool generateExportBoxes = true;
        [Tooltip("Скрывать экспортные BoxCollider'ы в иерархии.")]
        [SerializeField] private bool hideExportBoxes = true;
        [Tooltip("Слой для препятствий.")]
        [SerializeField] private string obstacleLayerName = "Obstacle";
        [Tooltip("Имя родительского объекта с препятствиями.")]
        [SerializeField] private string obstaclesRootName = "GeneratedObstacles";
        [Tooltip("Удалять предыдущую генерацию.")]
        [SerializeField] private bool clearPrevious = true;

        [MenuItem("Tools/Runtime Server/Generate Island Obstacles")]
        public static void Open()
        {
            GetWindow<IslandObstacleGenerator>("Island Obstacles");
        }

        private void OnGUI()
        {
            terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
            sourceRoot = (GameObject)EditorGUILayout.ObjectField("Source Root", sourceRoot, typeof(GameObject), true);
            groundMask = LayerMaskField("Ground Mask", groundMask);

            seaLevel = EditorGUILayout.FloatField("Sea Level", seaLevel);
            cellSize = Mathf.Max(0.25f, EditorGUILayout.FloatField("Cell Size", cellSize));
            rayDistance = Mathf.Max(1f, EditorGUILayout.FloatField("Ray Distance", rayDistance));

            wallHeight = Mathf.Max(0.1f, EditorGUILayout.FloatField("Wall Height", wallHeight));
            wallThickness = Mathf.Max(0.05f, EditorGUILayout.FloatField("Wall Thickness", wallThickness));
            exportOnly = EditorGUILayout.Toggle("Export Only", exportOnly);
            useCombinedMesh = EditorGUILayout.Toggle("Combined Mesh", useCombinedMesh);
            addMeshCollider = EditorGUILayout.Toggle("Add MeshCollider", addMeshCollider);
            generateExportBoxes = EditorGUILayout.Toggle("Generate Export Boxes", generateExportBoxes);
            hideExportBoxes = EditorGUILayout.Toggle("Hide Export Boxes", hideExportBoxes);
            obstacleLayerName = EditorGUILayout.TextField("Obstacle Layer", obstacleLayerName);
            obstaclesRootName = EditorGUILayout.TextField("Root Name", obstaclesRootName);
            clearPrevious = EditorGUILayout.Toggle("Clear Previous", clearPrevious);

            GUILayout.Space(8);
            if (GUILayout.Button("Generate Obstacles"))
            {
                Generate();
            }
        }

        private void Generate()
        {
            if (!TryGetBounds(out var bounds))
            {
                Debug.LogWarning("Не удалось определить bounds источника.");
                return;
            }

            var layer = LayerMask.NameToLayer(obstacleLayerName);
            if (layer < 0)
            {
                Debug.LogWarning($"Layer '{obstacleLayerName}' не найден. Объекты останутся на Default.");
            }

            var root = GameObject.Find(obstaclesRootName);
            if (root != null && clearPrevious)
            {
                DestroyImmediate(root);
                root = null;
            }

            if (root == null)
            {
                root = new GameObject(obstaclesRootName);
            }

            if (layer >= 0) root.layer = layer;

            var sizeX = Mathf.CeilToInt(bounds.size.x / cellSize);
            var sizeZ = Mathf.CeilToInt(bounds.size.z / cellSize);

            var land = new bool[sizeX, sizeZ];

            // Семплим карту
            for (int ix = 0; ix < sizeX; ix++)
            {
                for (int iz = 0; iz < sizeZ; iz++)
                {
                    var world = new Vector3(
                        bounds.min.x + (ix + 0.5f) * cellSize,
                        bounds.max.y + 1f,
                        bounds.min.z + (iz + 0.5f) * cellSize);

                    land[ix, iz] = IsLand(world);
                }
            }

            // Собираем клетки границы
            var edgeCells = new List<Vector3>();
            var edgeCellsForExport = new List<Vector3>();
            int created = 0;
            for (int ix = 0; ix < sizeX; ix++)
            {
                for (int iz = 0; iz < sizeZ; iz++)
                {
                    if (!land[ix, iz]) continue;

                    bool edge = false;
                    for (int dx = -1; dx <= 1 && !edge; dx++)
                    {
                        for (int dz = -1; dz <= 1 && !edge; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            var nx = ix + dx;
                            var nz = iz + dz;
                            if (nx < 0 || nz < 0 || nx >= sizeX || nz >= sizeZ)
                            {
                                edge = true;
                                break;
                            }
                            if (!land[nx, nz]) edge = true;
                        }
                    }

                    if (!edge) continue;

                    var outward = GetOutwardNormal(land, ix, iz);
                    var offset = outward * ((cellSize + wallThickness) * 0.5f);

                    var center = new Vector3(
                        bounds.min.x + (ix + 0.5f) * cellSize,
                        seaLevel + wallHeight * 0.5f,
                        bounds.min.z + (iz + 0.5f) * cellSize) + new Vector3(offset.x, 0f, offset.y);

                    edgeCells.Add(center);
                    edgeCellsForExport.Add(center);
                    created++;
                }
            }

            if (useCombinedMesh && !exportOnly)
            {
                var meshGo = new GameObject("ObstacleMesh");
                meshGo.transform.SetParent(root.transform, false);
                if (layer >= 0) meshGo.layer = layer;

                var mf = meshGo.AddComponent<MeshFilter>();
                var mr = meshGo.AddComponent<MeshRenderer>();
                mr.enabled = false;

                mf.sharedMesh = BuildCombinedMesh(edgeCells, cellSize + wallThickness, wallHeight);

                if (addMeshCollider)
                {
                    var mc = meshGo.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }

            if (generateExportBoxes)
            {
                var boxesRoot = new GameObject("ObstacleBoxesExport");
                boxesRoot.transform.SetParent(root.transform, false);
                if (layer >= 0) boxesRoot.layer = layer;
                if (hideExportBoxes) boxesRoot.hideFlags = HideFlags.HideInHierarchy;

                var sizeXZ = cellSize + wallThickness;
                foreach (var c in edgeCellsForExport)
                {
                    var go = new GameObject("ObstacleBox");
                    go.transform.SetParent(boxesRoot.transform, false);
                    go.transform.position = c;
                    if (layer >= 0) go.layer = layer;
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(sizeXZ, wallHeight, sizeXZ);
                }
            }

            Debug.Log($"Сгенерировано препятствий (клетки границы): {created}");
        }

        private bool TryGetBounds(out Bounds bounds)
        {
            if (terrain != null)
            {
                var pos = terrain.transform.position;
                var size = terrain.terrainData.size;
                bounds = new Bounds(pos + size * 0.5f, size);
                return true;
            }

            if (sourceRoot == null)
            {
                bounds = default;
                return false;
            }

            var renderer = sourceRoot.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                bounds = renderer.bounds;
                return true;
            }

            var collider = sourceRoot.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                bounds = collider.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        private bool IsLand(Vector3 world)
        {
            if (terrain != null)
            {
                var h = terrain.SampleHeight(world) + terrain.GetPosition().y;
                return h > seaLevel + 0.01f;
            }

            var origin = world + Vector3.up * (rayDistance * 0.5f);
            if (Physics.Raycast(origin, Vector3.down, out var hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y > seaLevel + 0.01f;
            }

            return false;
        }

        private static Vector2 GetOutwardNormal(bool[,] land, int ix, int iz)
        {
            var sizeX = land.GetLength(0);
            var sizeZ = land.GetLength(1);
            var n = Vector2.zero;

            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var nx = ix + dx;
                    var nz = iz + dz;
                    var isWater = nx < 0 || nz < 0 || nx >= sizeX || nz >= sizeZ || !land[nx, nz];
                    if (isWater)
                    {
                        n += new Vector2(dx, dz);
                    }
                }
            }

            if (n.sqrMagnitude < 0.001f) return Vector2.zero;
            return n.normalized;
        }

        private static Mesh BuildCombinedMesh(List<Vector3> centers, float sizeXZ, float height)
        {
            var mesh = new Mesh();
            if (centers.Count == 0) return mesh;

            var verts = new List<Vector3>(centers.Count * 24);
            var tris = new List<int>(centers.Count * 36);

            var half = sizeXZ * 0.5f;
            var h = height * 0.5f;

            foreach (var c in centers)
            {
                var v0 = new Vector3(c.x - half, c.y - h, c.z - half);
                var v1 = new Vector3(c.x + half, c.y - h, c.z - half);
                var v2 = new Vector3(c.x + half, c.y - h, c.z + half);
                var v3 = new Vector3(c.x - half, c.y - h, c.z + half);
                var v4 = new Vector3(c.x - half, c.y + h, c.z - half);
                var v5 = new Vector3(c.x + half, c.y + h, c.z - half);
                var v6 = new Vector3(c.x + half, c.y + h, c.z + half);
                var v7 = new Vector3(c.x - half, c.y + h, c.z + half);

                var start = verts.Count;
                verts.AddRange(new[] { v0, v1, v2, v3, v4, v5, v6, v7 });

                // Bottom
                tris.AddRange(new[] { start + 0, start + 2, start + 1, start + 0, start + 3, start + 2 });
                // Top
                tris.AddRange(new[] { start + 4, start + 5, start + 6, start + 4, start + 6, start + 7 });
                // Front
                tris.AddRange(new[] { start + 3, start + 6, start + 2, start + 3, start + 7, start + 6 });
                // Back
                tris.AddRange(new[] { start + 0, start + 1, start + 5, start + 0, start + 5, start + 4 });
                // Left
                tris.AddRange(new[] { start + 0, start + 4, start + 7, start + 0, start + 7, start + 3 });
                // Right
                tris.AddRange(new[] { start + 1, start + 2, start + 6, start + 1, start + 6, start + 5 });
            }

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static LayerMask LayerMaskField(string label, LayerMask selected)
        {
            var layers = new List<string>();
            var layerNumbers = new List<int>();

            for (int i = 0; i < 32; i++)
            {
                var layerName = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(layerName))
                {
                    layers.Add(layerName);
                    layerNumbers.Add(i);
                }
            }

            var maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if (((1 << layerNumbers[i]) & selected.value) > 0)
                {
                    maskWithoutEmpty |= (1 << i);
                }
            }

            maskWithoutEmpty = EditorGUILayout.MaskField(label, maskWithoutEmpty, layers.ToArray());

            var mask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((maskWithoutEmpty & (1 << i)) > 0)
                {
                    mask |= (1 << layerNumbers[i]);
                }
            }

            selected.value = mask;
            return selected;
        }
    }
}
