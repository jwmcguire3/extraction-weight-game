#nullable enable
using ExtractionWeight.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ExtractionWeight.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(CanvasScaler))]
    [RequireComponent(typeof(GraphicRaycaster))]
    public sealed class MobileUIHUD : MonoBehaviour
    {
        [SerializeField]
        private PlayerController? _playerController;

        [SerializeField]
        private Color _lightColor = new(0.31f, 0.81f, 0.45f, 0.95f);

        [SerializeField]
        private Color _loadedColor = new(0.95f, 0.82f, 0.24f, 0.95f);

        [SerializeField]
        private Color _overburdenedColor = new(0.93f, 0.52f, 0.18f, 0.95f);

        [SerializeField]
        private Color _softCeilingColor = new(0.85f, 0.2f, 0.2f, 0.95f);

        [SerializeField]
        private Color _disabledActionColor = new(0.35f, 0.35f, 0.35f, 0.75f);

        private Canvas? _canvas;
        private Font? _font;
        private Image? _carryGaugeFill;
        private Text? _carryGaugeText;
        private Image? _staminaFill;
        private Text? _staminaText;
        private Text? _tideTimerText;
        private Image? _actionButtonImage;
        private Text? _actionButtonText;
        private Image? _crouchButtonImage;
        private Text? _crouchButtonText;
        private static Sprite? s_fallbackSprite;

        private void Awake()
        {
            _playerController ??= FindAnyObjectByType<PlayerController>();
            _canvas = GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();
            EnsureLayout();
        }

        private void Update()
        {
            if (_playerController == null)
            {
                return;
            }

            var carryFraction = _playerController.CapacityFraction;
            if (_carryGaugeFill != null)
            {
                _carryGaugeFill.fillAmount = Mathf.Clamp01(carryFraction / PlayerController.MaxCapacityFraction);
                _carryGaugeFill.color = GetCarryColor(_playerController.CurrentBreakpointIndex);
            }

            if (_carryGaugeText != null)
            {
                _carryGaugeText.text = $"{Mathf.RoundToInt(carryFraction * 100f)}%";
            }

            if (_staminaFill != null)
            {
                _staminaFill.fillAmount = _playerController.CurrentStamina / _playerController.MaxStamina;
            }

            if (_staminaText != null)
            {
                _staminaText.text = $"STA {Mathf.CeilToInt(_playerController.CurrentStamina)}";
            }

            if (_tideTimerText != null)
            {
                _tideTimerText.text = $"{Mathf.CeilToInt(_playerController.CurrentTideSecondsRemaining)}s";
            }

            if (_actionButtonText != null)
            {
                _actionButtonText.text = _playerController.CurrentContextActionKind == ContextActionKind.None
                    ? "..."
                    : _playerController.CurrentContextActionLabel;
            }

            if (_actionButtonImage != null)
            {
                _actionButtonImage.color = _playerController.CurrentContextActionKind == ContextActionKind.None
                    ? _disabledActionColor
                    : GetCarryColor(_playerController.CurrentBreakpointIndex);
            }

            if (_crouchButtonImage != null)
            {
                _crouchButtonImage.color = _playerController.IsCrouched
                    ? _loadedColor
                    : new Color(0.14f, 0.16f, 0.18f, 0.8f);
            }

            if (_crouchButtonText != null)
            {
                _crouchButtonText.text = _playerController.IsCrouched ? "Crouched" : "Crouch";
            }
        }

        private void EnsureLayout()
        {
            var rootRect = transform as RectTransform;
            if (rootRect == null)
            {
                return;
            }

            if (transform.childCount > 0)
            {
                CacheExistingReferences();
                return;
            }

            var builtinSprite = LoadBuiltinSprite("UI/Skin/UISprite.psd");
            var knobSprite = LoadBuiltinSprite("UI/Skin/Knob.psd");

            var gaugeRoot = CreateRect("TopCenter", rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(180f, 140f));
            var gaugeBack = CreateImage("CarryGaugeBack", gaugeRoot, builtinSprite, new Color(0f, 0f, 0f, 0.4f));
            gaugeBack.type = Image.Type.Filled;
            gaugeBack.fillMethod = Image.FillMethod.Radial360;
            gaugeBack.fillAmount = 1f;
            gaugeBack.rectTransform.sizeDelta = new Vector2(132f, 132f);

            _carryGaugeFill = CreateImage("CarryGaugeFill", gaugeRoot, builtinSprite, _lightColor);
            _carryGaugeFill.type = Image.Type.Filled;
            _carryGaugeFill.fillMethod = Image.FillMethod.Radial360;
            _carryGaugeFill.fillOrigin = (int)Image.Origin360.Top;
            _carryGaugeFill.fillClockwise = false;
            _carryGaugeFill.rectTransform.sizeDelta = new Vector2(116f, 116f);

            var staminaBack = CreateImage("StaminaBack", gaugeRoot, builtinSprite, new Color(0f, 0f, 0f, 0.45f));
            staminaBack.type = Image.Type.Sliced;
            staminaBack.rectTransform.anchoredPosition = new Vector2(0f, -80f);
            staminaBack.rectTransform.sizeDelta = new Vector2(180f, 18f);

            _staminaFill = CreateImage("StaminaFill", staminaBack.rectTransform, builtinSprite, new Color(0.27f, 0.73f, 0.94f, 0.95f));
            _staminaFill.type = Image.Type.Filled;
            _staminaFill.fillMethod = Image.FillMethod.Horizontal;
            _staminaFill.rectTransform.anchorMin = Vector2.zero;
            _staminaFill.rectTransform.anchorMax = Vector2.one;
            _staminaFill.rectTransform.offsetMin = new Vector2(2f, 2f);
            _staminaFill.rectTransform.offsetMax = new Vector2(-2f, -2f);

            _carryGaugeText = CreateText("CarryGaugeText", gaugeRoot, new Vector2(0f, 2f), 26, TextAnchor.MiddleCenter, "0%");
            _staminaText = CreateText("StaminaText", gaugeRoot, new Vector2(0f, -104f), 16, TextAnchor.MiddleCenter, "STA 100");
            _tideTimerText = CreateText("TideTimer", rootRect, new Vector2(-90f, -52f), 22, TextAnchor.MiddleRight, "150s");
            _tideTimerText.rectTransform.anchorMin = new Vector2(1f, 1f);
            _tideTimerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tideTimerText.rectTransform.sizeDelta = new Vector2(180f, 40f);

            var joystickArea = CreateRect("JoystickArea", rootRect, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(128f, 128f), new Vector2(220f, 220f));
            var joystickBack = CreateImage("JoystickBack", joystickArea, builtinSprite, new Color(0.08f, 0.09f, 0.11f, 0.45f));
            joystickBack.rectTransform.sizeDelta = new Vector2(180f, 180f);
            joystickBack.raycastTarget = true;

            var joystickStick = CreateImage("JoystickStick", joystickArea, knobSprite != null ? knobSprite : builtinSprite, new Color(0.92f, 0.95f, 1f, 0.75f));
            joystickStick.rectTransform.sizeDelta = new Vector2(96f, 96f);
            var onScreenStick = joystickStick.gameObject.AddComponent<OnScreenStick>();
            onScreenStick.controlPath = "<Gamepad>/leftStick";
            onScreenStick.movementRange = 60f;
            onScreenStick.useIsolatedInputActions = true;

            var actionButtonRoot = CreateRect("ActionButton", rootRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-128f, 128f), new Vector2(170f, 170f));
            _actionButtonImage = CreateImage("ActionButtonImage", actionButtonRoot, builtinSprite, _disabledActionColor);
            _actionButtonImage.rectTransform.sizeDelta = new Vector2(150f, 150f);
            _actionButtonImage.raycastTarget = true;
            var actionButton = _actionButtonImage.gameObject.AddComponent<OnScreenButton>();
            actionButton.controlPath = "<Gamepad>/buttonSouth";
            _actionButtonText = CreateText("ActionButtonText", actionButtonRoot, Vector2.zero, 24, TextAnchor.MiddleCenter, "...");

            var crouchButtonRoot = CreateRect("CrouchButton", rootRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-292f, 88f), new Vector2(124f, 124f));
            _crouchButtonImage = CreateImage("CrouchButtonImage", crouchButtonRoot, builtinSprite, new Color(0.14f, 0.16f, 0.18f, 0.8f));
            _crouchButtonImage.rectTransform.sizeDelta = new Vector2(116f, 116f);
            _crouchButtonImage.raycastTarget = true;
            var crouchButton = _crouchButtonImage.gameObject.AddComponent<OnScreenButton>();
            crouchButton.controlPath = "<Gamepad>/buttonEast";
            _crouchButtonText = CreateText("CrouchButtonText", crouchButtonRoot, Vector2.zero, 18, TextAnchor.MiddleCenter, "Crouch");
        }

        private void CacheExistingReferences()
        {
            _carryGaugeFill = transform.Find("TopCenter/CarryGaugeFill")?.GetComponent<Image>();
            _carryGaugeText = transform.Find("TopCenter/CarryGaugeText")?.GetComponent<Text>();
            _staminaFill = transform.Find("TopCenter/StaminaBack/StaminaFill")?.GetComponent<Image>();
            _staminaText = transform.Find("TopCenter/StaminaText")?.GetComponent<Text>();
            _tideTimerText = transform.Find("TideTimer")?.GetComponent<Text>();
            _actionButtonImage = transform.Find("ActionButton/ActionButtonImage")?.GetComponent<Image>();
            _actionButtonText = transform.Find("ActionButton/ActionButtonText")?.GetComponent<Text>();
            _crouchButtonImage = transform.Find("CrouchButton/CrouchButtonImage")?.GetComponent<Image>();
            _crouchButtonText = transform.Find("CrouchButton/CrouchButtonText")?.GetComponent<Text>();
        }

        private Color GetCarryColor(int breakpointIndex)
        {
            return breakpointIndex switch
            {
                0 => _lightColor,
                1 => _loadedColor,
                2 => _overburdenedColor,
                _ => _softCeilingColor,
            };
        }

        private static RectTransform CreateRect(
            string name,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            return rect;
        }

        private Image CreateImage(string name, RectTransform parent, Sprite? sprite, Color color)
        {
            var image = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, parent.sizeDelta).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private Text CreateText(string name, RectTransform parent, Vector2 anchoredPosition, int fontSize, TextAnchor alignment, string value)
        {
            var text = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPosition, new Vector2(180f, 36f)).gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = Color.white;
            text.text = value;
            return text;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(eventSystemObject);
        }

        private static Sprite LoadBuiltinSprite(string resourcePath)
        {
            if (s_fallbackSprite != null)
            {
                return s_fallbackSprite;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "HUD_FallbackSprite",
            };
            texture.SetPixels(new[]
            {
                Color.white, Color.white,
                Color.white, Color.white,
            });
            texture.Apply();

            s_fallbackSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            return s_fallbackSprite;
        }

#if UNITY_EDITOR
        public void EditorConfigure(PlayerController playerController)
        {
            _playerController = playerController;
        }
#endif
    }
}
