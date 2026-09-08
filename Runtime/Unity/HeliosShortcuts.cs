using UnityEngine.InputSystem;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public readonly struct HeliosShortcutContext
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

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosShortcut
    {
        string Id { get; }
        int Order { get; }
        bool TryHandle(HeliosShortcutContext context);
    }
}
