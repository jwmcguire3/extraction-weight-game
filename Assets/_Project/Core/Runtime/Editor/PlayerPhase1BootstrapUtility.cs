#nullable enable
using System.IO;
using Cinemachine;
using ExtractionWeight.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ExtractionWeight.Core.Editor
{
    public static class PlayerPhase1BootstrapUtility
    {
        private const string PlayerPrefabPath = "Assets/_Project/Data/Player/Prefabs/Phase1Player.prefab";
        private const string PlayerFolderPath = "Assets/_Project/Data/Player";
        private const string PlayerPrefabFolderPath = "Assets/_Project/Data/Player/Prefabs";
        private const string DrydockScenePath = "Assets/_Project/Scenes/Zones/Drydock.unity";
        private const string InputActionsPath = "Assets/_Project/Core/Input/PlayerControls.inputactions";
        private const string PlayerInstanceName = "Phase1Player";

        [MenuItem("Tools/Extraction Weight/Build Phase 1 Player Content")]
        public static void BuildPhase1PlayerContent()
        {
            EnsureFolders();

            var prefab = CreateOrUpdatePlayerPrefab();
            WireDrydockScene(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorSceneManager.SaveOpenScenes();

            Debug.Log("Phase 1 player content generated.");
        }

        public static void BuildPhase1PlayerContentFromBatchMode()
        {
            BuildPhase1PlayerContent();
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(PlayerFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Player");
            }

            if (!AssetDatabase.IsValidFolder(PlayerPrefabFolderPath))
            {
                AssetDatabase.CreateFolder(PlayerFolderPath, "Prefabs");
            }
        }

        private static GameObject CreateOrUpdatePlayerPrefab()
        {
            var playerRoot = new GameObject(PlayerInstanceName);

            try
            {
                var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
                if (inputAsset == null)
                {
                    throw new FileNotFoundException($"Input actions asset was not found at {InputActionsPath}.");
                }

                var characterController = playerRoot.AddComponent<CharacterController>();
                characterController.height = 1.8f;
                characterController.center = new Vector3(0f, 0.9f, 0f);
                characterController.radius = 0.35f;
                characterController.minMoveDistance = 0f;
                characterController.skinWidth = 0.05f;

                playerRoot.AddComponent<InteractionTracker>();
                var playerController = playerRoot.AddComponent<PlayerController>();
                var carryFeedbackController = playerRoot.AddComponent<CarryFeedbackController>();
                var footstepLightSource = playerRoot.AddComponent<AudioSource>();
                var footstepLoadedSource = playerRoot.AddComponent<AudioSource>();
                var footstepOverburdenedSource = playerRoot.AddComponent<AudioSource>();
                var breathSource = playerRoot.AddComponent<AudioSource>();
                var ambientItemSource = playerRoot.AddComponent<AudioSource>();

                ConfigureAudioSource(footstepLightSource);
                ConfigureAudioSource(footstepLoadedSource);
                ConfigureAudioSource(footstepOverburdenedSource);
                ConfigureAudioSource(breathSource);
                ConfigureAudioSource(ambientItemSource);

                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "Visual";
                visual.transform.SetParent(playerRoot.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                var capsuleCollider = visual.GetComponent<CapsuleCollider>();
                if (capsuleCollider != null)
                {
                    Object.DestroyImmediate(capsuleCollider);
                }

                var cameraRoot = new GameObject("CameraRoot").transform;
                cameraRoot.SetParent(playerRoot.transform, false);
                cameraRoot.localPosition = new Vector3(0f, 1.65f, 0f);

                var mainCameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
                mainCameraObject.tag = "MainCamera";
                mainCameraObject.transform.SetParent(playerRoot.transform, false);
                mainCameraObject.transform.localPosition = Vector3.zero;
                mainCameraObject.transform.localRotation = Quaternion.identity;

                var virtualCameraObject = new GameObject("FirstPersonVirtualCamera", typeof(CinemachineVirtualCamera));
                virtualCameraObject.transform.SetParent(cameraRoot, false);
                virtualCameraObject.transform.localPosition = Vector3.zero;
                virtualCameraObject.transform.localRotation = Quaternion.identity;
                var virtualCamera = virtualCameraObject.GetComponent<CinemachineVirtualCamera>();
                virtualCamera.Priority = 20;
                virtualCamera.m_Lens.FieldOfView = 65f;
                virtualCamera.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

                var hudObject = new GameObject("MobileHUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(MobileUIHUD));
                hudObject.transform.SetParent(playerRoot.transform, false);
                var mobileHud = hudObject.GetComponent<MobileUIHUD>();

                playerController.EditorConfigure(characterController, inputAsset, playerRoot.transform);
                carryFeedbackController.EditorConfigure(
                    playerController,
                    cameraRoot,
                    virtualCamera,
                    footstepLightSource,
                    footstepLoadedSource,
                    footstepOverburdenedSource,
                    breathSource,
                    ambientItemSource);
                mobileHud.EditorConfigure(playerController);

                return PrefabUtility.SaveAsPrefabAsset(playerRoot, PlayerPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(playerRoot);
            }
        }

        private static void WireDrydockScene(GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(DrydockScenePath, OpenSceneMode.Single);
            GameObject? existingPlayer = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != PlayerInstanceName)
                {
                    continue;
                }

                existingPlayer = root;
                break;
            }

            if (existingPlayer != null)
            {
                Object.DestroyImmediate(existingPlayer);
            }

            var playerInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            playerInstance.name = PlayerInstanceName;
            playerInstance.transform.position = new Vector3(0f, 0.05f, 0f);
            playerInstance.transform.rotation = Quaternion.identity;

            foreach (var root in scene.GetRootGameObjects())
            {
                if (!root.name.StartsWith("ExtractionPoint_"))
                {
                    continue;
                }

                var trigger = root.GetComponent<SphereCollider>();
                if (trigger == null)
                {
                    trigger = root.AddComponent<SphereCollider>();
                }

                trigger.isTrigger = true;
                trigger.radius = 3f;

                var target = root.GetComponent<PlayerContextActionTarget>();
                if (target == null)
                {
                    target = root.AddComponent<PlayerContextActionTarget>();
                }

                target.EditorConfigure(ContextActionKind.Extract, "Extract", 3f, 5);
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void ConfigureAudioSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.dopplerLevel = 0f;
        }
    }
}
