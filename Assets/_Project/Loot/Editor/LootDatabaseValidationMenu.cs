#nullable enable
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ExtractionWeight.Loot.Editor
{
    public static class LootDatabaseValidationMenu
    {
        [MenuItem("Tools/Extraction Weight/Validate Loot Database")]
        public static void ValidateLootDatabaseMenu()
        {
            ValidateLootDatabase();
        }

        public static bool ValidateLootDatabase()
        {
            var database = LoadDatabase();
            if (database is null)
            {
                EditorUtility.DisplayDialog("Loot Database Validation", "No LootDatabase asset was found at Assets/_Project/Data/Loot/LootDatabase.asset.", "OK");
                return false;
            }

            var errors = database.Validate();
            if (errors.Count == 0)
            {
                EditorUtility.DisplayDialog("Loot Database Validation", $"Loot database is valid. {database.Definitions.Count} definitions checked.", "OK");
                return true;
            }

            var message = string.Join("\n", errors.Take(12));
            if (errors.Count > 12)
            {
                message += $"\n...and {errors.Count - 12} more.";
            }

            EditorUtility.DisplayDialog("Loot Database Validation Failed", message, "OK");
            return false;
        }

        internal static LootDatabase? LoadDatabase()
        {
            return AssetDatabase.LoadAssetAtPath<LootDatabase>(LootDatabase.AssetPath);
        }
    }
}
