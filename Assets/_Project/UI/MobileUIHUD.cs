#nullable enable
using System.Collections.Generic;
using ExtractionWeight.Core;
using ExtractionWeight.Zone;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
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
        private Text? _openExitsText;
        private Image? _tideBarFill;
        private Image? _tideBarBackdrop;
        private Image? _actionButtonImage;
        private Image? _actionButtonFillRing;
        private Text? _actionButtonText;
        private Image? _crouchButtonImage;
        private Text? _crouchButtonText;
        private Text? _hudMessageText;
        private Image? _deathFadeImage;
        private Text? _deathTitleText;
        private Text? _deathValueText;
        private readonly List<Image> _tideMarkers = new();
        private static Sprite? s_fallbackSprite;
        private ZoneRuntime? _zoneRuntime;
        private TideController? _tideController;

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

            _zoneRuntime ??= FindAnyObjectByType<ZoneRuntime>();
            _tideController ??= FindAnyObjectByType<TideController>();
            if (_openExitsText != null)
            {
                _openExitsText.text = _zoneRuntime?.OpenExtractionSummary ?? string.Empty;
            }

            UpdateTideBar();

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

            if (_actionButtonFillRing != null)
            {
                _actionButtonFillRing.fillAmount = _playerController.CurrentContextActionProgress;
                _actionButtonFillRing.color = GetCarryColor(_playerController.CurrentBreakpointIndex);
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

            if (_hudMessageText != null)
            {
                _hudMessageText.text = _playerController.CurrentHudMessage;
                _hudMessageText.enabled = !string.IsNullOrWhiteSpace(_playerController.CurrentHudMessage);
            }
        }

        public void SetDeathOverlay(Color fadeColor, float lostLootValue)
        {
            if (_deathFadeImage == null || _deathTitleText == null || _deathValueText == null)
            {
                return;
            }

            _deathFadeImage.enabled = fadeColor.a > 0f;
            _deathFadeImage.color = fadeColor;
            _deathTitleText.enabled = fadeColor.a > 0.01f;
            _deathValueText.enabled = fadeColor.a > 0.01f;
            _deathTitleText.text = "You died";
            _deathValueText.text = $"Lost Loot: ${lostLootValue:0}";
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
            CreateTideBar(rootRect, builtinSprite);
            _tideTimerText = CreateText("TideTimer", rootRect, new Vector2(-90f, -52f), 22, TextAnchor.MiddleRight, "150s");
            _tideTimerText.rectTransform.anchorMin = new Vector2(1f, 1f);
            _tideTimerText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _tideTimerText.rectTransform.sizeDelta = new Vector2(180f, 40f);
            _openExitsText = CreateText("OpenExitsText", rootRect, new Vector2(0f, -52f), 18, TextAnchor.MiddleCenter, "Open exits: A B C D");
            _openExitsText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _openExitsText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _openExitsText.rectTransform.sizeDelta = new Vector2(360f, 32f);

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
            _actionButtonFillRing = CreateImage("ActionButtonFillRing", actionButtonRoot, builtinSprite, _loadedColor);
            _actionButtonFillRing.type = Image.Type.Filled;
            _actionButtonFillRing.fillMethod = Image.FillMethod.Radial360;
            _actionButtonFillRing.fillOrigin = (int)Image.Origin360.Top;
            _actionButtonFillRing.fillClockwise = false;
            _actionButtonFillRing.rectTransform.sizeDelta = new Vector2(164f, 164f);
            _actionButtonFillRing.fillAmount = 0f;
            _actionButtonFillRing.raycastTarget = false;
            _actionButtonImage = CreateImage("ActionButtonImage", actionButtonRoot, builtinSprite, _disabledActionColor);
            _actionButtonImage.rectTransform.sizeDelta = new Vector2(150f, 150f);
            _actionButtonImage.raycastTarget = true;
            var actionButton = _actionButtonImage.gameObject.AddComponent<OnScreenButton>();
            actionButton.controlPath = "<Gamepad>/buttonSouth";
            _actionButtonText = CreateText("ActionButtonText", actionButtonRoot, Vector2.zero, 24, TextAnchor.MiddleCenter, "...");
            _hudMessageText = CreateText("HudMessageText", rootRect, new Vector2(0f, 120f), 28, TextAnchor.MiddleCenter, string.Empty);
            _hudMessageText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _hudMessageText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _hudMessageText.rectTransform.sizeDelta = new Vector2(420f, 40f);
            _hudMessageText.enabled = false;

            var crouchButtonRoot = CreateRect("CrouchButton", rootRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-292f, 88f), new Vector2(124f, 124f));
            _crouchButtonImage = CreateImage("CrouchButtonImage", crouchButtonRoot, builtinSprite, new Color(0.14f, 0.16f, 0.18f, 0.8f));
            _crouchButtonImage.rectTransform.sizeDelta = new Vector2(116f, 116f);
            _crouchButtonImage.raycastTarget = true;
            var crouchButton = _crouchButtonImage.gameObject.AddComponent<OnScreenButton>();
            crouchButton.controlPath = "<Gamepad>/buttonEast";
            _crouchButtonText = CreateText("CrouchButtonText", crouchButtonRoot, Vector2.zero, 18, TextAnchor.MiddleCenter, "Crouch");

            _deathFadeImage = CreateImage("DeathFade", rootRect, builtinSprite, new Color(0f, 0f, 0f, 0f));
            _deathFadeImage.rectTransform.anchorMin = Vector2.zero;
            _deathFadeImage.rectTransform.anchorMax = Vector2.one;
            _deathFadeImage.rectTransform.offsetMin = Vector2.zero;
            _deathFadeImage.rectTransform.offsetMax = Vector2.zero;
            _deathFadeImage.enabled = false;
            _deathFadeImage.transform.SetAsLastSibling();

            _deathTitleText = CreateText("DeathTitleText", rootRect, new Vector2(0f, 32f), 54, TextAnchor.MiddleCenter, "You died");
            _deathTitleText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _deathTitleText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _deathTitleText.rectTransform.sizeDelta = new Vector2(520f, 72f);
            _deathTitleText.enabled = false;
            _deathTitleText.transform.SetAsLastSibling();

            _deathValueText = CreateText("DeathValueText", rootRect, new Vector2(0f, -28f), 30, TextAnchor.MiddleCenter, string.Empty);
            _deathValueText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _deathValueText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _deathValueText.rectTransform.sizeDelta = new Vector2(520f, 48f);
            _deathValueText.enabled = false;
            _deathValueText.transform.SetAsLastSibling();
        }

        private void CacheExistingReferences()
        {
            _carryGaugeFill = transform.Find("TopCenter/CarryGaugeFill")?.GetComponent<Image>();
            _carryGaugeText = transform.Find("TopCenter/CarryGaugeText")?.GetComponent<Text>();
            _staminaFill = transform.Find("TopCenter/StaminaBack/StaminaFill")?.GetComponent<Image>();
            _staminaText = transform.Find("TopCenter/StaminaText")?.GetComponent<Text>();
            _tideBarBackdrop = transform.Find("TideBar/TideBarBackdrop")?.GetComponent<Image>();
            _tideBarFill = transform.Find("TideBar/TideBarBackdrop/TideBarFill")?.GetComponent<Image>();
            _tideTimerText = transform.Find("TideTimer")?.GetComponent<Text>();
            _openExitsText = transform.Find("OpenExitsText")?.GetComponent<Text>();
            _actionButtonImage = transform.Find("ActionButton/ActionButtonImage")?.GetComponent<Image>();
            _actionButtonFillRing = transform.Find("ActionButton/ActionButtonFillRing")?.GetComponent<Image>();
            _actionButtonText = transform.Find("ActionButton/ActionButtonText")?.GetComponent<Text>();
            _crouchButtonImage = transform.Find("CrouchButton/CrouchButtonImage")?.GetComponent<Image>();
            _crouchButtonText = transform.Find("CrouchButton/CrouchButtonText")?.GetComponent<Text>();
            _hudMessageText = transform.Find("HudMessageText")?.GetComponent<Text>();
            _deathFadeImage = transform.Find("DeathFade")?.GetComponent<Image>();
            _deathTitleText = transform.Find("DeathTitleText")?.GetComponent<Text>();
            _deathValueText = transform.Find("DeathValueText")?.GetComponent<Text>();
            CacheTideMarkers();
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

        public float TideBarFillAmount => _tideBarFill?.fillAmount ?? 0f;

        private void CreateTideBar(RectTransform rootRect, Sprite builtinSprite)
        {
            var tideBarRoot = CreateRect("TideBar", rootRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(900f, 30f));
            _tideBarBackdrop = CreateImage("TideBarBackdrop", tideBarRoot, builtinSprite, new Color(0.06f, 0.09f, 0.11f, 0.85f));
            _tideBarBackdrop.type = Image.Type.Sliced;
            _tideBarBackdrop.rectTransform.sizeDelta = new Vector2(900f, 16f);
            _tideBarFill = CreateImage("TideBarFill", _tideBarBackdrop.rectTransform, builtinSprite, new Color(0.72f, 0.8f, 0.88f, 0.98f));
            _tideBarFill.type = Image.Type.Filled;
            _tideBarFill.fillMethod = Image.FillMethod.Horizontal;
            _tideBarFill.fillAmount = 0f;
            _tideBarFill.rectTransform.anchorMin = Vector2.zero;
            _tideBarFill.rectTransform.anchorMax = Vector2.one;
            _tideBarFill.rectTransform.offsetMin = new Vector2(2f, 2f);
            _tideBarFill.rectTransform.offsetMax = new Vector2(-2f, -2f);
        }

        private void UpdateTideBar()
        {
            if (_tideBarFill == null || _zoneRuntime?.CurrentZoneDefinition == null)
            {
                return;
            }

            _tideBarFill.fillAmount = _tideController?.Progress ?? 0f;
            EnsureTideMarkers();

            var zoneDefinition = _zoneRuntime.CurrentZoneDefinition;
            for (var i = 0; i < _tideMarkers.Count && i < zoneDefinition.ExtractionPoints.Count; i++)
            {
                var point = zoneDefinition.ExtractionPoints[i];
                var isClosed = _zoneRuntime.IsExtractionOpen(point.PointId) == false;
                _tideMarkers[i].color = isClosed
                    ? new Color(0.24f, 0.27f, 0.31f, 1f)
                    : new Color(1f, 0.94f, 0.76f, 1f);
            }
        }

        private void EnsureTideMarkers()
        {
            if (_tideBarBackdrop == null || _zoneRuntime?.CurrentZoneDefinition == null || _tideController == null)
            {
                return;
            }

            var expectedCount = _zoneRuntime.CurrentZoneDefinition.ExtractionPoints.Count;
            if (_tideMarkers.Count == expectedCount)
            {
                return;
            }

            var markerRoot = _tideBarBackdrop.rectTransform.Find("TideMarkers") as RectTransform;
            if (markerRoot != null)
            {
                for (var i = markerRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(markerRoot.GetChild(i).gameObject);
                }
            }
            else
            {
                markerRoot = CreateRect("TideMarkers", _tideBarBackdrop.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                markerRoot.offsetMin = Vector2.zero;
                markerRoot.offsetMax = Vector2.zero;
            }

            _tideMarkers.Clear();
            var width = _tideBarBackdrop.rectTransform.rect.width;
            for (var i = 0; i < expectedCount; i++)
            {
                var point = _zoneRuntime.CurrentZoneDefinition.ExtractionPoints[i];
                var closePercent = _tideController.GetClosePercent(point);
                var marker = CreateImage($"Marker_{point.PointId}", markerRoot ?? _tideBarBackdrop.rectTransform, LoadBuiltinSprite("UI/Skin/UISprite.psd"), new Color(1f, 0.94f, 0.76f, 1f));
                marker.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                marker.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                marker.rectTransform.sizeDelta = new Vector2(6f, 22f);
                marker.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(2f, width - 2f, closePercent), 0f);
                _tideMarkers.Add(marker);
            }
        }

        private void CacheTideMarkers()
        {
            _tideMarkers.Clear();
            var markerRoot = transform.Find("TideBar/TideBarBackdrop/TideMarkers");
            if (markerRoot == null)
            {
                return;
            }

            for (var i = 0; i < markerRoot.childCount; i++)
            {
                var image = markerRoot.GetChild(i).GetComponent<Image>();
                if (image != null)
                {
                    _tideMarkers.Add(image);
                }
            }
        }

#if UNITY_EDITOR
        public void EditorConfigure(PlayerController playerController)
        {
            _playerController = playerController;
        }
#endif
    }
}
