using DVBARPG.Core.Services;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DVBARPG.Game.Hub
{
    /// <summary>
    /// Маркер кликабельного NPC в хабе; данные приходят с бэка (<see cref="NpcInfo"/>).
    /// </summary>
    public sealed class HubNpcActor : MonoBehaviour
    {
        private const string OutlineShaderName = "GameClient/NpcHoverOutline";

        public NpcInfo Data { get; private set; }
        [SerializeField] private Color outlineColor = Color.white;
        [SerializeField, Range(0.01f, 0.12f)] private float outlineThickness = 0.04f;

        private readonly List<Renderer> _outlineRenderers = new List<Renderer>();
        private Material _outlineMaterial;
        private bool _isHovered;

        public void Bind(NpcInfo npc)
        {
            Data = npc;
        }

        private void Awake()
        {
            BuildOutlineRenderers();
        }

        public void SetHovered(bool isHovered)
        {
            if (_isHovered == isHovered)
            {
                return;
            }

            _isHovered = isHovered;
            if (_outlineMaterial != null)
            {
                _outlineMaterial.SetColor("_OutlineColor", outlineColor);
                _outlineMaterial.SetFloat("_OutlineThickness", outlineThickness);
            }

            foreach (var renderer in _outlineRenderers)
            {
                if (renderer != null) renderer.enabled = isHovered;
            }
        }

        private void OnDestroy()
        {
            if (_outlineMaterial != null)
            {
                Destroy(_outlineMaterial);
            }
        }

        private void BuildOutlineRenderers()
        {
            var shader = Shader.Find(OutlineShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[HubNpcActor] Outline shader '{OutlineShaderName}' not found.");
                return;
            }

            _outlineMaterial = new Material(shader);
            _outlineMaterial.name = "NpcHoverOutline_Runtime";
            _outlineMaterial.SetColor("_OutlineColor", outlineColor);
            _outlineMaterial.SetFloat("_OutlineThickness", outlineThickness);

            var skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var source in skinnedRenderers)
            {
                if (source == null || source.sharedMesh == null) continue;
                var outlineGo = new GameObject(source.name + "_Outline");
                outlineGo.transform.SetParent(source.transform, false);
                outlineGo.layer = source.gameObject.layer;

                var outlineRenderer = outlineGo.AddComponent<SkinnedMeshRenderer>();
                outlineRenderer.sharedMesh = source.sharedMesh;
                outlineRenderer.bones = source.bones;
                outlineRenderer.rootBone = source.rootBone;
                outlineRenderer.localBounds = source.localBounds;
                outlineRenderer.updateWhenOffscreen = true;
                outlineRenderer.material = _outlineMaterial;
                outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                outlineRenderer.receiveShadows = false;
                outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
                outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                outlineRenderer.enabled = false;
                _outlineRenderers.Add(outlineRenderer);
            }

            var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var source in meshRenderers)
            {
                if (source == null) continue;
                var meshFilter = source.GetComponent<MeshFilter>();
                if (meshFilter == null || meshFilter.sharedMesh == null) continue;

                var outlineGo = new GameObject(source.name + "_Outline");
                outlineGo.transform.SetParent(source.transform, false);
                outlineGo.layer = source.gameObject.layer;
                var outlineFilter = outlineGo.AddComponent<MeshFilter>();
                outlineFilter.sharedMesh = meshFilter.sharedMesh;

                var outlineRenderer = outlineGo.AddComponent<MeshRenderer>();
                outlineRenderer.material = _outlineMaterial;
                outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                outlineRenderer.receiveShadows = false;
                outlineRenderer.lightProbeUsage = LightProbeUsage.Off;
                outlineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                outlineRenderer.enabled = false;
                _outlineRenderers.Add(outlineRenderer);
            }
        }
    }
}
