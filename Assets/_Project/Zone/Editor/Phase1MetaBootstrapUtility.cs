#nullable enable
using System.Collections.Generic;
using ExtractionWeight.MetaState;
using ExtractionWeight.Loot;
using ExtractionWeight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ExtractionWeight.Zone.Editor
{
    public static class Phase1MetaBootstrapUtility
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string BaseScenePath = "Assets/_Project/Scenes/Base.unity";

        public static void CreateOrUpdateBaseAndBootContent(ZoneDefinition zoneDefinition, GameObject markerPrefab)
        {
            CreateOrUpdateBaseScene();
            ConfigureBootScene(zoneDefinition, markerPrefab);
        }

        public static void EnsureBaseAndBootContentExists(ZoneDefinition zoneDefinition, GameObject markerPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BaseScenePath) == null)
            {
                CreateOrUpdateBaseAndBootContent(zoneDefinition, markerPrefab);
                return;
            }

            ConfigureBootScene(zoneDefinition, markerPrefab);
        }

        private static void CreateOrUpdateBaseScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lootDatabase = AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
            if (lootDatabase == null)
            {
                throw new System.IO.FileNotFoundException($"Loot database not found at {LootDatabase.AssetPath}.");
            }

            var canvasObject = new GameObject("BaseCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var controller = canvasObject.AddComponent<BaseScreenController>();
            CreatePanelLayout(canvasObject.transform, controller, lootDatabase);

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, BaseScenePath);
        }

        private static void ConfigureBootScene(ZoneDefinition definition, GameObject markerPrefab)
        {
            var lootDatabase = AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
            if (lootDatabase == null)
            {
                throw new System.IO.FileNotFoundException($"Loot database not found at {LootDatabase.AssetPath}.");
            }

            var scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var bootstrap = GameObject.Find("Bootstrap");
            if (bootstrap == null)
            {
                bootstrap = new GameObject("Bootstrap");
            }

            var loader = bootstrap.GetComponent<ZoneLoader>();
            if (loader == null)
            {
                loader = bootstrap.AddComponent<ZoneLoader>();
            }

            var manager = bootstrap.GetComponent<GameFlowManager>();
            if (manager == null)
            {
                manager = bootstrap.AddComponent<GameFlowManager>();
            }

            loader.EditorConfigure(new List<ZoneDefinition> { definition }, markerPrefab);
            manager.EditorConfigure(loader, lootDatabase);

            EditorUtility.SetDirty(loader);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreatePanelLayout(Transform canvasTransform, BaseScreenController controller, LootDatabase lootDatabase)
        {
            var root = CreateRect("LayoutRoot", canvasTransform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var rootLayout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            rootLayout.padding = new RectOffset(32, 32, 32, 32);
            rootLayout.spacing = 16f;
            rootLayout.childControlHeight = false;
            rootLayout.childControlWidth = true;
            rootLayout.childForceExpandHeight = false;
            rootLayout.childForceExpandWidth = true;

            var headerPanel = CreatePanel("HeaderPanel", root, new Color(0.11f, 0.16f, 0.21f, 0.96f), 150f);
            var headerText = CreateText("HeaderText", "Header", headerPanel, TextAnchor.MiddleLeft, 28);

            var contentRow = CreateRect("ContentRow", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            contentRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 620f;
            var contentLayout = contentRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            contentLayout.spacing = 16f;
            contentLayout.childControlHeight = true;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = true;
            contentLayout.childForceExpandWidth = true;

            var stashPanel = CreatePanel("StashPanel", contentRow, new Color(0.15f, 0.19f, 0.23f, 0.94f), -1f);
            stashPanel.gameObject.AddComponent<LayoutElement>().preferredWidth = 760f;
            var stashLayout = stashPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            stashLayout.padding = new RectOffset(18, 18, 18, 18);
            stashLayout.spacing = 10f;
            stashLayout.childControlWidth = true;
            stashLayout.childControlHeight = false;
            stashLayout.childForceExpandHeight = false;

            CreateText("StashTitle", "Stash", stashPanel, TextAnchor.MiddleLeft, 26);
            var stashSummaryText = CreateText("StashSummaryText", "No banked loot yet.", stashPanel, TextAnchor.MiddleLeft, 20);
            var stashScrollObject = new GameObject("StashScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            stashScrollObject.transform.SetParent(stashPanel, false);
            var stashScrollRect = stashScrollObject.GetComponent<ScrollRect>();
            var stashScrollLayout = stashScrollObject.AddComponent<LayoutElement>();
            stashScrollLayout.preferredHeight = 500f;
            var stashScrollImage = stashScrollObject.GetComponent<Image>();
            stashScrollImage.color = new Color(0.08f, 0.11f, 0.15f, 0.9f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(stashScrollObject.transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var stashContent = CreateRect("Content", viewport.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero);
            stashContent.offsetMin = new Vector2(0f, 0f);
            stashContent.offsetMax = new Vector2(0f, 0f);
            var stashContentLayout = stashContent.gameObject.AddComponent<VerticalLayoutGroup>();
            stashContentLayout.padding = new RectOffset(0, 0, 0, 0);
            stashContentLayout.spacing = 8f;
            stashContentLayout.childControlHeight = false;
            stashContentLayout.childControlWidth = true;
            stashContentLayout.childForceExpandHeight = false;
            stashContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            stashScrollRect.content = stashContent;
            stashScrollRect.viewport = viewportRect;
            stashScrollRect.horizontal = false;

            var rightColumn = CreateRect("RightColumn", contentRow, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var rightLayout = rightColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            rightLayout.spacing = 16f;
            rightLayout.childControlHeight = false;
            rightLayout.childControlWidth = true;
            rightLayout.childForceExpandHeight = false;
            rightLayout.childForceExpandWidth = true;

            var runStartPanel = CreatePanel("RunStartPanel", rightColumn, new Color(0.15f, 0.2f, 0.17f, 0.94f), 320f);
            var runStartLayout = runStartPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            runStartLayout.padding = new RectOffset(18, 18, 18, 18);
            runStartLayout.spacing = 12f;
            runStartLayout.childControlHeight = false;
            runStartLayout.childControlWidth = true;
            runStartLayout.childForceExpandHeight = false;
            CreateText("RunStartTitle", "Run Start", runStartPanel, TextAnchor.MiddleLeft, 26);
            var zoneCardText = CreateCard(runStartPanel, "ZoneCard", "Drydock");
            var loadoutCardText = CreateCard(runStartPanel, "LoadoutCard", "Light Pack");
            var enterZoneButton = CreateButton(runStartPanel, "EnterZoneButton", "Enter Zone");

            var lastRunPanel = CreatePanel("LastRunPanel", rightColumn, new Color(0.2f, 0.16f, 0.14f, 0.94f), 284f);
            var lastRunLayout = lastRunPanel.gameObject.AddComponent<VerticalLayoutGroup>();
            lastRunLayout.padding = new RectOffset(18, 18, 18, 18);
            lastRunLayout.spacing = 12f;
            lastRunLayout.childControlHeight = false;
            lastRunLayout.childControlWidth = true;
            lastRunLayout.childForceExpandHeight = false;
            CreateText("LastRunTitle", "Last Run", lastRunPanel, TextAnchor.MiddleLeft, 26);
            var lastRunText = CreateText("LastRunText", "No runs completed yet.", lastRunPanel, TextAnchor.UpperLeft, 20);
            ((RectTransform)lastRunText.transform).sizeDelta = new Vector2(0f, 180f);

            controller.EditorConfigure(
                lootDatabase,
                headerText,
                stashSummaryText,
                stashContent,
                zoneCardText,
                loadoutCardText,
                enterZoneButton,
                lastRunText);
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color, float preferredHeight)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rectTransform = (RectTransform)panel.transform;
            var image = panel.GetComponent<Image>();
            image.color = color;
            if (preferredHeight > 0f)
            {
                panel.AddComponent<LayoutElement>().preferredHeight = preferredHeight;
            }

            return rectTransform;
        }

        private static Text CreateCard(Transform parent, string name, string text)
        {
            var card = CreatePanel(name, parent, new Color(0.09f, 0.11f, 0.15f, 0.92f), 82f);
            return CreateText($"{name}Text", text, card, TextAnchor.MiddleLeft, 22);
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.79f, 0.4f, 0.21f, 1f);
            var layout = buttonObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 68f;

            var labelText = CreateText("Label", label, buttonObject.transform, TextAnchor.MiddleCenter, 24);
            var labelRect = (RectTransform)labelText.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            return buttonObject.GetComponent<Button>();
        }

        private static Text CreateText(string name, string text, Transform parent, TextAnchor alignment, int fontSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var textComponent = textObject.GetComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = fontSize;
            textComponent.alignment = alignment;
            textComponent.color = Color.white;
            textComponent.text = text;
            return textComponent;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }
    }
}
