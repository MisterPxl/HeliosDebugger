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
            bool releaseBuild = (report.summary.options & BuildOptions.Development) == 0;
            if (releaseBuild && !settings.AllowInReleaseBuild)
            {
                const string message =
                    "HeliosDebugger is enabled for a release build. Add HELIOS_DEBUGGER_DISABLE " +
                    "to strip the runtime assembly, or explicitly enable Allow In Release Build.";
                if (settings.StrictReleaseValidation)
                    throw new BuildFailedException(message);
                UnityEngine.Debug.LogWarning(message);
            }

            if (!string.IsNullOrWhiteSpace(settings.WebhookUrl) && !settings.WebhookUrl.StartsWith("https://"))
                throw new BuildFailedException("HeliosDebugger webhook URLs must use HTTPS.");

            if (releaseBuild && settings.AllowInReleaseBuild && settings.RequirePin &&
                (string.IsNullOrWhiteSpace(settings.PinSalt) || string.IsNullOrWhiteSpace(settings.PinHash)))
            {
                throw new BuildFailedException(
                    "HeliosDebugger requires a configured PIN before it can be included in a release build.");
            }

            if (releaseBuild && settings.AllowInReleaseBuild &&
                !System.IO.File.Exists("Assets/HeliosDebuggerGenerated/HeliosGeneratedOptionsCatalog.g.cs"))
            {
                throw new BuildFailedException(
                    "Generate the Helios options catalog before building a release player.");
            }
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

        [MenuItem("Tools/HeliosDebugger/Create Theme Asset")]
        public static void CreateThemeAsset()
        {
            const string assetPath = "Assets/HeliosDebuggerTheme.asset";
            HeliosThemeProfile existing = AssetDatabase.LoadAssetAtPath<HeliosThemeProfile>(assetPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                return;
            }

            HeliosThemeProfile theme = ScriptableObject.CreateInstance<HeliosThemeProfile>();
            AssetDatabase.CreateAsset(theme, assetPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = theme;
        }
    }
}
