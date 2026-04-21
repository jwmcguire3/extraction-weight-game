#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExtractionWeight.Core;
using ExtractionWeight.Loot;
using ExtractionWeight.Zone;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace ExtractionWeight.MetaState
{
    [DisallowMultipleComponent]
    public sealed class GameFlowManager : MonoBehaviour
    {
        private const string BaseSceneName = "Base";
        private const string BootSceneName = "Boot";
        private static readonly RunLoadoutSelection LightPack = new("light-pack", "Light Pack", 100f);

        private static GameFlowManager? s_instance;

        [SerializeField]
        private ZoneLoader? _zoneLoader;

        [SerializeField]
        private LootDatabase? _lootDatabase;

        private PlayerHealth? _activePlayerHealth;
        private bool _isTransitioning;
        private bool _isQueuingBaseReload;
        private float _runStartRealtimeSeconds;

        public static GameFlowManager? Instance => s_instance;

        public GameFlowState State { get; private set; } = GameFlowState.AtBase;

        public RunSessionStats SessionStats { get; } = new();

        public RunContext? CurrentRunContext { get; private set; }

        public LastRunSummary? LastRunSummary { get; private set; }

        public event Action? StateChanged;

        public event Action? SessionChanged;

        public event Action? StashChanged;

        public event Action? LastRunChanged;

        private void Awake()
        {
            if (s_instance != null && s_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _zoneLoader ??= GetComponent<ZoneLoader>();
            ConfigureStashResolver();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Update()
        {
            var activeScene = SceneManager.GetActiveScene();
            var baseScene = SceneManager.GetSceneByName(BaseSceneName);
            if (activeScene.name == BootSceneName &&
                !baseScene.isLoaded &&
                (CurrentRunContext == null || State == GameFlowState.AtBase || State == GameFlowState.ReturningToBase))
            {
                QueueBaseSceneReload();
            }
        }

        private void Start()
        {
            QueueBaseSceneReload();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (s_instance == this)
            {
                s_instance = null;
            }
        }

        public RunLoadoutSelection GetAvailableLoadout()
        {
            return LightPack;
        }

        public ZoneDefinition? GetZoneDefinition(string zoneId)
        {
            return _zoneLoader != null ? _zoneLoader.GetZoneDefinition(zoneId) : null;
        }

        public async void EnterZone(string zoneId)
        {
            if (_zoneLoader == null || _isTransitioning || State != GameFlowState.AtBase)
            {
                return;
            }

            var zoneDefinition = _zoneLoader.GetZoneDefinition(zoneId);
            if (zoneDefinition == null)
            {
                throw new InvalidOperationException($"Unknown zone id '{zoneId}'.");
            }

            _isTransitioning = true;
            SessionStats.RunsAttempted++;
            SessionChanged?.Invoke();

            CurrentRunContext = new RunContext(zoneDefinition.ZoneId, zoneDefinition.DisplayName, LightPack);
            _runStartRealtimeSeconds = Time.realtimeSinceStartup;
            SetState(GameFlowState.EnteringZone);

            await UnloadBaseSceneIfLoadedAsync();
            await _zoneLoader.LoadZoneByIdAsync(zoneId);

            if (_zoneLoader.LoadedZoneScene.HasValue)
            {
                SceneManager.SetActiveScene(_zoneLoader.LoadedZoneScene.Value);
            }

            SetState(GameFlowState.InZone);
            _isTransitioning = false;
        }

        public void NotifyExtractionStarted()
        {
            if (State == GameFlowState.InZone)
            {
                SetState(GameFlowState.ExtractingFromZone);
            }
        }

        public void FailCurrentRun()
        {
            if (CurrentRunContext == null || State == GameFlowState.ReturningToBase || _isTransitioning)
            {
                return;
            }

            _ = ReturnToBaseAfterRunAsync(
                wasSuccessful: false,
                CurrentRunContext.ZoneId,
                new List<LootItem>(),
                totalBankedValue: 0f,
                lostLootValue: 0f,
                presentationDelaySeconds: 0f,
                clearCarryOnFinish: false);
        }

        public void ResetProgressForTests()
        {
            CurrentRunContext = null;
            LastRunSummary = null;
            SessionStats.RunsAttempted = 0;
            SessionStats.RunsSuccessful = 0;
            State = GameFlowState.AtBase;
            _isTransitioning = false;
            PlayerStash.Instance.ClearItems();
            SessionChanged?.Invoke();
            StashChanged?.Invoke();
            LastRunChanged?.Invoke();
            StateChanged?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == BaseSceneName)
            {
                SceneManager.SetActiveScene(scene);
                _isQueuingBaseReload = false;
                UnsubscribeFromActivePlayerHealth();
                return;
            }

            HookActivePlayerHealth();
        }

        public void CompleteSuccessfulRun(string zoneId, List<LootItem> bankedItems)
        {
            if (_isTransitioning || CurrentRunContext == null)
            {
                return;
            }

            _ = ReturnToBaseAfterRunAsync(true, zoneId, bankedItems, CalculateTotalValue(bankedItems), 0f, 0f, false);
        }

        public void CompleteFailedRun(string zoneId)
        {
            if (_isTransitioning || CurrentRunContext == null)
            {
                return;
            }

            _ = ReturnToBaseAfterRunAsync(false, zoneId, new List<LootItem>(), 0f, 0f, 0f, false);
        }

        private async Task ReturnToBaseAfterRunAsync(
            bool wasSuccessful,
            string zoneId,
            List<LootItem> bankedItems,
            float totalBankedValue,
            float lostLootValue,
            float presentationDelaySeconds,
            bool clearCarryOnFinish)
        {
            _isTransitioning = true;
            SetState(GameFlowState.ReturningToBase);

            if (wasSuccessful)
            {
                SessionStats.RunsSuccessful++;
                SessionChanged?.Invoke();
                PlayerStash.Instance.BankItems(bankedItems);
                StashChanged?.Invoke();
            }

            LastRunSummary = BuildLastRunSummary(wasSuccessful, zoneId, bankedItems, totalBankedValue, lostLootValue);
            LastRunChanged?.Invoke();

            if (presentationDelaySeconds > 0f)
            {
                await Task.Delay(Mathf.CeilToInt(presentationDelaySeconds * 1000f));
            }

            if (clearCarryOnFinish)
            {
                var player = FindAnyObjectByType<PlayerController>();
                player?.CarryState.Clear();
            }

            if (_zoneLoader != null)
            {
                await _zoneLoader.UnloadCurrentZoneAsync();
            }

            await EnsureBaseSceneLoadedAsync(forceReload: true);
            CurrentRunContext = null;
            SetState(GameFlowState.AtBase);
            _isTransitioning = false;
        }

        private void ConfigureStashResolver()
        {
            PlayerStash.SetItemValueResolver(itemId =>
            {
                if (_lootDatabase == null)
                {
                    return 0f;
                }

                try
                {
                    return _lootDatabase.GetById(itemId).Value;
                }
                catch
                {
                    return 0f;
                }
            });
        }

        private async Task EnsureBaseSceneLoadedAsync(bool forceReload = false)
        {
            var baseScene = SceneManager.GetSceneByName(BaseSceneName);
            if (baseScene.isLoaded && !forceReload)
            {
                SceneManager.SetActiveScene(baseScene);
                return;
            }

            if (baseScene.isLoaded && forceReload)
            {
                var unloadOperation = SceneManager.UnloadSceneAsync(baseScene);
                if (unloadOperation != null)
                {
                    await AwaitAsyncOperation(unloadOperation);
                }
            }

            var loadOperation = SceneManager.LoadSceneAsync(BaseSceneName, LoadSceneMode.Additive);
            if (loadOperation != null)
            {
                await AwaitAsyncOperation(loadOperation);
            }
        }

        private static async Task AwaitAsyncOperation(AsyncOperation operation)
        {
            while (!operation.isDone)
            {
                await Task.Yield();
            }
        }

        private IEnumerator EnsureBaseSceneLoadedOnNextFrame()
        {
            _isQueuingBaseReload = true;
            yield return null;
            var loadTask = EnsureBaseSceneLoadedAsync();
            while (!loadTask.IsCompleted)
            {
                yield return null;
            }
        }

        private void QueueBaseSceneReload()
        {
            if (_isQueuingBaseReload)
            {
                return;
            }

            _isQueuingBaseReload = true;
            StartCoroutine(EnsureBaseSceneLoadedOnNextFrame());
        }

        private async Task UnloadBaseSceneIfLoadedAsync()
        {
            var baseScene = SceneManager.GetSceneByName(BaseSceneName);
            if (!baseScene.isLoaded)
            {
                return;
            }

            var unloadOperation = SceneManager.UnloadSceneAsync(baseScene);
            if (unloadOperation != null)
            {
                await AwaitAsyncOperation(unloadOperation);
            }
        }

        private LastRunSummary BuildLastRunSummary(bool wasSuccessful, string zoneId, List<LootItem> bankedItems, float totalBankedValue, float lostLootValue)
        {
            var countsById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < bankedItems.Count; i++)
            {
                var itemId = bankedItems[i].ItemId;
                countsById.TryGetValue(itemId, out var count);
                countsById[itemId] = count + 1;
            }

            var storedItems = new List<StoredLootItem>(countsById.Count);
            foreach (var pair in countsById)
            {
                storedItems.Add(new StoredLootItem
                {
                    ItemId = pair.Key,
                    Count = pair.Value,
                });
            }

            storedItems.Sort((left, right) => string.Compare(left.ItemId, right.ItemId, StringComparison.Ordinal));

            var durationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _runStartRealtimeSeconds);
            return new LastRunSummary
            {
                WasSuccessful = wasSuccessful,
                ZoneId = zoneId,
                ZoneDisplayName = CurrentRunContext?.ZoneDisplayName ?? zoneId,
                DurationSeconds = durationSeconds,
                TotalBankedValue = totalBankedValue,
                LostLootValue = lostLootValue,
                BankedItems = storedItems,
            };
        }

        private static float CalculateTotalValue(List<LootItem> bankedItems)
        {
            var total = 0f;
            for (var i = 0; i < bankedItems.Count; i++)
            {
                total += bankedItems[i].Value;
            }

            return total;
        }

        private void SetState(GameFlowState nextState)
        {
            State = nextState;
            StateChanged?.Invoke();
        }

        private void HookActivePlayerHealth()
        {
            var playerHealth = FindAnyObjectByType<PlayerHealth>();
            if (ReferenceEquals(_activePlayerHealth, playerHealth))
            {
                return;
            }

            UnsubscribeFromActivePlayerHealth();
            _activePlayerHealth = playerHealth;
            if (_activePlayerHealth != null)
            {
                _activePlayerHealth.OnPlayerDeath += HandlePlayerDeath;
            }
        }

        private void UnsubscribeFromActivePlayerHealth()
        {
            if (_activePlayerHealth == null)
            {
                return;
            }

            _activePlayerHealth.OnPlayerDeath -= HandlePlayerDeath;
            _activePlayerHealth = null;
        }

        private void HandlePlayerDeath(PlayerHealth playerHealth)
        {
            if (CurrentRunContext == null || _isTransitioning)
            {
                return;
            }

            var player = playerHealth.GetComponent<PlayerController>();
            var lostLootValue = player != null ? CalculateLostLootValue(player.CarryState.Items) : 0f;
            var presentation = playerHealth.GetComponent<IPlayerDeathPresentation>();
            var presentationDelay = presentation?.GetPresentationDurationSeconds() ?? 0f;
            presentation?.Play(lostLootValue);

            _ = ReturnToBaseAfterRunAsync(
                wasSuccessful: false,
                CurrentRunContext.ZoneId,
                new List<LootItem>(),
                totalBankedValue: 0f,
                lostLootValue,
                presentationDelay,
                clearCarryOnFinish: true);
        }

        private static float CalculateLostLootValue(IReadOnlyList<ILoadoutItem> items)
        {
            var total = 0f;
            for (var i = 0; i < items.Count; i++)
            {
                total += items[i].Value;
            }

            return total;
        }

#if UNITY_EDITOR
        public void EditorConfigure(ZoneLoader zoneLoader, LootDatabase lootDatabase)
        {
            _zoneLoader = zoneLoader;
            _lootDatabase = lootDatabase;
            ConfigureStashResolver();
        }
#endif
    }
}
