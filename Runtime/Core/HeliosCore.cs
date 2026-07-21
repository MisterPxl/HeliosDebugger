using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeliosDebugger
{
    public static class Helios
    {
        private static HeliosService _service;

        public static HeliosService Service => _service ?? (_service = HeliosService.CreateDefault());
        public static bool IsInitialized => _service != null;

        public static void Initialize(HeliosDebuggerSettings settings = null)
        {
            if (_service == null)
                _service = HeliosService.CreateDefault(settings);
            else if (settings != null)
                _service.ApplySettings(settings);
        }

        public static void Show() => Service.Show();
        public static void Hide() => Service.Hide();
        public static void Toggle() => Service.Toggle();
        public static void OpenTab<TTab>() where TTab : IHeliosTab => Service.OpenTab(typeof(TTab));
        public static void RegisterTab(IHeliosTab tab) => Service.RegisterTab(tab);
        public static void RegisterAction(HeliosActionDefinition action) => Service.RegisterAction(action);
        public static void RegisterOptions(object instance) => Service.Options.RegisterInstance(instance);
        public static void RegisterSystemInfoProvider(IHeliosSystemInfoProvider provider) => Service.SystemInfo.RegisterProvider(provider);
        public static void RegisterReportTransport(IHeliosReportTransport transport) => Service.Reporting.RegisterTransport(transport);
        public static void AddReportAttachment(HeliosReportAttachment attachment) => Service.Reporting.AddAttachment(attachment);

        public static void Shutdown()
        {
            _service?.Dispose();
            _service = null;
        }
    }

    public sealed class HeliosService
    {
        private readonly List<IHeliosTab> _tabs = new List<IHeliosTab>();
        private readonly List<HeliosActionDefinition> _actions = new List<HeliosActionDefinition>();

        private HeliosContext _context;

        public event Action VisibilityChanged;
        public event Action TabsChanged;
        public event Action ActionsChanged;

        public HeliosDebuggerSettings Settings { get; private set; }
        public HeliosLogStore Logs { get; }
        public HeliosProfilerSampler Profiler { get; }
        public HeliosOptionsRegistry Options { get; }
        public HeliosSystemInfoRegistry SystemInfo { get; }
        public HeliosReportService Reporting { get; }
        public bool IsVisible { get; private set; }
        public IHeliosTab ActiveTab { get; private set; }
        public IReadOnlyList<IHeliosTab> Tabs => _tabs;
        public IReadOnlyList<HeliosActionDefinition> Actions => _actions;

        private HeliosService(HeliosDebuggerSettings settings)
        {
            Settings = settings != null ? settings : HeliosDebuggerSettings.CreateRuntimeDefault();
            Logs = new HeliosLogStore(Settings.LogCapacity);
            Profiler = new HeliosProfilerSampler(Settings.ProfilerHistoryCapacity);
            Options = new HeliosOptionsRegistry();
            SystemInfo = new HeliosSystemInfoRegistry();
            Reporting = new HeliosReportService(Logs, Profiler, SystemInfo);
        }

        public static HeliosService CreateDefault(HeliosDebuggerSettings settings = null)
        {
            var service = new HeliosService(settings);
            service.RegisterDefaultTabs();
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
        }

        public void AttachRoot(HeliosDebuggerRoot root)
        {
            _context = new HeliosContext(this, root);
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].Initialize(_context);
        }

        public void RegisterTab(IHeliosTab tab)
        {
            if (tab == null)
                return;

            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].GetType() == tab.GetType())
                    return;
            }

            _tabs.Add(tab);
            _tabs.Sort((left, right) => left.Order.CompareTo(right.Order));

            if (_context != null)
                tab.Initialize(_context);

            ActiveTab = ActiveTab ?? tab;
            TabsChanged?.Invoke();
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

        public void Show()
        {
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
                TabsChanged?.Invoke();
                Show();
                return;
            }
        }

        public void Tick(float deltaTime)
        {
            Profiler.Tick(deltaTime);
        }

        public void Dispose()
        {
            Logs.Dispose();
            Profiler.Dispose();
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
            Reporting.RegisterTransport(new HeliosWebhookReportTransport());
            Reporting.RegisterTransport(new HeliosNativeShareReportTransport());
        }
    }

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

    public interface IHeliosTab
    {
        string Title { get; }
        int Order { get; }
        void Initialize(HeliosContext context);
        void Build(HeliosWidgetFactory widgets, Transform parent);
        void Refresh();
        void Dispose();
    }

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

    public sealed class HeliosActionDefinition
    {
        public HeliosActionDefinition(
            string id,
            string displayName,
            string category,
            string description,
            int order,
            Action execute)
        {
            Id = id;
            DisplayName = displayName;
            Category = category;
            Description = description;
            Order = order;
            Execute = execute;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public int Order { get; }
        public Action Execute { get; }

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
    }

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
