#if !HELIOS_DEBUGGER_DISABLE
using UnityEngine;

namespace HeliosDebugger.Samples
{
    public sealed class HeliosDebuggerDynamicExampleGui : MonoBehaviour
    {
        [SerializeField] private HeliosDebuggerDynamicExample _dynamicExample;

        private void OnGUI()
        {
            if (_dynamicExample == null)
                return;

            Rect area = new Rect(24f, 24f, 360f, 72f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Helios dynamic sample");

            bool nextEnabled = GUILayout.Toggle(
                _dynamicExample.IsEnemySpeedOptionEnabled,
                "Show Enemy Speed option");
            if (nextEnabled != _dynamicExample.IsEnemySpeedOptionEnabled)
                _dynamicExample.SetEnemySpeedOptionEnabled(nextEnabled);

            GUILayout.EndArea();
        }
    }
}
#endif
