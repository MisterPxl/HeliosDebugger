#if !HELIOS_DEBUGGER_DISABLE
using HeliosDebugger;
using UnityEngine;

[HeliosOptions("Sample")]
public static class HeliosDebuggerExample
{
    [HeliosOption("God Mode", Description = "Example boolean cheat.", Pin = true)]
    public static bool GodMode;

    [HeliosOption("Enemy Spawn Count", Persist = true)]
    [HeliosRange(0, 25, 1)]
    public static int EnemySpawnCount = 3;

    [HeliosOption("Game Speed")]
    [HeliosRange(0.25f, 3f, 0.25f)]
    public static float GameSpeed = 1f;

    [HeliosOption("Spawn Offset", Description = "World-space offset used by the sample spawner.")]
    [HeliosRange(-20f, 20f, 0.5f)]
    public static Vector3 SpawnOffset = new Vector3(0f, 0f, 2f);

    [HeliosOption("Debug Tint", Description = "RGBA tint used by debug visualization.")]
    public static Color DebugTint = new Color(0.2f, 0.74f, 1f, 1f);

    [HeliosAction("Log Sample Action", Pin = true)]
    public static void LogSampleAction()
    {
        Debug.Log("Helios sample action executed.");
    }
}
#endif
