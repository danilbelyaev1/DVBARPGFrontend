#if UNITY_EDITOR
namespace Tools.Editor
{
    using DVBARPG.Game.Hub;
    using DVBARPG.UI.Common;
    using DVBARPG.UI.Hub;
    using DVBARPG.UI.Shop;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.UI;

    /// <summary>
    /// Одноразовая/повторная сборка <see cref="NpcInteractionMenu"/> под объектом Hub и привязка <see cref="HubWorldPointerInput"/>.
    /// </summary>
    public static class HubNpcInteractionMenuSetup
    {
        private const string MenuPath = "Tools/Hub/Setup Npc Interaction Menu";

        [MenuItem(MenuPath)]
        public static void Setup()
        {
            var shop = Object.FindFirstObjectByType<NpcShopScreen>(FindObjectsInactive.Include);
            if (shop == null)
            {
                EditorUtility.DisplayDialog("Hub NPC UI", "В активной сцене не найден NpcShopScreen (панель магазина). Откройте act1_hub.", "OK");
                return;
            }

            var hub = shop.transform.parent;
            if (hub == null)
            {
                EditorUtility.DisplayDialog("Hub NPC UI", "NpcShopScreen без родителя (ожидается Hub).", "OK");
                return;
            }

            var existing = hub.Find("NpcInteractionMenuRoot");
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var rootGo = new GameObject("NpcInteractionMenuRoot", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(rootGo, "Create NpcInteractionMenuRoot");
            rootGo.transform.SetParent(hub, false);
            StretchFull(rootGo.GetComponent<RectTransform>());
            SetLayerRecursively(rootGo, hub.gameObject.layer);

            var panelGo = new GameObject("NpcDialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            Undo.RegisterCreatedObjectUndo(panelGo, "Create NpcDialogPanel");
            panelGo.transform.SetParent(rootGo.transform, false);
            StretchFull(panelGo.GetComponent<RectTransform>());
            var panelBg = panelGo.GetComponent<Image>();
            panelBg.color = new Color(0.06f, 0.07f, 0.1f, 0.92f);
            panelBg.raycastTarget = true;
            panelGo.SetActive(false);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            Undo.RegisterCreatedObjectUndo(titleGo, "Create Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 44f);
            titleRt.anchoredPosition = new Vector2(0f, -8f);
            var titleText = titleGo.GetComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 26;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "NPC";

            var closeGo = new GameObject("CloseButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            Undo.RegisterCreatedObjectUndo(closeGo, "Create CloseButton");
            closeGo.transform.SetParent(panelGo.transform, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.sizeDelta = new Vector2(40f, 40f);
            closeRt.anchoredPosition = new Vector2(-10f, -8f);
            var closeImg = closeGo.GetComponent<Image>();
            closeImg.color = new Color(0.25f, 0.25f, 0.3f, 1f);
            closeImg.raycastTarget = true;
            var closeBtn = closeGo.GetComponent<Button>();
            var closeLabelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            StretchFull(closeLabelGo.GetComponent<RectTransform>());
            var closeLabel = closeLabelGo.GetComponent<Text>();
            closeLabel.font = font;
            closeLabel.fontSize = 22;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = Color.white;
            closeLabel.text = "×";

            var buttonsGo = new GameObject("Buttons", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(buttonsGo, "Create Buttons");
            buttonsGo.transform.SetParent(panelGo.transform, false);
            var buttonsRt = buttonsGo.GetComponent<RectTransform>();
            buttonsRt.anchorMin = new Vector2(0f, 0f);
            buttonsRt.anchorMax = new Vector2(1f, 1f);
            buttonsRt.offsetMin = new Vector2(24f, 24f);
            buttonsRt.offsetMax = new Vector2(-24f, -56f);

            var menu = Undo.AddComponent<NpcInteractionMenu>(rootGo);
            var soMenu = new SerializedObject(menu);
            soMenu.FindProperty("panelRoot").objectReferenceValue = panelGo;
            soMenu.FindProperty("titleText").objectReferenceValue = titleText;
            soMenu.FindProperty("buttonsParent").objectReferenceValue = buttonsRt;
            soMenu.FindProperty("closeButton").objectReferenceValue = closeBtn;
            soMenu.FindProperty("shopScreen").objectReferenceValue = shop;
            soMenu.FindProperty("shopPanelRoot").objectReferenceValue = shop.gameObject;
            var toast = Object.FindFirstObjectByType<ErrorToast>(FindObjectsInactive.Include);
            soMenu.FindProperty("errorToast").objectReferenceValue = toast;
            soMenu.ApplyModifiedPropertiesWithoutUndo();

            var pointer = Object.FindFirstObjectByType<HubWorldPointerInput>(FindObjectsInactive.Include);
            if (pointer != null)
            {
                var soPtr = new SerializedObject(pointer);
                soPtr.FindProperty("interactionMenu").objectReferenceValue = menu;
                soPtr.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[HubNpcInteractionMenuSetup] HubWorldPointerInput не найден — назначьте interactionMenu вручную на HubRuntime.");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[HubNpcInteractionMenuSetup] NpcInteractionMenu создан под «" + hub.name + "».");
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (var i = 0; i < t.childCount; i++)
            {
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
            }
        }
    }
}
#endif
