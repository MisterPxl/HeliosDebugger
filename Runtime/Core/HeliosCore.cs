using System;
using System.Collections.Generic;
using UnityEngine;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public static class Helios
    {
        private static readonly List<IHeliosTabProvider> TabProviders = new List<IHeliosTabProvider>();
        private static HeliosService _service;

        private static bool _changingService;

        /// <summary>Passive lifecycle notifications; subscribing never initializes Helios.</summary>
        public static event Action<HeliosService> Initialized;
        public static event Action<HeliosService> ShuttingDown;

        public static HeliosService Service
        {
            get
            {
                if (_service == null) Initialize();
                return _service;
            }
        }

        public static bool TryGetService(out HeliosService service)
        {
            service = _service;
            return service != null;
        }
        public static bool IsInitialized => _service != null;

        public static void Initialize(HeliosDebuggerSettings settings = null)
        {
            if (_changingService)
                throw new InvalidOperationException("Helios lifecycle cannot be changed from a lifecycle notification.");
            if (_service != null)
            {
                if (settings != null) _service.ApplySettings(settings);
                return;
            }
            _changingService = true;
            try
            {
                _service = HeliosService.CreateDefault(settings);
                NotifyLifecycle(Initialized, _service);
            }
            finally { _changingService = false; }
        }

        public static void Show() => Service.Show();
        public static void Hide() => Service.Hide();
        public static void Toggle() => Service.Toggle();
        public static void OpenTab<TTab>() where TTab : IHeliosTab => Service.OpenTab(typeof(TTab));
        /// <summary>
        /// Registers a tab and transfers disposal ownership to Helios when successful.
        /// A rejected duplicate or a tab whose initialization throws remains owned by the caller.
        /// </summary>
        public static bool RegisterTab(IHeliosTab tab) => Service.RegisterTab(tab);
        public static bool UnregisterTab<TTab>() where TTab : IHeliosTab =>
            _service != null && _service.UnregisterTab(typeof(TTab));
        public static bool RegisterTabProvider(IHeliosTabProvider provider)
        {
            if (provider == null)
                return false;

            Type providerType = provider.GetType();
            for (int i = 0; i < TabProviders.Count; i++)
            {
                if (TabProviders[i].GetType() == providerType)
                    return false;
            }

            TabProviders.Add(provider);
            TabProviders.Sort(CompareTabProviders);
            if (_service != null && _service.HasAttachedRoot)
                _service.RegisterTabProvider(provider);
            return true;
        }

        public static bool UnregisterTabProvider(IHeliosTabProvider provider)
        {
            if (provider == null)
                return false;

            for (int i = 0; i < TabProviders.Count; i++)
            {
                if (!ReferenceEquals(TabProviders[i], provider))
                    continue;

                Type providerType = provider.GetType();
                TabProviders.RemoveAt(i);
                _service?.UnregisterTabProvider(providerType);
                return true;
            }

            return false;
        }

        public static bool UnregisterTabProvider<TProvider>() where TProvider : IHeliosTabProvider
        {
            Type providerType = typeof(TProvider);
            for (int i = 0; i < TabProviders.Count; i++)
            {
                if (TabProviders[i].GetType() != providerType)
                    continue;

                TabProviders.RemoveAt(i);
                _service?.UnregisterTabProvider(providerType);
                return true;
            }

            return false;
        }
        public static void RegisterOverlay(IHeliosOverlay overlay) => Service.RegisterOverlay(overlay);
        public static void RegisterShortcut(IHeliosShortcut shortcut) => Service.RegisterShortcut(shortcut);
        public static void RegisterOptionControlBuilder(IHeliosOptionControlBuilder builder) => Service.RegisterOptionControlBuilder(builder);
        public static void RegisterAction(HeliosActionDefinition action) => Service.RegisterAction(action);
        public static void RegisterStaticOptions<TOptions>() => Service.Options.RegisterStaticType(typeof(TOptions));
        public static void RegisterOptions(object instance) => Service.Options.RegisterInstance(instance);
        public static bool UnregisterOptions(object instance) => Service.Options.UnregisterInstance(instance);
        public static void AddOptionContainer(IHeliosOptionContainer container) => Service.AddOptionContainer(container);
        public static bool RemoveOptionContainer(IHeliosOptionContainer container) => Service.RemoveOptionContainer(container);
        public static void AddOption(IHeliosValueOption option) => Service.AddOption(option);
        public static void AddOption(IHeliosActionOption action) => Service.AddOption(action);
        public static bool RemoveOption(IHeliosValueOption option) => Service.RemoveOption(option);
        public static bool RemoveOption(IHeliosActionOption action) => Service.RemoveOption(action);
        public static void RegisterSystemInfoProvider(IHeliosSystemInfoProvider provider) => Service.SystemInfo.RegisterProvider(provider);
        public static void RegisterReportTransport(IHeliosReportTransport transport) => Service.Reporting.RegisterTransport(transport);
        public static void AddReportAttachment(HeliosReportArtifact attachment) => Service.Reporting.AddAttachment(attachment);
        public static void RegisterNativeShareProvider(IHeliosNativeShareProvider provider) => Service.RegisterNativeShareProvider(provider);
        public static void SetAccessPolicy(IHeliosAccessPolicy policy) => Service.Access.SetPolicy(policy);
        public static bool TryUnlock(string credential) => Service.TryUnlock(credential);
        public static void Lock() => Service.Access.Lock();

        public static void Shutdown()
        {
            if (_changingService)
                throw new InvalidOperationException("Helios lifecycle cannot be changed from a lifecycle notification.");
            if (_service == null) return;
            _changingService = true;
            HeliosService previous = _service;
            _service = null;
            try
            {
                NotifyLifecycle(ShuttingDown, previous);
                previous.Dispose();
            }
            finally { _changingService = false; }
        }

        /// <summary>Only the owner of the current generation may shut it down.</summary>
        public static bool Shutdown(HeliosService expectedService)
        {
            if (expectedService == null || !ReferenceEquals(_service, expectedService)) return false;
            Shutdown();
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            Shutdown();
            Initialized = null;
            ShuttingDown = null;
            TabProviders.Clear();
        }

        private static void NotifyLifecycle(Action<HeliosService> handlers, HeliosService service)
        {
            if (handlers == null) return;
            foreach (Action<HeliosService> handler in handlers.GetInvocationList())
            {
                try { handler(service); }
                catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            }
        }

        internal static void MaterializeTabProviders(HeliosService service)
        {
            if (service == null)
                return;

            for (int i = 0; i < TabProviders.Count; i++)
                service.RegisterTabProvider(TabProviders[i]);
        }

        internal static void ResetTabProvidersForTests()
        {
            Shutdown();
            TabProviders.Clear();
        }

        private static int CompareTabProviders(IHeliosTabProvider left, IHeliosTabProvider right)
        {
            string leftName = left.GetType().FullName ?? left.GetType().Name;
            string rightName = right.GetType().FullName ?? right.GetType().Name;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosService
    {
        private readonly List<IHeliosTab> _tabs = new List<IHeliosTab>();
        private readonly List<IHeliosOverlay> _overlays = new List<IHeliosOverlay>();
        private readonly List<IHeliosShortcut> _shortcuts = new List<IHeliosShortcut>();
        private readonly List<IHeliosOptionControlBuilder> _optionControlBuilders = new List<IHeliosOptionControlBuilder>();
        private readonly List<HeliosActionDefinition> _actions = new List<HeliosActionDefinition>();
        private readonly HashSet<Type> _materializedTabProviders = new HashSet<Type>();
        private readonly Dictionary<Type, IHeliosTab> _providerTabs = new Dictionary<Type, IHeliosTab>();

        private HeliosContext _context;
        private bool _initialTabSelected;
        private bool _disposed;

        public event Action VisibilityChanged;
        public event Action TabsChanged;
        public event Action OverlaysChanged;
        public event Action ActionsChanged;

        public HeliosDebuggerSettings Settings { get; private set; }
        public HeliosLogStore Logs { get; }
        public HeliosProfilerSampler Profiler { get; }
        public HeliosOptionsRegistry Options { get; }
        public HeliosSystemInfoRegistry SystemInfo { get; }
        public HeliosReportService Reporting { get; }
        public HeliosAccessController Access { get; }
        public bool IsVisible { get; private set; }
        public bool IsDisposed => _disposed;
        public IHeliosTab ActiveTab { get; private set; }
        public IReadOnlyList<IHeliosTab> Tabs => _tabs;
        public IReadOnlyList<IHeliosOverlay> Overlays => _overlays;
        public IReadOnlyList<IHeliosShortcut> Shortcuts => _shortcuts;
        public IReadOnlyList<IHeliosOptionControlBuilder> OptionControlBuilders => _optionControlBuilders;
        public IReadOnlyList<HeliosActionDefinition> Actions => _actions;
        internal bool HasAttachedRoot => _context != null && _context.Root != null;
        public bool CanInteractWithDebugger => !_disposed && IsVisible &&
            Access.Policy.Evaluate(new HeliosAccessRequest(HeliosAccessOperation.OpenDebugger)) == HeliosAccessDecision.Allow;

        private HeliosService(HeliosDebuggerSettings settings)
        {
            Settings = settings != null ? settings : HeliosDebuggerSettings.CreateRuntimeDefault();
            Logs = new HeliosLogStore(Settings.LogCapacity);
            Profiler = new HeliosProfilerSampler(Settings.ProfilerHistoryCapacity);
            Options = new HeliosOptionsRegistry();
            SystemInfo = new HeliosSystemInfoRegistry();
            Reporting = new HeliosReportService(Logs, Profiler, SystemInfo);
            Access = new HeliosAccessController(CreateAccessPolicy(Settings));
            Access.Changed += RevalidateAccess;
            foreach (IHeliosOptionControlBuilder builder in HeliosBuiltInOptionControls.Create())
                RegisterOptionControlBuilder(builder);
        }

        public static HeliosService CreateDefault(HeliosDebuggerSettings settings = null)
        {
            var service = new HeliosService(settings);
            service.RegisterDefaultTabs();
            Helios.MaterializeTabProviders(service);
            service.RegisterDefaultProviders();
            return service;
        }

        public void ApplySettings(HeliosDebuggerSettings settings)
        {
            if (settings == null)
                return;

            Settings = settings;
            Logs.SetCapacity(settings.LogCapacity);
            Profiler.SetCapacity(settings.ProfilerHistoryCapacity);
            Access.SetPolicy(CreateAccessPolicy(settings));
            HeliosWebhookReportTransport webhook =
                Reporting.GetTransport(HeliosTransportId.Webhook) as HeliosWebhookReportTransport;
            webhook?.Configure(settings.WebhookUrl, 20);
        }

        public void AttachRoot(HeliosDebuggerRoot root)
        {
            _context = new HeliosContext(this, root);
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Initialize(_context);
            Helios.MaterializeTabProviders(this);
            HeliosOverlayContext overlayContext = new HeliosOverlayContext(this, root);
            for (int i = 0; i < _overlays.Count; i++)
                _overlays[i].Initialize(overlayContext);
        }

        /// <summary>
        /// Registers a tab and transfers disposal ownership to this service when successful.
        /// A rejected duplicate or a tab whose initialization throws remains owned by the caller.
        /// </summary>
        public bool RegisterTab(IHeliosTab tab)
        {
            if (_disposed || tab == null)
                return false;

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].GetType() == tab.GetType())
                    return false;
            }

            _tabs.Add(tab);
            _tabs.Sort(CompareTabs);

            if (_context != null)
            {
                try
                {
                    tab.Initialize(_context);
                }
                catch
                {
                    _tabs.Remove(tab);
                    throw;
                }
            }

            ActiveTab = ActiveTab ?? tab;
            TabsChanged?.Invoke();
            return true;
        }

        public bool UnregisterTab<TTab>() where TTab : IHeliosTab
        {
            return UnregisterTab(typeof(TTab));
        }

        internal bool UnregisterTab(Type tabType)
        {
            if (_disposed || tabType == null)
                return false;

            for (int i = 0; i < _tabs.Count; i++)
            {
                IHeliosTab tab = _tabs[i];
                if (tab.GetType() != tabType)
                    continue;

                _tabs.RemoveAt(i);
                RemoveProviderTabMappings(tab);
                if (ReferenceEquals(ActiveTab, tab))
                    ActiveTab = _tabs.Count > 0 ? _tabs[0] : null;
                DisposeTabSafely(tab);
                Helios.MaterializeTabProviders(this);
                TabsChanged?.Invoke();
                return true;
            }

            return false;
        }

        internal bool RegisterTabProvider(IHeliosTabProvider provider)
        {
            if (_disposed || provider == null)
                return false;

            Type providerType = provider.GetType();
            if (!_materializedTabProviders.Add(providerType))
                return false;

            IHeliosTab tab;
            try
            {
                tab = provider.CreateTab();
            }
            catch
            {
                _materializedTabProviders.Remove(providerType);
                throw;
            }

            if (tab == null)
            {
                _materializedTabProviders.Remove(providerType);
                return false;
            }

            bool registered;
            try
            {
                registered = RegisterTab(tab);
            }
            catch
            {
                _materializedTabProviders.Remove(providerType);
                DisposeTabSafely(tab);
                throw;
            }

            if (!registered)
            {
                _materializedTabProviders.Remove(providerType);
                DisposeTabSafely(tab);
                return false;
            }

            _providerTabs.Add(providerType, tab);
            return true;
        }

        internal bool UnregisterTabProvider(Type providerType)
        {
            if (providerType == null || !_materializedTabProviders.Remove(providerType))
                return false;

            if (!_providerTabs.TryGetValue(providerType, out IHeliosTab tab))
                return true;

            _providerTabs.Remove(providerType);
            return UnregisterTab(tab.GetType());
        }

        public void RegisterOverlay(IHeliosOverlay overlay)
        {
            if (_disposed || overlay == null || string.IsNullOrWhiteSpace(overlay.Id))
                return;

            for (int i = 0; i < _overlays.Count; i++)
            {
                if (string.Equals(_overlays[i].Id, overlay.Id, StringComparison.Ordinal))
                {
                    DisposeOverlaySafely(_overlays[i]);
                    _overlays[i] = overlay;
                    if (_context != null)
                        overlay.Initialize(new HeliosOverlayContext(this, _context.Root));
                    _overlays.Sort((left, right) => left.Order.CompareTo(right.Order));
                    OverlaysChanged?.Invoke();
                    return;
                }
            }

            _overlays.Add(overlay);
            _overlays.Sort((left, right) => left.Order.CompareTo(right.Order));
            if (_context != null)
                overlay.Initialize(new HeliosOverlayContext(this, _context.Root));
            OverlaysChanged?.Invoke();
        }

        public void RegisterShortcut(IHeliosShortcut shortcut)
        {
            if (shortcut == null || string.IsNullOrWhiteSpace(shortcut.Id))
                return;

            for (int i = 0; i < _shortcuts.Count; i++)
            {
                if (!string.Equals(_shortcuts[i].Id, shortcut.Id, StringComparison.Ordinal))
                    continue;
                _shortcuts[i] = shortcut;
                _shortcuts.Sort((left, right) => left.Order.CompareTo(right.Order));
                return;
            }

            _shortcuts.Add(shortcut);
            _shortcuts.Sort((left, right) => left.Order.CompareTo(right.Order));
        }

        public void RegisterOptionControlBuilder(IHeliosOptionControlBuilder builder)
        {
            if (builder == null)
                return;

            for (int i = 0; i < _optionControlBuilders.Count; i++)
            {
                if (_optionControlBuilders[i].GetType() != builder.GetType())
                    continue;
                _optionControlBuilders[i] = builder;
                _optionControlBuilders.Sort((left, right) => left.Order.CompareTo(right.Order));
                return;
            }

            _optionControlBuilders.Add(builder);
            _optionControlBuilders.Sort((left, right) => left.Order.CompareTo(right.Order));
        }

        public void RegisterAction(HeliosActionDefinition action)
        {
            if (action == null || string.IsNullOrWhiteSpace(action.Id))
                return;

            for (int i = 0; i < _actions.Count; i++)
            {
                if (string.Equals(_actions[i].Id, action.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _actions[i] = action;
                    ActionsChanged?.Invoke();
                    return;
                }
            }

            _actions.Add(action);
            _actions.Sort((left, right) => left.Order.CompareTo(right.Order));
            ActionsChanged?.Invoke();
        }

        /// <summary>Removes this exact registration without removing a replacement sharing its ID.</summary>
        public bool UnregisterAction(HeliosActionDefinition action)
        {
            if (action == null || !_actions.Remove(action)) return false;
            ActionsChanged?.Invoke();
            return true;
        }

        public void AddOptionContainer(IHeliosOptionContainer container)
        {
            Options.RegisterOptionContainer(container);
        }

        public bool RemoveOptionContainer(IHeliosOptionContainer container)
        {
            return Options.UnregisterOptionContainer(container);
        }

        public void AddOption(IHeliosValueOption option)
        {
            Options.AddOption(option);
        }

        public void AddOption(IHeliosActionOption action)
        {
            Options.AddOption(action);
        }

        public bool RemoveOption(IHeliosValueOption option)
        {
            return Options.RemoveOption(option);
        }

        public bool RemoveOption(IHeliosActionOption action)
        {
            return Options.RemoveOption(action);
        }

        public void Show()
        {
            Access.Request(
                new HeliosAccessRequest(HeliosAccessOperation.OpenDebugger),
                ShowAllowed);
        }

        private void ShowAllowed()
        {
            SelectInitialTab();
            IsVisible = true;
            VisibilityChanged?.Invoke();
        }

        public void Hide()
        {
            IsVisible = false;
            VisibilityChanged?.Invoke();
        }

        public void Toggle()
        {
            if (IsVisible) Hide(); else Show();
        }

        public void OpenTab(Type tabType)
        {
            if (tabType == null)
                return;

            for (int i = 0; i < _tabs.Count; i++)
            {
                IHeliosTab tab = _tabs[i];
                if (tab.GetType() != tabType)
                    continue;

                ActiveTab = tab;
                RememberTab(tab);
                IHeliosTabOpenHandler openHandler = tab as IHeliosTabOpenHandler;
                if (openHandler != null)
                    openHandler.OnOpened();
                TabsChanged?.Invoke();
                Show();
                return;
            }
        }

        public void OpenTabById(string tabId)
        {
            if (string.IsNullOrWhiteSpace(tabId))
                return;

            for (int i = 0; i < _tabs.Count; i++)
            {
                IHeliosTab tab = _tabs[i];
                if (!string.Equals(GetTabId(tab), tabId, StringComparison.Ordinal))
                    continue;

                OpenTab(tab.GetType());
                return;
            }
        }

        public bool TryUnlock(string credential)
        {
            return Access.TryUnlock(credential);
        }

        public void Tick(float deltaTime)
        {
            RevalidateAccess();
            Profiler.Tick(deltaTime);
        }

        private void RevalidateAccess()
        {
            if (IsVisible && !CanInteractWithDebugger)
                Hide();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _actions.Clear();
            Access.Changed -= RevalidateAccess;
            for (int i = 0; i < _tabs.Count; i++)
                DisposeTabSafely(_tabs[i]);
            _tabs.Clear();
            _providerTabs.Clear();
            _materializedTabProviders.Clear();
            ActiveTab = null;
            for (int i = 0; i < _overlays.Count; i++)
                DisposeOverlaySafely(_overlays[i]);
            _overlays.Clear();
            Options.Dispose();
            Logs.Dispose();
            Profiler.Dispose();
            _context = null;
        }

        private static void DisposeTabSafely(IHeliosTab tab)
        {
            try
            {
                tab?.Dispose();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private static void DisposeOverlaySafely(IHeliosOverlay overlay)
        {
            try
            {
                overlay?.Dispose();
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        private void RemoveProviderTabMappings(IHeliosTab tab)
        {
            Type providerType = null;
            foreach (KeyValuePair<Type, IHeliosTab> pair in _providerTabs)
            {
                if (ReferenceEquals(pair.Value, tab))
                {
                    providerType = pair.Key;
                    break;
                }
            }

            if (providerType != null)
                _providerTabs.Remove(providerType);
        }

        private static int CompareTabs(IHeliosTab left, IHeliosTab right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
                return order;

            string leftName = left.GetType().FullName ?? left.GetType().Name;
            string rightName = right.GetType().FullName ?? right.GetType().Name;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private void RegisterDefaultTabs()
        {
            RegisterTab(new HeliosConsoleTab());
            RegisterTab(new HeliosProfilerTab());
            RegisterTab(new HeliosOptionsTab());
            RegisterTab(new HeliosSystemInfoTab());
            RegisterTab(new HeliosBugReporterTab());
        }

        private void RegisterDefaultProviders()
        {
            SystemInfo.RegisterProvider(new HeliosDefaultSystemInfoProvider());
            Reporting.RegisterTransport(new HeliosLocalReportTransport());
            Reporting.RegisterTransport(new HeliosWebhookReportTransport(Settings.WebhookUrl));
            Reporting.RegisterTransport(new HeliosNativeShareReportTransport());
        }

        public void RegisterNativeShareProvider(IHeliosNativeShareProvider provider)
        {
            HeliosNativeShareReportTransport transport =
                Reporting.GetTransport(HeliosTransportId.NativeShare) as HeliosNativeShareReportTransport;
            if (transport == null)
            {
                transport = new HeliosNativeShareReportTransport();
                Reporting.RegisterTransport(transport);
            }
            transport.Providers.Register(provider);
        }

        private void SelectInitialTab()
        {
            if (_initialTabSelected)
                return;

            _initialTabSelected = true;
            string tabId = Settings.DefaultTabId;
            if (Settings.RememberLastTab)
                tabId = PlayerPrefs.GetString("HeliosDebugger.LastTab", tabId);

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (!string.Equals(GetTabId(_tabs[i]), tabId, StringComparison.Ordinal))
                    continue;
                ActiveTab = _tabs[i];
                IHeliosTabOpenHandler openHandler = ActiveTab as IHeliosTabOpenHandler;
                openHandler?.OnOpened();
                TabsChanged?.Invoke();
                return;
            }
        }

        private void RememberTab(IHeliosTab tab)
        {
            if (!Settings.RememberLastTab || tab == null)
                return;
            PlayerPrefs.SetString("HeliosDebugger.LastTab", GetTabId(tab));
        }

        private static string GetTabId(IHeliosTab tab)
        {
            return HeliosTypeIdentityAttribute.GetId(tab.GetType());
        }

        private static IHeliosAccessPolicy CreateAccessPolicy(HeliosDebuggerSettings settings)
        {
            if (settings == null || !settings.RequirePin)
                return new HeliosAllowAllAccessPolicy();

            try
            {
                return new HeliosPinAccessPolicy(
                    settings.PinSalt,
                    settings.PinHash,
                    TimeSpan.FromMinutes(settings.PinSessionMinutes));
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError($"Helios PIN configuration is invalid: {exception.Message}");
                return new HeliosDenyAllAccessPolicy();
            }
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosContext
    {
        public HeliosContext(HeliosService service, HeliosDebuggerRoot root)
        {
            Service = service ?? throw new ArgumentNullException(nameof(service));
            Root = root;
        }

        public HeliosService Service { get; }
        public HeliosDebuggerRoot Root { get; }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosTab
    {
        string Title { get; }
        int Order { get; }
        void Initialize(HeliosContext context);
        void Build(HeliosWidgetFactory widgets, Transform parent);
        void Refresh();
        void Dispose();
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosTabProvider
    {
        /// <summary>
        /// Creates a fresh tab whose disposal ownership transfers to Helios.
        /// </summary>
        IHeliosTab CreateTab();
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosTabOpenHandler
    {
        void OnOpened();
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public abstract class HeliosTabBase : IHeliosTab
    {
        protected HeliosContext Context { get; private set; }
        protected HeliosWidgetFactory Widgets { get; private set; }
        protected Transform Parent { get; private set; }

        public abstract string Title { get; }
        public abstract int Order { get; }

        public virtual void Initialize(HeliosContext context)
        {
            Context = context;
        }

        public void Build(HeliosWidgetFactory widgets, Transform parent)
        {
            Widgets = widgets;
            Parent = parent;
            BuildContent(widgets, parent);
            Refresh();
        }

        public virtual void Refresh()
        {
        }

        public virtual void Dispose()
        {
        }

        protected abstract void BuildContent(HeliosWidgetFactory widgets, Transform parent);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosActionDefinition : IHeliosActionOption
    {
        private static readonly HeliosActionParameter[] NoParameters = new HeliosActionParameter[0];

        public HeliosActionDefinition(
            string id,
            string displayName,
            string category,
            string description,
            int order,
            Action execute,
            bool pin = false)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Description = description;
            Order = order;
            Execute = execute;
            Pin = pin;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public int Order { get; }
        public bool Pin { get; }
        public Action Execute { get; }
        public IReadOnlyList<HeliosActionParameter> Parameters => NoParameters;

        public HeliosActionResult Invoke()
        {
            try
            {
                Execute?.Invoke();
                return HeliosActionResult.Succeed($"Executed {DisplayName}.");
            }
            catch (Exception ex)
            {
                return HeliosActionResult.Fail(ex.Message, ex);
            }
        }

        public HeliosActionResult Invoke(IReadOnlyList<string> parameterValues)
        {
            return Invoke();
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosActionResult
    {
        private HeliosActionResult(bool success, string message, Exception exception)
        {
            Success = success;
            Message = message;
            Exception = exception;
        }

        public bool Success { get; }
        public string Message { get; }
        public Exception Exception { get; }

        public static HeliosActionResult Succeed(string message)
        {
            return new HeliosActionResult(true, message, null);
        }

        public static HeliosActionResult Fail(string message, Exception exception = null)
        {
            return new HeliosActionResult(false, message, exception);
        }
    }
}
