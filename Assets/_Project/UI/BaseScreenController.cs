#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Loot;
using ExtractionWeight.MetaState;
using UnityEngine;
using UnityEngine.UI;

namespace ExtractionWeight.UI
{
    [DisallowMultipleComponent]
    public sealed class BaseScreenController : MonoBehaviour
    {
        [SerializeField]
        private LootDatabase? _lootDatabase;

        [SerializeField]
        private Text? _headerText;

        [SerializeField]
        private Text? _stashSummaryText;

        [SerializeField]
        private RectTransform? _stashContentRoot;

        [SerializeField]
        private Text? _zoneCardText;

        [SerializeField]
        private Text? _loadoutCardText;

        [SerializeField]
        private Button? _enterZoneButton;

        [SerializeField]
        private Text? _lastRunText;

        public string HeaderText => _headerText != null ? _headerText.text : string.Empty;

        public string LastRunText => _lastRunText != null ? _lastRunText.text : string.Empty;

        private void OnEnable()
        {
            if (_enterZoneButton != null)
            {
                _enterZoneButton.onClick.AddListener(HandleEnterZoneClicked);
            }

            SubscribeToFlowManager();
            Refresh();
        }

        private void OnDisable()
        {
            if (_enterZoneButton != null)
            {
                _enterZoneButton.onClick.RemoveListener(HandleEnterZoneClicked);
            }

            UnsubscribeFromFlowManager();
        }

        private void Refresh()
        {
            RefreshHeader();
            RefreshStash();
            RefreshRunStartPanel();
            RefreshLastRun();
        }

        private void RefreshHeader()
        {
            var manager = GameFlowManager.Instance;
            if (_headerText == null || manager == null)
            {
                return;
            }

            _headerText.text =
                $"Stash Value: ${PlayerStash.Instance.TotalValue:0}\n" +
                $"Runs Attempted: {manager.SessionStats.RunsAttempted}   Runs Successful: {manager.SessionStats.RunsSuccessful}";
        }

        private void RefreshStash()
        {
            if (_stashSummaryText != null)
            {
                _stashSummaryText.text = PlayerStash.Instance.Items.Count == 0
                    ? "No banked loot yet."
                    : $"Banked Items: {PlayerStash.Instance.Items.Count}";
            }

            if (_stashContentRoot == null)
            {
                return;
            }

            for (var i = _stashContentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_stashContentRoot.GetChild(i).gameObject);
            }

            if (PlayerStash.Instance.Items.Count == 0)
            {
                CreateStashEntry("Empty stash", null);
                return;
            }

            for (var i = 0; i < PlayerStash.Instance.Items.Count; i++)
            {
                var storedItem = PlayerStash.Instance.Items[i];
                var definition = ResolveDefinition(storedItem.ItemId);
                var valueText = definition != null
                    ? $"${definition.Value * storedItem.Count:0}"
                    : "$0";
                var title = definition != null
                    ? $"{definition.DisplayName} x{storedItem.Count}  {valueText}"
                    : $"{storedItem.ItemId} x{storedItem.Count}  {valueText}";

                CreateStashEntry(title, definition != null ? definition.Icon : null);
            }
        }

        private void RefreshRunStartPanel()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            var zone = manager.GetZoneDefinition("drydock");
            if (_zoneCardText != null)
            {
                _zoneCardText.text = zone != null
                    ? $"{zone.DisplayName}\nPhase 1 Zone"
                    : "Drydock\nUnavailable";
            }

            if (_loadoutCardText != null)
            {
                var loadout = manager.GetAvailableLoadout();
                _loadoutCardText.text = $"{loadout.DisplayName}\nCapacity {loadout.StartingCapacity:0}\nNo starting items";
            }

            if (_enterZoneButton != null)
            {
                _enterZoneButton.interactable = manager.State == GameFlowState.AtBase && zone != null;
            }
        }

        private void RefreshLastRun()
        {
            if (_lastRunText == null)
            {
                return;
            }

            var summary = GameFlowManager.Instance?.LastRunSummary;
            if (summary == null)
            {
                _lastRunText.text = "No runs completed yet.";
                return;
            }

            var itemLines = new List<string>();
            for (var i = 0; i < summary.BankedItems.Count; i++)
            {
                var item = summary.BankedItems[i];
                var definition = ResolveDefinition(item.ItemId);
                itemLines.Add(definition != null ? $"{definition.DisplayName} x{item.Count}" : $"{item.ItemId} x{item.Count}");
            }

            var itemsText = itemLines.Count == 0 ? "No items banked" : string.Join(", ", itemLines);
            _lastRunText.text =
                $"{(summary.WasSuccessful ? "Success" : "Failed")} - {summary.ZoneDisplayName}\n" +
                $"{itemsText}\n" +
                $"Duration: {summary.DurationSeconds:0.0}s";
        }

        private void HandleEnterZoneClicked()
        {
            GameFlowManager.Instance?.EnterZone("drydock");
        }

        private void HandleStateOrDataChanged()
        {
            Refresh();
        }

        private void SubscribeToFlowManager()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.StateChanged += HandleStateOrDataChanged;
            manager.SessionChanged += HandleStateOrDataChanged;
            manager.StashChanged += HandleStateOrDataChanged;
            manager.LastRunChanged += HandleStateOrDataChanged;
        }

        private void UnsubscribeFromFlowManager()
        {
            var manager = GameFlowManager.Instance;
            if (manager == null)
            {
                return;
            }

            manager.StateChanged -= HandleStateOrDataChanged;
            manager.SessionChanged -= HandleStateOrDataChanged;
            manager.StashChanged -= HandleStateOrDataChanged;
            manager.LastRunChanged -= HandleStateOrDataChanged;
        }

        private LootDefinition? ResolveDefinition(string itemId)
        {
            if (_lootDatabase == null)
            {
                return null;
            }

            try
            {
                return _lootDatabase.GetById(itemId);
            }
            catch
            {
                return null;
            }
        }

        private void CreateStashEntry(string labelText, Sprite? icon)
        {
            if (_stashContentRoot == null)
            {
                return;
            }

            var entry = new GameObject("StashEntry", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            entry.transform.SetParent(_stashContentRoot, false);
            var entryImage = entry.GetComponent<Image>();
            entryImage.color = new Color(0.13f, 0.17f, 0.21f, 0.92f);

            var layout = entry.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandWidth = false;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(entry.transform, false);
            var iconImage = iconObject.GetComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = icon != null ? Color.white : new Color(0.45f, 0.49f, 0.55f, 1f);
            ((RectTransform)iconObject.transform).sizeDelta = new Vector2(36f, 36f);

            var label = CreateText("Label", labelText, entry.transform, TextAnchor.MiddleLeft, 18);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            ((RectTransform)label.transform).sizeDelta = new Vector2(500f, 36f);
        }

        private static Text CreateText(string name, string text, Transform parent, TextAnchor alignment, int fontSize)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var uiText = textObject.GetComponent<Text>();
            uiText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            uiText.fontSize = fontSize;
            uiText.alignment = alignment;
            uiText.color = Color.white;
            uiText.text = text;
            return uiText;
        }

#if UNITY_EDITOR
        public void EditorConfigure(
            LootDatabase lootDatabase,
            Text headerText,
            Text stashSummaryText,
            RectTransform stashContentRoot,
            Text zoneCardText,
            Text loadoutCardText,
            Button enterZoneButton,
            Text lastRunText)
        {
            _lootDatabase = lootDatabase;
            _headerText = headerText;
            _stashSummaryText = stashSummaryText;
            _stashContentRoot = stashContentRoot;
            _zoneCardText = zoneCardText;
            _loadoutCardText = loadoutCardText;
            _enterZoneButton = enterZoneButton;
            _lastRunText = lastRunText;
        }
#endif
    }
}
