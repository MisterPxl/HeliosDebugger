using HeliosDebugger;
using UnityEngine;

[HeliosOptions("Sample")]
public static class HeliosDebuggerExample
{
    [HeliosOption("God Mode", Description = "Example boolean cheat.")]
    public static bool GodMode;

    [HeliosOption("Enemy Spawn Count", Persist = true)]
    [HeliosRange(0, 25, 1)]
    public static int EnemySpawnCount = 3;

    [HeliosOption("Game Speed")]
    [HeliosRange(0.25f, 3f, 0.25f)]
    public static float GameSpeed = 1f;

    [HeliosAction("Log Sample Action", Pin = true)]
    public static void LogSampleAction()
    {
        Debug.Log("Helios sample action executed.");
    }
}
