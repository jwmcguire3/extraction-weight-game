#nullable enable
using System.Collections.Generic;
using System.IO;
using ExtractionWeight.Loot;
using ExtractionWeight.Weight;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionWeight.Zone.Editor
{
    public static class ZoneSeedDataUtility
    {
        private const string DataFolderPath = "Assets/_Project/Data";
        private const string ZoneDataFolderPath = "Assets/_Project/Data/Zones";
        private const string ZonePrefabFolderPath = "Assets/_Project/Data/Zones/Prefabs";
        private const string ZoneMaterialFolderPath = "Assets/_Project/Data/Zones/Materials";
        private const string ScenesFolderPath = "Assets/_Project/Scenes";
        private const string ZoneScenesFolderPath = "Assets/_Project/Scenes/Zones";
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string DrydockScenePath = "Assets/_Project/Scenes/Zones/Drydock.unity";
        private const string DrydockDefinitionPath = "Assets/_Project/Data/Zones/DrydockZoneDefinition.asset";
        private const string ExtractionMarkerMaterialPath = "Assets/_Project/Data/Zones/Materials/ExtractionPointMarker.mat";
        private const string ExtractionMarkerPrefabPath = "Assets/_Project/Data/Zones/Prefabs/ExtractionPointMarker.prefab";
        private const string DrydockSceneAddress = "Zones/Drydock";

        [InitializeOnLoadMethod]
        private static void EnsureSeedDataExistsOnLoad()
        {
            EditorApplication.delayCall += EnsureSeedDataExists;
        }

        [MenuItem("Tools/Extraction Weight/Create Drydock Zone Seed Data")]
        public static void CreateDrydockZoneSeedDataMenu()
        {
            CreateOrUpdateDrydockZoneSeedData();
            EditorUtility.DisplayDialog("Zone Seed Data", "Drydock zone seed data created or refreshed.", "OK");
        }

        public static void CreateOrUpdateDrydockZoneSeedData()
        {
            EnsureFolder(DataFolderPath);
            EnsureFolder(ZoneDataFolderPath);
            EnsureFolder(ZonePrefabFolderPath);
            EnsureFolder(ZoneMaterialFolderPath);
            EnsureFolder(ScenesFolderPath);
            EnsureFolder(ZoneScenesFolderPath);

            var previousScene = EditorSceneManager.GetActiveScene().path;
            var markerPrefab = CreateOrUpdateExtractionMarkerPrefab();
            CreateOrUpdateDrydockScene();
            var definition = CreateOrUpdateDrydockDefinition();
            ConfigureAddressables();
            ConfigureBootScene(definition, markerPrefab);
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (!string.IsNullOrWhiteSpace(previousScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScene) != null)
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
            else
            {
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            }
        }

        public static void EnsureSeedDataExists()
        {
            if (AssetDatabase.LoadAssetAtPath<ZoneDefinition>(DrydockDefinitionPath) is not null &&
                AssetDatabase.LoadAssetAtPath<GameObject>(ExtractionMarkerPrefabPath) is not null &&
                AssetDatabase.LoadAssetAtPath<SceneAsset>(DrydockScenePath) != null)
            {
                ConfigureAddressables();
                ConfigureBootScene(
                    AssetDatabase.LoadAssetAtPath<ZoneDefinition>(DrydockDefinitionPath)!,
                    AssetDatabase.LoadAssetAtPath<GameObject>(ExtractionMarkerPrefabPath)!);
                ConfigureBuildSettings();
                return;
            }

            CreateOrUpdateDrydockZoneSeedData();
        }

        public static void BuildDrydockZoneContent()
        {
            CreateOrUpdateDrydockZoneSeedData();
        }

        private static GameObject CreateOrUpdateExtractionMarkerPrefab()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(ExtractionMarkerMaterialPath);
            if (material is null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    color = new Color(0.82f, 0.16f, 0.12f, 1f),
                };

                AssetDatabase.CreateAsset(material, ExtractionMarkerMaterialPath);
            }

            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ExtractionMarkerPrefabPath);
            var instance = prefabAsset is null
                ? GameObject.CreatePrimitive(PrimitiveType.Capsule)
                : PrefabUtility.InstantiatePrefab(prefabAsset) as GameObject ?? GameObject.CreatePrimitive(PrimitiveType.Capsule);

            instance.name = "ExtractionPointMarker";
            instance.transform.localScale = new Vector3(1.5f, 3f, 1.5f);

            var renderer = instance.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (instance.GetComponent<Collider>() is CapsuleCollider collider)
            {
                collider.isTrigger = true;
            }

            if (instance.GetComponent<ZoneExtractionPointMarker>() == null)
            {
                instance.AddComponent<ZoneExtractionPointMarker>();
            }

            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(instance, ExtractionMarkerPrefabPath);
            Object.DestroyImmediate(instance);
            return savedPrefab;
        }

        private static void CreateOrUpdateDrydockScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "DrydockPlane";
            plane.transform.position = Vector3.zero;
            plane.transform.localScale = new Vector3(20f, 1f, 20f);

            var planeRenderer = plane.GetComponent<MeshRenderer>();
            if (planeRenderer != null)
            {
                var planeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"))
                {
                    color = new Color(0.35f, 0.38f, 0.42f, 1f),
                };
                planeRenderer.sharedMaterial = planeMaterial;
            }

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            CreateExtractionPointAnchor("ExtractionPoint_A", new Vector3(-95f, 0f, -95f));
            CreateExtractionPointAnchor("ExtractionPoint_B", new Vector3(95f, 0f, -95f));
            CreateExtractionPointAnchor("ExtractionPoint_C", new Vector3(-95f, 0f, 95f));
            CreateExtractionPointAnchor("ExtractionPoint_D", new Vector3(95f, 0f, 95f));

            EditorSceneManager.SaveScene(scene, DrydockScenePath);
        }

        private static ZoneDefinition CreateOrUpdateDrydockDefinition()
        {
            var zoneDefinition = AssetDatabase.LoadAssetAtPath<ZoneDefinition>(DrydockDefinitionPath);
            if (zoneDefinition is null)
            {
                zoneDefinition = ScriptableObject.CreateInstance<ZoneDefinition>();
                AssetDatabase.CreateAsset(zoneDefinition, DrydockDefinitionPath);
            }

            zoneDefinition.EditorSetData(
                "drydock",
                "Drydock",
                DrydockSceneAddress,
                new ZoneAxisWeights(0.30f, 0.20f, 0.15f, 0.35f),
                600f,
                CreateExtractionPoints(),
                CreateLootSpawnRegions(),
                CreateStubPatrolRoutes(),
                DrydockScenePath);

            EditorUtility.SetDirty(zoneDefinition);
            return zoneDefinition;
        }

        private static void ConfigureAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings is null)
            {
                settings = AddressableAssetSettings.Create(AddressableAssetSettingsDefaultObject.kDefaultConfigFolder, "AddressableAssetSettings", true, true);
            }

            settings.ActivePlayModeDataBuilderIndex = 0;
            ProjectConfigData.ActivePlayModeIndex = 0;

            var group = settings.FindGroup("Zones");
            if (group is null)
            {
                group = settings.CreateGroup("Zones", false, false, false, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            }

            var sceneGuid = AssetDatabase.AssetPathToGUID(DrydockScenePath);
            var entry = settings.CreateOrMoveEntry(sceneGuid, group);
            entry.address = DrydockSceneAddress;
            entry.SetLabel("ZoneScene", true, true);

            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureBootScene(ZoneDefinition definition, GameObject markerPrefab)
        {
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

            loader.EditorConfigure(new List<ZoneDefinition> { definition }, markerPrefab);
            EditorUtility.SetDirty(loader);
            EditorSceneManager.SaveScene(scene);
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(DrydockScenePath, true),
            };
        }

        private static List<ExtractionPointData> CreateExtractionPoints()
        {
            return new List<ExtractionPointData>
            {
                new("A", ExtractionType.Overland, new Vector3(-95f, 1.5f, -95f), 150f, ItemSizeFilter.AcceptsAll, 0f),
                new("B", ExtractionType.Standard, new Vector3(95f, 1.5f, -95f), 360f, ItemSizeFilter.AcceptsAll, 90f),
                new("C", ExtractionType.Drone, new Vector3(-95f, 1.5f, 95f), 300f, ItemSizeFilter.SmallOnly, 30f),
                new("D", ExtractionType.Standard, new Vector3(95f, 1.5f, 95f), 480f, ItemSizeFilter.MediumAndSmaller, 90f),
            };
        }

        private static List<LootSpawnRegion> CreateLootSpawnRegions()
        {
            return new List<LootSpawnRegion>
            {
                new(new Vector3(-72f, 0f, -48f), 18f, new[] { LootCategory.Currency, LootCategory.Tool }, 1, 3, 0.0f),
                new(new Vector3(70f, 0f, -42f), 16f, new[] { LootCategory.Currency, LootCategory.Commodity }, 1, 3, 0.1f),
                new(new Vector3(-24f, 0f, -6f), 14f, new[] { LootCategory.Currency, LootCategory.Relic }, 2, 4, 0.45f),
                new(new Vector3(28f, 0f, 12f), 15f, new[] { LootCategory.Relic, LootCategory.Tool }, 2, 4, 0.55f),
                new(new Vector3(-10f, 0f, 62f), 12f, new[] { LootCategory.Relic, LootCategory.Volatile }, 2, 5, 0.85f),
                new(new Vector3(52f, 0f, 58f), 11f, new[] { LootCategory.Objective, LootCategory.Volatile, LootCategory.Relic }, 1, 3, 1.0f),
            };
        }

        private static List<ThreatPatrolRoute> CreateStubPatrolRoutes()
        {
            return new List<ThreatPatrolRoute>
            {
                new("perimeter-west", new List<Vector3>()),
                new("perimeter-east", new List<Vector3>()),
            };
        }

        private static void CreateExtractionPointAnchor(string name, Vector3 position)
        {
            var anchor = new GameObject(name);
            anchor.transform.position = position;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(folderName))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
