#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtractionWeight.Tools.Editor
{
    public static class BuildPipeline
    {
        private const string BuildRoot = "Builds/Phase1";

        [MenuItem("Tools/Extraction Weight/Build Phase 1 Android APK")]
        public static void BuildPhase1AndroidApk()
        {
            ApplyAndroidSettings();
            var outputPath = Path.Combine(BuildRoot, "Android", "ExtractionWeight-Phase1.apk");
            BuildPlayer(BuildTarget.Android, BuildTargetGroup.Android, outputPath, BuildOptions.None);
        }

        [MenuItem("Tools/Extraction Weight/Build Phase 1 iOS Xcode Project")]
        public static void BuildPhase1IosProject()
        {
            ApplyIosSettings();
            var outputPath = Path.Combine(BuildRoot, "iOS");
            BuildPlayer(BuildTarget.iOS, BuildTargetGroup.iOS, outputPath, BuildOptions.None);
        }

        private static void ApplyAndroidSettings()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            AddDefineSymbol(NamedBuildTarget.Android, "PHASE_1");
        }

        private static void ApplyIosSettings()
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            AddDefineSymbol(NamedBuildTarget.iOS, "PHASE_1");
        }

        private static void AddDefineSymbol(NamedBuildTarget buildTarget, string defineSymbol)
        {
            var existingDefines = PlayerSettings.GetScriptingDefineSymbols(buildTarget);
            var defineSet = existingDefines
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            if (defineSet.Add(defineSymbol))
            {
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, string.Join(';', defineSet));
            }
        }

        private static void BuildPlayer(BuildTarget buildTarget, BuildTargetGroup buildTargetGroup, string outputPath, BuildOptions options)
        {
            EnsureBuildTargetSupport(buildTargetGroup, buildTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? outputPath);
            var stopwatch = Stopwatch.StartNew();

            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = buildTarget,
                targetGroup = buildTargetGroup,
                locationPathName = outputPath,
                options = options,
            };

            var report = UnityEditor.BuildPipeline.BuildPlayer(buildPlayerOptions);
            stopwatch.Stop();

            var sizeBytes = TryGetOutputSizeBytes(outputPath);
            UnityEngine.Debug.Log(
                $"Phase 1 build finished for {buildTarget} in {stopwatch.Elapsed:c}. " +
                $"Result: {report.summary.result}. Output: {outputPath}. Size: {FormatSize(sizeBytes)}");

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Phase 1 {buildTarget} build failed: {report.summary.result}");
            }
        }

        private static void EnsureBuildTargetSupport(BuildTargetGroup buildTargetGroup, BuildTarget buildTarget)
        {
            if (UnityEditor.BuildPipeline.IsBuildTargetSupported(buildTargetGroup, buildTarget))
            {
                return;
            }

            throw new BuildFailedException(
                $"Build target {buildTarget} is not supported by this Unity install. " +
                "Install the matching platform build support module in Unity Hub before invoking the Phase 1 build menu.");
        }

        private static long TryGetOutputSizeBytes(string outputPath)
        {
            if (File.Exists(outputPath))
            {
                return new FileInfo(outputPath).Length;
            }

            if (!Directory.Exists(outputPath))
            {
                return 0L;
            }

            return Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)
                .Sum(filePath => new FileInfo(filePath).Length);
        }

        private static string FormatSize(long sizeBytes)
        {
            if (sizeBytes <= 0)
            {
                return "0 B";
            }

            var suffixes = new[] { "B", "KB", "MB", "GB" };
            double normalized = sizeBytes;
            var suffixIndex = 0;
            while (normalized >= 1024d && suffixIndex < suffixes.Length - 1)
            {
                normalized /= 1024d;
                suffixIndex++;
            }

            return $"{normalized:0.##} {suffixes[suffixIndex]}";
        }

        [PostProcessBuild(1000)]
        private static void DisableIosBitcode(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            var pbxProjectType = Type.GetType("UnityEditor.iOS.Xcode.PBXProject, UnityEditor.iOS.Extensions.Xcode");
            if (pbxProjectType == null)
            {
                UnityEngine.Debug.LogWarning("UnityEditor.iOS.Xcode is unavailable in this editor install; skipping ENABLE_BITCODE patch.");
                return;
            }

            var getProjectPathMethod = pbxProjectType.GetMethod("GetPBXProjectPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var projectPath = (string?)getProjectPathMethod?.Invoke(null, new object[] { pathToBuiltProject });
            if (string.IsNullOrWhiteSpace(projectPath))
            {
                return;
            }

            var project = Activator.CreateInstance(pbxProjectType);
            var readFromFileMethod = pbxProjectType.GetMethod("ReadFromFile");
            var setBuildPropertyMethod = pbxProjectType.GetMethod("SetBuildProperty");
            var getUnityMainTargetGuidMethod = pbxProjectType.GetMethod("GetUnityMainTargetGuid");
            var getUnityFrameworkTargetGuidMethod = pbxProjectType.GetMethod("GetUnityFrameworkTargetGuid");
            var writeToFileMethod = pbxProjectType.GetMethod("WriteToFile");
            if (project == null ||
                readFromFileMethod == null ||
                setBuildPropertyMethod == null ||
                getUnityMainTargetGuidMethod == null ||
                getUnityFrameworkTargetGuidMethod == null ||
                writeToFileMethod == null)
            {
                return;
            }

            readFromFileMethod.Invoke(project, new object[] { projectPath });
            var mainTargetGuid = (string?)getUnityMainTargetGuidMethod.Invoke(project, Array.Empty<object>());
            var frameworkTargetGuid = (string?)getUnityFrameworkTargetGuidMethod.Invoke(project, Array.Empty<object>());
            if (!string.IsNullOrWhiteSpace(mainTargetGuid))
            {
                setBuildPropertyMethod.Invoke(project, new object[] { mainTargetGuid!, "ENABLE_BITCODE", "NO" });
            }

            if (!string.IsNullOrWhiteSpace(frameworkTargetGuid))
            {
                setBuildPropertyMethod.Invoke(project, new object[] { frameworkTargetGuid!, "ENABLE_BITCODE", "NO" });
            }

            writeToFileMethod.Invoke(project, new object[] { projectPath });
        }
    }
}
