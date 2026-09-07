using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HeliosDebugger
{
    [DefaultExecutionOrder(-9000)]
    public sealed class HeliosDebuggerRoot : MonoBehaviour
    {
        private const float PanelMarginX = 64f;
        private const float PanelMarginY = 48f;

        private readonly List<Button> _tabButtons = new List<Button>();
        private HeliosService _service;
        private HeliosWidgetFactory _widgets;
        private Canvas _canvas;
        private GameObject _panel;
        private GameObject _trigger;
        private RectTransform _panelRect;
        private RectTransform _triggerRect;
        private Rect _lastSafeArea;
        private GameObject _overlayRoot;
        private GameObject _challengePanel;
        private TMP_InputField _challengeInput;
        private TextMeshProUGUI _challengeStatus;
        private RectTransform _content;
        private Camera _canvasCamera;
        private Transform _worldSpaceAnchor;
        private float _lastTriggerTapTime;
        private int _triggerTapCount;
        private readonly List<Key> _konami = new List<Key>
        {
            Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow,
            Key.LeftArrow, Key.RightArrow, Key.LeftArrow, Key.RightArrow,
            Key.B, Key.A
        };
        private int _konamiIndex;

        public HeliosService Service => _service;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
#if HELIOS_DEBUGGER_DISABLE || HELIOS_DEBUGGER_DISABLE_AUTO_BOOT
            return;
#else
            HeliosDebuggerSettings settings = HeliosDebuggerSettings.LoadOrDefault();
            if (!settings.AutoBootstrap)
                return;

            if (!HeliosBootstrapPolicy.CanRun(settings, Application.isEditor, UnityEngine.Debug.isDebugBuild))
                return;

            if (FindAnyObjectByType<HeliosDebuggerRoot>() != null)
                return;

            var go = new GameObject("HeliosDebugger");
            DontDestroyOnLoad(go);
            Helios.Initialize(settings);
            go.AddComponent<HeliosDebuggerRoot>();
#endif
        }

        private void Awake()
        {
            _service = Helios.Service;
            _widgets = new HeliosWidgetFactory(_service.Settings.Theme);
            RegisterBuiltInOverlays();
            _service.AttachRoot(this);
            EnsureEventSystem();
            BuildInterface();
            _service.VisibilityChanged += OnVisibilityChanged;
            _service.TabsChanged += RebuildTabs;
            _service.OverlaysChanged += RebuildOverlays;
            _service.Access.ChallengeRequested += OnAccessChallengeRequested;
            OnVisibilityChanged();

            if (_service.Settings.VisibleAtStartup)
                _service.Show();
        }

        private void Update()
        {
            _service.Logs.FlushPending();
            _service.Tick(Time.unscaledDeltaTime);
            PollKeyboardAndGamepad();

            if (_service.ActiveTab != null && _service.IsVisible)
                _service.ActiveTab.Refresh();
            if (!_service.IsVisible)
            {
                for (int i = 0; i < _service.Overlays.Count; i++)
                    _service.Overlays[i].Refresh();
            }

            if (Screen.safeArea != _lastSafeArea)
                ApplySafeArea();

            HeliosPersistence.FlushIfDirty();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                HeliosPersistence.FlushIfDirty();
        }

        private void OnDestroy()
        {
            HeliosPersistence.FlushIfDirty();
            if (_service != null)
            {
                _service.VisibilityChanged -= OnVisibilityChanged;
                _service.TabsChanged -= RebuildTabs;
                _service.OverlaysChanged -= RebuildOverlays;
                _service.Access.ChallengeRequested -= OnAccessChallengeRequested;
                Helios.Shutdown();
            }

            if (_widgets != null)
                _widgets.DestroyGeneratedAssets();
            HeliosShapeLibrary.Clear();
            HeliosIcons.ClearCache();
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        public void SetCanvasCamera(Camera targetCamera)
        {
            _canvasCamera = targetCamera;
            ApplyCanvasPlacement();
        }

        public void SetWorldSpaceAnchor(Transform anchor)
        {
            _worldSpaceAnchor = anchor;
            ApplyCanvasPlacement();
        }

        public void RebuildActiveTab()
        {
            if (_content == null || _service.ActiveTab == null)
                return;

            RefreshTabSelection();
            _widgets.Clear(_content);
            _service.ActiveTab.Build(_widgets, _content);
        }

        public void RebuildOverlays()
        {
            if (_overlayRoot == null)
                return;

            _widgets.Clear(_overlayRoot.transform);
            for (int i = 0; i < _service.Overlays.Count; i++)
            {
                IHeliosOverlay overlay = _service.Overlays[i];
                GameObject root = _widgets.CreatePanel($"Overlay_{overlay.Id}", _overlayRoot.transform, Color.clear);
                HeliosWidgetFactory.Stretch(root.GetComponent<RectTransform>());
                overlay.Build(_widgets, root.transform);
            }
        }

        private void BuildInterface()
        {
            GameObject canvasGo = new GameObject("HeliosCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            ApplyCanvasPlacement();

            Color panelColor = _widgets.Theme.Panel;
            panelColor.a = _service.Settings.PanelOpacity;
            _panel = _widgets.CreateSurface("Panel", canvasGo.transform, panelColor, _widgets.Theme.CardCornerRadius);
            _widgets.AddShadow(_panel);
            _widgets.AddBorder(_panel, _widgets.Theme.Border);
            _panelRect = _panel.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(
                _panelRect,
                Vector2.zero,
                Vector2.one,
                new Vector2(PanelMarginX, PanelMarginY),
                new Vector2(-PanelMarginX, -PanelMarginY));

            GameObject header = _widgets.CreateSurface("Header", _panel.transform, _widgets.Theme.Header, _widgets.Theme.CardCornerRadius);
            RectTransform headerRect = header.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(headerRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), Vector2.zero);
            GameObject logo = _widgets.CreateSurface("Logo", header.transform, _widgets.Theme.Selected, 999f);
            RectTransform logoRect = logo.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(logoRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, -16f), new Vector2(50f, 16f));
            _widgets.AddIcon(logo.transform, HeliosIcons.Get(HeliosIcons.Console), _widgets.Theme.Accent, 18f);

            TextMeshProUGUI title = _widgets.CreateText("Title", header.transform, "Astra Helios", _widgets.Theme.TitleFontSize, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyles.Bold;
            HeliosWidgetFactory.Stretch(title.rectTransform, 60f, 0f, 90f, 0f);
            Button close = _widgets.CreateIconButton("Close", header.transform, HeliosIcons.Get("x"), () => _service.Hide());
            HeliosWidgetFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-58f, -18f), new Vector2(-22f, 18f));

            GameObject tabs = _widgets.CreateSurface("Tabs", _panel.transform, _widgets.Theme.Navigation, _widgets.Theme.CardCornerRadius);
            RectTransform tabsRect = tabs.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(tabsRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(212f, -60f));
            VerticalLayoutGroup tabLayout = tabs.AddComponent<VerticalLayoutGroup>();
            tabLayout.padding = new RectOffset(10, 10, 10, 10);
            tabLayout.spacing = 6f;
            tabLayout.childControlHeight = true;
            tabLayout.childControlWidth = true;
            tabLayout.childForceExpandHeight = false;

            ScrollRect scroll = _widgets.CreateScrollView("ContentScroll", _panel.transform, out _content);
            HeliosWidgetFactory.Anchor(scroll.GetComponent<RectTransform>(), new Vector2(0f, 0f), Vector2.one, new Vector2(220f, 10f), new Vector2(-10f, -70f));

            _overlayRoot = _widgets.CreatePanel("Overlays", canvasGo.transform, Color.clear);
            HeliosWidgetFactory.Stretch(_overlayRoot.GetComponent<RectTransform>());
            _overlayRoot.GetComponent<Image>().raycastTarget = false;

            Button trigger = _widgets.CreateButton(
                "Trigger",
                canvasGo.transform,
                _service.Settings.TriggerLabel,
                OnTriggerClicked,
                HeliosButtonStyle.Primary(_widgets.Theme));
            trigger.GetComponent<Image>().sprite = HeliosShapeLibrary.Circle();
            _trigger = trigger.gameObject;
            _triggerRect = _trigger.GetComponent<RectTransform>();
            ApplyTriggerLayout(_triggerRect);
            ApplySafeArea();
            if (_service.Settings.TriggerActivation == HeliosTriggerActivation.TapAndHold)
            {
                HeliosHoldTrigger hold = _trigger.AddComponent<HeliosHoldTrigger>();
                hold.Duration = _service.Settings.TriggerHoldDuration;
                hold.Invoked = () => _service.Toggle();
            }

            BuildChallengePanel(canvasGo.transform);

            RebuildTabs();
            RebuildActiveTab();
            RebuildOverlays();
        }

        private void RebuildTabs()
        {
            _tabButtons.Clear();
            Transform tabRoot = _panel.transform.Find("Tabs");
            if (tabRoot == null)
                return;

            for (int i = tabRoot.childCount - 1; i >= 0; i--)
                Destroy(tabRoot.GetChild(i).gameObject);

            for (int i = 0; i < _service.Tabs.Count; i++)
            {
                IHeliosTab tab = _service.Tabs[i];
                Button button = CreateTabButton(tabRoot, tab, i, () =>
                {
                    _service.OpenTab(tab.GetType());
                    RefreshTabSelection();
                    RebuildActiveTab();
                });
                _widgets.AddLayout(button.gameObject, 38f);
                _tabButtons.Add(button);
            }

            RefreshTabSelection();
            RebuildActiveTab();
        }

        private Button CreateTabButton(Transform tabRoot, IHeliosTab tab, int index, UnityEngine.Events.UnityAction action)
        {
            Button button = _widgets.CreateButton(
                $"Tab_{tab.Title}",
                tabRoot,
                string.Empty,
                action,
                HeliosButtonStyle.Ghost(_widgets.Theme));

            Sprite icon = tab is IHeliosTabIcon tabIcon ? tabIcon.Icon : null;
            if (icon != null)
            {
                Image iconImage = _widgets.AddIcon(button.transform, icon, _widgets.Theme.MutedText, 18f);
                iconImage.name = "TabIcon";
                RectTransform iconRect = iconImage.rectTransform;
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.anchoredPosition = new Vector2(19f, 0f);
            }
            else
            {
                TextMeshProUGUI monogram = _widgets.CreateText("TabIcon", button.transform, GetMonogram(tab.Title), 12, TextAnchor.MiddleCenter);
                monogram.color = _widgets.Theme.MutedText;
                monogram.fontStyle = FontStyles.Bold;
                HeliosWidgetFactory.Anchor(monogram.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, -11f), new Vector2(32f, 11f));
            }

            TextMeshProUGUI label = _widgets.CreateText("TabLabel", button.transform, tab.Title, _widgets.Theme.BaseFontSize, TextAnchor.MiddleLeft);
            label.fontStyle = FontStyles.Bold;
            label.color = _widgets.Theme.MutedText;
            HeliosWidgetFactory.Stretch(label.rectTransform, 42f, 0f, 38f, 0f);

            TextMeshProUGUI shortcut = _widgets.CreateText("Shortcut", button.transform, (index + 1).ToString(), _widgets.Theme.CaptionFontSize, TextAnchor.MiddleRight);
            shortcut.color = _widgets.Theme.MutedText;
            HeliosWidgetFactory.Stretch(shortcut.rectTransform, 0f, 0f, 12f, 0f);

            GameObject accent = _widgets.CreateSurface("ActiveAccent", button.transform, _widgets.Theme.Accent, 999f);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(accentRect, new Vector2(0f, 0.18f), new Vector2(0f, 0.82f), new Vector2(0f, 0f), new Vector2(3f, 0f));
            accent.SetActive(false);
            return button;
        }

        private void RefreshTabSelection()
        {
            for (int i = 0; i < _tabButtons.Count && i < _service.Tabs.Count; i++)
            {
                Button button = _tabButtons[i];
                bool active = _service.ActiveTab != null && _service.ActiveTab.GetType() == _service.Tabs[i].GetType();
                Image background = button.targetGraphic as Image;
                if (background != null)
                    background.color = active ? _widgets.Theme.Selected : Color.clear;

                SetChildColor(button.transform, "TabLabel", active ? _widgets.Theme.Text : _widgets.Theme.MutedText);
                SetChildColor(button.transform, "Shortcut", active ? _widgets.Theme.Accent : _widgets.Theme.MutedText);
                SetChildColor(button.transform, "TabIcon", active ? _widgets.Theme.Accent : _widgets.Theme.MutedText);
                Transform accent = button.transform.Find("ActiveAccent");
                if (accent != null)
                    accent.gameObject.SetActive(active);
            }
        }

        private static void SetChildColor(Transform parent, string childName, Color color)
        {
            Transform child = parent.Find(childName);
            if (child == null)
                return;
            TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.color = color;
            Image image = child.GetComponent<Image>();
            if (image != null)
                image.color = color;
        }

        private static string GetMonogram(string title)
        {
            return string.IsNullOrWhiteSpace(title) ? "?" : title.Trim()[0].ToString().ToUpperInvariant();
        }

        private void OnVisibilityChanged()
        {
            if (_panel != null)
                _panel.SetActive(_service.IsVisible);

            if (_trigger != null)
                _trigger.SetActive(_service.Settings.ShowTrigger && !_service.IsVisible);
            if (_overlayRoot != null)
                _overlayRoot.SetActive(!_service.IsVisible && (_challengePanel == null || !_challengePanel.activeSelf));
        }

        private void OnTriggerClicked()
        {
            HeliosTriggerActivation activation = _service.Settings.TriggerActivation;
            if (activation == HeliosTriggerActivation.TapAndHold)
                return;
            if (activation == HeliosTriggerActivation.SingleTap)
            {
                _service.Toggle();
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastTriggerTapTime > _service.Settings.TripleTapWindow)
                _triggerTapCount = 0;

            _lastTriggerTapTime = now;
            _triggerTapCount++;

            int requiredTaps = activation == HeliosTriggerActivation.DoubleTap ? 2 : 3;
            if (_triggerTapCount >= requiredTaps)
            {
                _triggerTapCount = 0;
                _service.Toggle();
            }
        }

        private void PollKeyboardAndGamepad()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;
            HeliosShortcutContext shortcutContext = new HeliosShortcutContext(_service, keyboard, gamepad);
            for (int i = 0; i < _service.Shortcuts.Count; i++)
            {
                if (_service.Shortcuts[i].TryHandle(shortcutContext))
                    return;
            }

            if (keyboard != null)
            {
                if (_service.IsVisible && _service.Settings.CloseOnEscape && keyboard.escapeKey.wasPressedThisFrame)
                {
                    _service.Hide();
                    return;
                }

                Key key = ToInputKey(_service.Settings.ToggleKey);
                if (key != Key.None && keyboard[key].wasPressedThisFrame)
                    _service.Toggle();

                if (_service.IsVisible)
                    PollTabShortcuts(keyboard);

                if (_service.Settings.EnableKonamiCode)
                    PollKonami(keyboard);
            }

            if (_service.Settings.EnableGamepadCombo && gamepad != null &&
                gamepad.startButton.wasPressedThisFrame && gamepad.selectButton.isPressed)
            {
                _service.Toggle();
            }
        }

        private void PollKonami(Keyboard keyboard)
        {
            if (_konamiIndex >= _konami.Count)
                _konamiIndex = 0;

            Key expected = _konami[_konamiIndex];
            if (keyboard[expected].wasPressedThisFrame)
            {
                _konamiIndex++;
                if (_konamiIndex >= _konami.Count)
                {
                    _konamiIndex = 0;
                    _service.Toggle();
                }
            }
            else if (AnyKonamiKeyPressed(keyboard))
            {
                _konamiIndex = 0;
            }
        }

        private bool AnyKonamiKeyPressed(Keyboard keyboard)
        {
            for (int i = 0; i < _konami.Count; i++)
            {
                if (keyboard[_konami[i]].wasPressedThisFrame)
                    return true;
            }

            return keyboard.anyKey.wasPressedThisFrame;
        }

        private static Key ToInputKey(KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;
                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;
                case KeyCode.BackQuote: return Key.Backquote;
                case KeyCode.Space: return Key.Space;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;
                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.Delete: return Key.Delete;
                case KeyCode.Insert: return Key.Insert;
                case KeyCode.Home: return Key.Home;
                case KeyCode.End: return Key.End;
                case KeyCode.PageUp: return Key.PageUp;
                case KeyCode.PageDown: return Key.PageDown;
                case KeyCode.Minus: return Key.Minus;
                case KeyCode.Equals: return Key.Equals;
                case KeyCode.LeftBracket: return Key.LeftBracket;
                case KeyCode.RightBracket: return Key.RightBracket;
                case KeyCode.Semicolon: return Key.Semicolon;
                case KeyCode.Quote: return Key.Quote;
                case KeyCode.Comma: return Key.Comma;
                case KeyCode.Period: return Key.Period;
                case KeyCode.Slash: return Key.Slash;
                case KeyCode.Backslash: return Key.Backslash;
                case KeyCode.Keypad0: return Key.Numpad0;
                case KeyCode.Keypad1: return Key.Numpad1;
                case KeyCode.Keypad2: return Key.Numpad2;
                case KeyCode.Keypad3: return Key.Numpad3;
                case KeyCode.Keypad4: return Key.Numpad4;
                case KeyCode.Keypad5: return Key.Numpad5;
                case KeyCode.Keypad6: return Key.Numpad6;
                case KeyCode.Keypad7: return Key.Numpad7;
                case KeyCode.Keypad8: return Key.Numpad8;
                case KeyCode.Keypad9: return Key.Numpad9;
                case KeyCode.KeypadEnter: return Key.NumpadEnter;
                default: return Key.None;
            }
        }

        private void RegisterBuiltInOverlays()
        {
            if (_service.Settings.ShowPinnedOverlay)
                _service.RegisterOverlay(new HeliosPinnedOptionsOverlay());
            if (_service.Settings.ShowDockedConsole)
                _service.RegisterOverlay(new HeliosDockedConsoleOverlay());
            if (_service.Settings.ShowDockedProfiler)
                _service.RegisterOverlay(new HeliosDockedProfilerOverlay());
        }

        private void PollTabShortcuts(Keyboard keyboard)
        {
            int index = -1;
            if (keyboard.digit1Key.wasPressedThisFrame) index = 0;
            else if (keyboard.digit2Key.wasPressedThisFrame) index = 1;
            else if (keyboard.digit3Key.wasPressedThisFrame) index = 2;
            else if (keyboard.digit4Key.wasPressedThisFrame) index = 3;
            else if (keyboard.digit5Key.wasPressedThisFrame) index = 4;

            if (index < 0 || index >= _service.Tabs.Count)
                return;
            _service.OpenTab(_service.Tabs[index].GetType());
            RefreshTabSelection();
            RebuildActiveTab();
        }

        private void ApplyTriggerLayout(RectTransform rect)
        {
            HeliosTriggerCorner corner = _service.Settings.TriggerCorner;
            bool right = corner == HeliosTriggerCorner.BottomRight || corner == HeliosTriggerCorner.TopRight;
            bool top = corner == HeliosTriggerCorner.TopLeft || corner == HeliosTriggerCorner.TopRight;
            Vector2 anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;

            Vector2 offset = _service.Settings.TriggerOffset;
            rect.anchoredPosition = new Vector2(right ? -offset.x : offset.x, top ? -offset.y : offset.y);
            rect.sizeDelta = _service.Settings.TriggerSize;
        }

        private void ApplySafeArea()
        {
            _lastSafeArea = Screen.safeArea;
            if (_canvas == null || _canvas.renderMode == RenderMode.WorldSpace)
                return;

            float scale = _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            Rect safe = _lastSafeArea;
            float left = safe.xMin / scale;
            float bottom = safe.yMin / scale;
            float right = (Screen.width - safe.xMax) / scale;
            float top = (Screen.height - safe.yMax) / scale;

            if (_panelRect != null)
            {
                _panelRect.offsetMin = new Vector2(PanelMarginX + left, PanelMarginY + bottom);
                _panelRect.offsetMax = new Vector2(-(PanelMarginX + right), -(PanelMarginY + top));
            }

            if (_triggerRect != null)
            {
                HeliosTriggerCorner corner = _service.Settings.TriggerCorner;
                bool cornerRight = corner == HeliosTriggerCorner.BottomRight || corner == HeliosTriggerCorner.TopRight;
                bool cornerTop = corner == HeliosTriggerCorner.TopLeft || corner == HeliosTriggerCorner.TopRight;
                Vector2 offset = _service.Settings.TriggerOffset;
                _triggerRect.anchoredPosition = new Vector2(
                    cornerRight ? -(offset.x + right) : offset.x + left,
                    cornerTop ? -(offset.y + top) : offset.y + bottom);
            }
        }

        private void ApplyCanvasPlacement()
        {
            if (_canvas == null)
                return;

            RectTransform rect = _canvas.GetComponent<RectTransform>();
            HeliosCanvasPlacement placement = _service.Settings.CanvasPlacement;
            if (placement == HeliosCanvasPlacement.WorldSpace)
            {
                _canvas.renderMode = RenderMode.WorldSpace;
                _canvas.worldCamera = _canvasCamera != null ? _canvasCamera : Camera.main;
                _canvas.transform.SetParent(_worldSpaceAnchor != null ? _worldSpaceAnchor : transform, false);
                rect.sizeDelta = new Vector2(1920f, 1080f);
                rect.localPosition = Vector3.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * _service.Settings.WorldSpaceScale;
                return;
            }

            _canvas.transform.SetParent(transform, false);
            rect.localScale = Vector3.one;
            if (placement == HeliosCanvasPlacement.ScreenSpaceCamera)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _canvasCamera != null ? _canvasCamera : Camera.main;
                _canvas.planeDistance = 10f;
            }
            else
            {
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.worldCamera = null;
            }
        }

        private void BuildChallengePanel(Transform canvasRoot)
        {
            _challengePanel = _widgets.CreateSurface("AccessChallenge", canvasRoot, new Color(0f, 0f, 0f, 0.78f), 0f);
            HeliosWidgetFactory.Stretch(_challengePanel.GetComponent<RectTransform>());

            GameObject card = _widgets.CreateSurface("Card", _challengePanel.transform, _widgets.Theme.Elevated, _widgets.Theme.CardCornerRadius);
            _widgets.AddShadow(card);
            _widgets.AddBorder(card, _widgets.Theme.Border);
            RectTransform cardRect = card.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(
                cardRect,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-220f, -100f),
                new Vector2(220f, 100f));

            TextMeshProUGUI title = _widgets.CreateText("Title", card.transform, "Helios access", 20, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyles.Bold;
            HeliosWidgetFactory.Anchor(title.rectTransform, new Vector2(0f, 0.7f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            _challengeInput = _widgets.CreateInput("Pin", card.transform, "PIN", null);
            _challengeInput.contentType = TMP_InputField.ContentType.Password;
            HeliosWidgetFactory.Anchor(
                _challengeInput.GetComponent<RectTransform>(),
                new Vector2(0.08f, 0.43f),
                new Vector2(0.7f, 0.66f),
                Vector2.zero,
                Vector2.zero);

            Button unlock = _widgets.CreateButton("Unlock", card.transform, "Unlock", SubmitChallenge, HeliosButtonStyle.Primary(_widgets.Theme));
            HeliosWidgetFactory.Anchor(
                unlock.GetComponent<RectTransform>(),
                new Vector2(0.72f, 0.43f),
                new Vector2(0.92f, 0.66f),
                Vector2.zero,
                Vector2.zero);

            _challengeStatus = _widgets.CreateText("Status", card.transform, string.Empty, 13, TextAnchor.MiddleCenter);
            HeliosWidgetFactory.Anchor(
                _challengeStatus.rectTransform,
                new Vector2(0.08f, 0.12f),
                new Vector2(0.92f, 0.38f),
                Vector2.zero,
                Vector2.zero);
            _challengePanel.SetActive(false);
        }

        private void OnAccessChallengeRequested(HeliosAccessRequest request)
        {
            if (_challengePanel == null)
                return;
            _challengeInput.text = string.Empty;
            _challengeStatus.text = "Enter the configured PIN.";
            _challengePanel.SetActive(true);
            if (_panel != null)
                _panel.SetActive(false);
            if (_overlayRoot != null)
                _overlayRoot.SetActive(false);
            _challengeInput.ActivateInputField();
        }

        private void SubmitChallenge()
        {
            if (_service.TryUnlock(_challengeInput.text))
            {
                _challengePanel.SetActive(false);
                OnVisibilityChanged();
                RebuildActiveTab();
                return;
            }

            _challengeStatus.text = "Invalid PIN.";
            _challengeInput.text = string.Empty;
            _challengeInput.ActivateInputField();
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
