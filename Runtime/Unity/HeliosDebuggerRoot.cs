using System.Collections;
using System.Collections.Generic;
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
        private readonly List<Button> _tabButtons = new List<Button>();
        private HeliosService _service;
        private HeliosWidgetFactory _widgets;
        private GameObject _panel;
        private GameObject _trigger;
        private RectTransform _content;
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

            if (settings.DevelopmentBuildOnly && !Application.isEditor && !UnityEngine.Debug.isDebugBuild)
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
            _service.AttachRoot(this);
            _widgets = new HeliosWidgetFactory();
            EnsureEventSystem();
            BuildInterface();
            _service.VisibilityChanged += OnVisibilityChanged;
            _service.TabsChanged += RebuildTabs;
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
        }

        private void OnDestroy()
        {
            if (_service != null)
            {
                _service.VisibilityChanged -= OnVisibilityChanged;
                _service.TabsChanged -= RebuildTabs;
                for (int i = 0; i < _service.Tabs.Count; i++)
                    _service.Tabs[i].Dispose();
                Helios.Shutdown();
            }
        }

        public Coroutine Run(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        public void RebuildActiveTab()
        {
            if (_content == null || _service.ActiveTab == null)
                return;

            _widgets.Clear(_content);
            _service.ActiveTab.Build(_widgets, _content);
        }

        private void BuildInterface()
        {
            GameObject canvasGo = new GameObject("HeliosCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = _widgets.CreatePanel("Panel", canvasGo.transform, new Color(0.02f, 0.025f, 0.035f, 0.97f));
            RectTransform panelRect = _panel.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(panelRect, Vector2.zero, Vector2.one, new Vector2(64f, 48f), new Vector2(-64f, -48f));

            GameObject header = _widgets.CreatePanel("Header", _panel.transform, new Color(0.08f, 0.1f, 0.14f, 0.98f));
            RectTransform headerRect = header.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(headerRect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), Vector2.zero);
            Text title = _widgets.CreateText("Title", header.transform, "HeliosDebugger", 22, TextAnchor.MiddleLeft);
            HeliosWidgetFactory.Stretch(title.rectTransform, 18f, 0f, 160f, 0f);
            Button close = _widgets.CreateButton("Close", header.transform, "Close", () => _service.Hide());
            HeliosWidgetFactory.Anchor(close.GetComponent<RectTransform>(), new Vector2(1f, 0.1f), new Vector2(1f, 0.9f), new Vector2(-146f, 0f), new Vector2(-16f, 0f));

            GameObject tabs = _widgets.CreatePanel("Tabs", _panel.transform, new Color(0.05f, 0.06f, 0.08f, 0.98f));
            RectTransform tabsRect = tabs.GetComponent<RectTransform>();
            HeliosWidgetFactory.Anchor(tabsRect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(180f, -60f));
            VerticalLayoutGroup tabLayout = tabs.AddComponent<VerticalLayoutGroup>();
            tabLayout.padding = new RectOffset(8, 8, 8, 8);
            tabLayout.spacing = 6f;
            tabLayout.childControlHeight = true;
            tabLayout.childControlWidth = true;
            tabLayout.childForceExpandHeight = false;

            ScrollRect scroll = _widgets.CreateScrollView("ContentScroll", _panel.transform, out _content);
            HeliosWidgetFactory.Anchor(scroll.GetComponent<RectTransform>(), new Vector2(0f, 0f), Vector2.one, new Vector2(188f, 8f), new Vector2(-8f, -68f));

            _trigger = _widgets.CreateButton("Trigger", canvasGo.transform, "H", OnTriggerClicked).gameObject;
            RectTransform triggerRect = _trigger.GetComponent<RectTransform>();
            triggerRect.anchorMin = new Vector2(1f, 0f);
            triggerRect.anchorMax = new Vector2(1f, 0f);
            triggerRect.pivot = new Vector2(1f, 0f);
            triggerRect.anchoredPosition = new Vector2(-18f, 18f);
            triggerRect.sizeDelta = new Vector2(54f, 54f);

            RebuildTabs();
            RebuildActiveTab();
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
                Button button = _widgets.CreateButton($"Tab_{tab.Title}", tabRoot, tab.Title, () =>
                {
                    _service.OpenTab(tab.GetType());
                    RebuildActiveTab();
                });
                _widgets.AddLayout(button.gameObject, 40f);
                _tabButtons.Add(button);
            }

            RebuildActiveTab();
        }

        private void OnVisibilityChanged()
        {
            if (_panel != null)
                _panel.SetActive(_service.IsVisible);

            if (_trigger != null)
                _trigger.SetActive(_service.Settings.ShowTrigger && !_service.IsVisible);
        }

        private void OnTriggerClicked()
        {
            if (!_service.Settings.RequireTripleTap)
            {
                _service.Toggle();
                return;
            }

            float now = Time.unscaledTime;
            if (now - _lastTriggerTapTime > _service.Settings.TripleTapWindow)
                _triggerTapCount = 0;

            _lastTriggerTapTime = now;
            _triggerTapCount++;

            if (_triggerTapCount >= 3)
            {
                _triggerTapCount = 0;
                _service.Toggle();
            }
        }

        private void PollKeyboardAndGamepad()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Key key = ToInputKey(_service.Settings.ToggleKey);
                if (key != Key.None && keyboard[key].wasPressedThisFrame)
                    _service.Toggle();

                if (_service.Settings.EnableKonamiCode)
                    PollKonami(keyboard);
            }

            Gamepad gamepad = Gamepad.current;
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
                default: return Key.None;
            }
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
