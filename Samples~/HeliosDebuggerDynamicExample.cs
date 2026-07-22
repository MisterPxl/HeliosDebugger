using HeliosDebugger;
using UnityEngine;

public sealed class HeliosDebuggerDynamicExample : MonoBehaviour
{
    private HeliosDynamicOptionContainer _container;
    private float _enemySpeed = 1f;
    private int _spawnedEnemies;

    private void OnEnable()
    {
        _container = new HeliosDynamicOptionContainer();
        _container.AddOption(HeliosOptionDefinition.Create(
            "Enemy Speed",
            () => _enemySpeed,
            value => _enemySpeed = value,
            "Dynamic Sample"));
        _container.AddAction(HeliosOptionDefinition.FromMethod(
            "Spawn Enemy",
            () =>
            {
                _spawnedEnemies++;
                Debug.Log($"Spawned enemy {_spawnedEnemies} at speed {_enemySpeed}.");
            },
            "Dynamic Sample"));

        Helios.AddOptionContainer(_container);
    }

    private void OnDisable()
    {
        if (_container == null)
            return;

        Helios.RemoveOptionContainer(_container);
        _container = null;
    }
}
