using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HeliosDebugger.Editor
{
    public sealed class HeliosDebuggerBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            HeliosDebuggerSettings settings = HeliosDebuggerSettings.LoadOrDefault();
            if (settings.DevelopmentBuildOnly && !EditorUserBuildSettings.development)
            {
                UnityEngine.Debug.Log(
                    "HeliosDebugger is configured for Development Builds only. " +
                    "Runtime bootstrap will be disabled in this production build.");
            }

            if (!string.IsNullOrWhiteSpace(settings.WebhookUrl) && !settings.WebhookUrl.StartsWith("https://"))
                throw new BuildFailedException("HeliosDebugger webhook URLs must use HTTPS.");
        }
    }

    public static class HeliosDebuggerEditorMenu
    {
        [MenuItem("Tools/HeliosDebugger/Create Settings Asset")]
        public static void CreateSettingsAsset()
        {
            const string resourcesPath = "Assets/Resources";
            const string assetPath = resourcesPath + "/HeliosDebuggerSettings.asset";

            if (!AssetDatabase.IsValidFolder(resourcesPath))
                AssetDatabase.CreateFolder("Assets", "Resources");

            HeliosDebuggerSettings existing = AssetDatabase.LoadAssetAtPath<HeliosDebuggerSettings>(assetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return;
            }

            HeliosDebuggerSettings settings = ScriptableObject.CreateInstance<HeliosDebuggerSettings>();
            AssetDatabase.CreateAsset(settings, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = settings;
        }

        [MenuItem("Tools/HeliosDebugger/Validate Setup")]
        public static void ValidateSetup()
        {
            HeliosDebuggerSettings settings = HeliosDebuggerSettings.LoadOrDefault();
            UnityEngine.Debug.Log(
                $"HeliosDebugger setup valid. AutoBootstrap={settings.AutoBootstrap}, " +
                $"DevelopmentOnly={settings.DevelopmentBuildOnly}, LogCapacity={settings.LogCapacity}.");
        }
    }
}
