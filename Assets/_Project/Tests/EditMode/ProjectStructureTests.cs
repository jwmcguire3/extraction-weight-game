#nullable enable

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ExtractionWeight.Tests.EditMode;

public class ProjectStructureTests
{
    [Test]
    public void ExpectedFoldersAndAssembliesExist()
    {
        var expectedAssemblies = new[]
        {
            "ExtractionWeight.Core",
            "ExtractionWeight.Weight",
            "ExtractionWeight.Loot",
            "ExtractionWeight.Zone",
            "ExtractionWeight.Threat",
            "ExtractionWeight.Extraction",
            "ExtractionWeight.MetaState",
            "ExtractionWeight.UI",
            "ExtractionWeight.Audio",
            "ExtractionWeight.Tests.EditMode",
            "ExtractionWeight.Tests.PlayMode"
        };

        var loadedAssemblyNames = AppDomain.CurrentDomain
            .GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var expectedAssembly in expectedAssemblies)
        {
            Assert.That(loadedAssemblyNames.Contains(expectedAssembly), Is.True, $"Missing assembly: {expectedAssembly}");
        }

        var expectedFolders = new[]
        {
            "Assets/_Project/Core",
            "Assets/_Project/Weight",
            "Assets/_Project/Loot",
            "Assets/_Project/Zone",
            "Assets/_Project/Threat",
            "Assets/_Project/Extraction",
            "Assets/_Project/MetaState",
            "Assets/_Project/UI",
            "Assets/_Project/Audio",
            "Assets/_Project/Data",
            "Assets/_Project/Scenes",
            "Assets/_Project/Tests",
            "Assets/_Project/Tests/EditMode",
            "Assets/_Project/Tests/PlayMode"
        };

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Could not resolve Unity project root.");

        foreach (var relativeFolder in expectedFolders)
        {
            var absolutePath = Path.Combine(projectRoot, relativeFolder);
            Assert.That(Directory.Exists(absolutePath), Is.True, $"Missing folder: {relativeFolder}");
        }
    }
}
