#if !HELIOS_DEBUGGER_DISABLE
using UnityEngine;

namespace Astra.Helios.Samples
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Samples")]
    public sealed class HeliosDebuggerDynamicExample : MonoBehaviour
    {
        private const string Category = "Dynamic Sample";

        private HeliosDynamicOptionContainer _container;
        private HeliosOptionDefinition<float> _enemySpeedOption;
        private bool _isEnemySpeedOptionEnabled = true;
        private float _enemySpeed = 1f;
        private int _spawnedEnemies;

        public bool IsEnemySpeedOptionEnabled => _isEnemySpeedOptionEnabled;

        private void OnEnable()
        {
            _container = new HeliosDynamicOptionContainer();
            _enemySpeedOption = HeliosOptionDefinition.Create<float>(
                "Enemy Speed",
                () => _enemySpeed,
                value => _enemySpeed = value,
                Category);
            _container.AddAction(HeliosOptionDefinition.FromMethod(
                "Spawn Enemy",
                () =>
                {
                    _spawnedEnemies++;
                    Debug.Log($"Spawned enemy {_spawnedEnemies} at speed {_enemySpeed}.");
                },
                Category));

            Helios.AddOptionContainer(_container);
            SetEnemySpeedOptionEnabled(_isEnemySpeedOptionEnabled);
        }

        private void OnDisable()
        {
            if (_container == null)
                return;

            Helios.RemoveOptionContainer(_container);
            _container = null;
            _enemySpeedOption = null;
        }

        public void SetEnemySpeedOptionEnabled(bool isEnabled)
        {
            _isEnemySpeedOptionEnabled = isEnabled;

            if (_container == null || _enemySpeedOption == null)
                return;

            if (isEnabled)
                _container.AddOption(_enemySpeedOption);
            else
                _container.RemoveOption(_enemySpeedOption);
        }
    }
}
#endif
