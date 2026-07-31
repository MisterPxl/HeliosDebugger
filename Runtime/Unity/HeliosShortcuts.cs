using UnityEngine.InputSystem;

namespace HeliosDebugger
{
    public sealed class HeliosShortcutContext
    {
        public HeliosShortcutContext(HeliosService service, Keyboard keyboard, Gamepad gamepad)
        {
            Service = service;
            Keyboard = keyboard;
            Gamepad = gamepad;
        }

        public HeliosService Service { get; }
        public Keyboard Keyboard { get; }
        public Gamepad Gamepad { get; }
    }

    public interface IHeliosShortcut
    {
        string Id { get; }
        int Order { get; }
        bool TryHandle(HeliosShortcutContext context);
    }
}
