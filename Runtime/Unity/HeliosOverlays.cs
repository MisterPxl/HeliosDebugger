using UnityEngine;

namespace HeliosDebugger
{
    public sealed class HeliosOverlayContext
    {
        public HeliosOverlayContext(HeliosService service, HeliosDebuggerRoot root)
        {
            Service = service;
            Root = root;
        }

        public HeliosService Service { get; }
        public HeliosDebuggerRoot Root { get; }
    }

    public interface IHeliosOverlay
    {
        string Id { get; }
        int Order { get; }
        void Initialize(HeliosOverlayContext context);
        void Build(HeliosWidgetFactory widgets, Transform parent);
        void Refresh();
        void Dispose();
    }

    public abstract class HeliosOverlayBase : IHeliosOverlay
    {
        protected HeliosOverlayContext Context { get; private set; }
        protected HeliosWidgetFactory Widgets { get; private set; }
        protected Transform Parent { get; private set; }

        public abstract string Id { get; }
        public abstract int Order { get; }

        public virtual void Initialize(HeliosOverlayContext context)
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
}
