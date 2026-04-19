#nullable enable
using System.Collections.Generic;
using System.IO;
using ExtractionWeight.Core;
using UnityEditor;
using UnityEngine;

namespace ExtractionWeight.Loot.Editor
{
    public static class LootSeedDataUtility
    {
        private const string LootFolderPath = "Assets/_Project/Data/Loot";
        private const string IconFolderPath = "Assets/_Project/Data/Loot/Icons";

        [InitializeOnLoadMethod]
        private static void EnsureSeedDataExistsOnLoad()
        {
            EditorApplication.delayCall += EnsureSeedDataExists;
        }

        [MenuItem("Tools/Extraction Weight/Create Phase 1 Loot Seed Data")]
        public static void CreatePhaseOneLootSeedDataMenu()
        {
            CreatePhaseOneLootSeedData();
            EditorUtility.DisplayDialog("Loot Seed Data", "Phase 1 loot seed data created or refreshed.", "OK");
        }

        public static void CreatePhaseOneLootSeedData()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder(LootFolderPath);
            EnsureFolder(IconFolderPath);

            var definitions = new List<LootDefinition>
            {
                CreateOrUpdateDefinition("currency-small-bag", "Small Bag", LootCategory.Currency, new CostSignature(0.05f, 0.04f, 0.03f, 0.04f), 10f, false, new Vector3(0.16f, 0.12f, 0.05f), new Color(0.75f, 0.62f, 0.23f)),
                CreateOrUpdateDefinition("currency-cash-roll", "Cash Roll", LootCategory.Currency, new CostSignature(0.04f, 0.03f, 0.02f, 0.03f), 20f, false, new Vector3(0.10f, 0.05f, 0.05f), new Color(0.29f, 0.54f, 0.29f)),
                CreateOrUpdateDefinition("currency-data-chip", "Data Chip", LootCategory.Currency, new CostSignature(0.03f, 0.02f, 0.04f, 0.02f), 30f, false, new Vector3(0.08f, 0.05f, 0.01f), new Color(0.23f, 0.58f, 0.70f)),
                CreateOrUpdateDefinition("currency-coin-stack", "Coin Stack", LootCategory.Currency, new CostSignature(0.06f, 0.05f, 0.04f, 0.05f), 40f, false, new Vector3(0.09f, 0.09f, 0.04f), new Color(0.86f, 0.73f, 0.31f)),
                CreateOrUpdateDefinition("relic-small-sculpture", "Small Sculpture", LootCategory.Relic, new CostSignature(0.18f, 0.16f, 0.20f, 0.18f), 80f, false, new Vector3(0.28f, 0.22f, 0.20f), new Color(0.58f, 0.45f, 0.39f)),
                CreateOrUpdateDefinition("relic-medium-painting", "Medium Painting", LootCategory.Relic, new CostSignature(0.12f, 0.30f, 0.16f, 0.18f), 120f, false, new Vector3(0.64f, 0.48f, 0.06f), new Color(0.68f, 0.39f, 0.28f)),
                CreateOrUpdateDefinition("relic-large-machine", "Large Machine", LootCategory.Relic, new CostSignature(0.28f, 0.34f, 0.32f, 0.44f), 300f, false, new Vector3(0.90f, 0.70f, 0.65f), new Color(0.44f, 0.47f, 0.55f)),
                CreateOrUpdateDefinition("relic-old-console", "Old Console", LootCategory.Relic, new CostSignature(0.14f, 0.18f, 0.28f, 0.22f), 160f, false, new Vector3(0.50f, 0.18f, 0.28f), new Color(0.30f, 0.34f, 0.30f)),
                CreateOrUpdateDefinition("relic-preserved-specimen", "Preserved Specimen", LootCategory.Relic, new CostSignature(0.20f, 0.24f, 0.18f, 0.16f), 220f, false, new Vector3(0.34f, 0.52f, 0.24f), new Color(0.47f, 0.66f, 0.54f)),
                CreateOrUpdateDefinition("volatile-leaking-battery", "Leaking Battery", LootCategory.Volatile, new CostSignature(0.16f, 0.14f, 0.22f, 0.16f), 240f, true, new Vector3(0.20f, 0.16f, 0.12f), new Color(0.78f, 0.63f, 0.12f), new AmbientAxisEffect(CostAxis.Handling, 0.02f)),
                CreateOrUpdateDefinition("volatile-caged-bird", "Caged Bird", LootCategory.Volatile, new CostSignature(0.24f, 0.22f, 0.18f, 0.20f), 320f, true, new Vector3(0.40f, 0.34f, 0.34f), new Color(0.71f, 0.42f, 0.22f), new AmbientAxisEffect(CostAxis.Noise, 0.03f)),
                CreateOrUpdateDefinition("volatile-cracked-vial", "Cracked Vial", LootCategory.Volatile, new CostSignature(0.18f, 0.26f, 0.24f, 0.18f), 500f, true, new Vector3(0.10f, 0.24f, 0.10f), new Color(0.69f, 0.25f, 0.25f), new AmbientAxisEffect(CostAxis.Silhouette, 0.025f)),
            };

            var database = AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
            if (database is null)
            {
                database = ScriptableObject.CreateInstance<LootDatabase>();
                AssetDatabase.CreateAsset(database, LootDatabase.AssetPath);
            }

            database.EditorSetDefinitions(definitions);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        internal static void EnsureSeedDataExists()
        {
            if (AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath) is not null)
            {
                return;
            }

            CreatePhaseOneLootSeedData();
        }

        public static bool ValidateSeededLootData()
        {
            var database = AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
            if (database is null)
            {
                Debug.LogError("LootDatabase asset is missing.");
                return false;
            }

            var errors = database.Validate();
            if (errors.Count > 0)
            {
                for (var i = 0; i < errors.Count; i++)
                {
                    Debug.LogError(errors[i], database);
                }

                return false;
            }

            Debug.Log($"Loot seed validation passed with {database.Definitions.Count} definitions.");
            return true;
        }

        private static LootDefinition CreateOrUpdateDefinition(
            string itemId,
            string displayName,
            LootCategory category,
            CostSignature baseCost,
            float value,
            bool isVolatile,
            Vector3 physicalSize,
            Color color,
            AmbientAxisEffect ambientEffect = default)
        {
            var assetPath = $"{LootFolderPath}/{itemId}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(assetPath);
            if (definition is null)
            {
                definition = ScriptableObject.CreateInstance<LootDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var icon = LoadOrCreateIconSprite(itemId, color);
            definition.EditorSetData(itemId, displayName, icon, category, baseCost, value, isVolatile, physicalSize, null, null, ambientEffect);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static Sprite LoadOrCreateIconSprite(string iconName, Color color)
        {
            var assetPath = $"{IconFolderPath}/{iconName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (existing is not null)
            {
                return existing;
            }

            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var pixels = new Color[64 * 64];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();

            var bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            File.WriteAllBytes(assetPath, bytes);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer is not null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 64f;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath)!;
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
