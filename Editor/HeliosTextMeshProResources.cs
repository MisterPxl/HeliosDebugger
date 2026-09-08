using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Astra.Helios.Editor
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Editor", "HeliosDebugger.Editor")]
    [InitializeOnLoad]
    public static class HeliosTextMeshProResources
    {
        private const string EssentialResourcesPackage = "Package Resources/TMP Essential Resources.unitypackage";
        private const string DefaultSettingsAssetPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string ImportAttemptKey = "HeliosDebugger.TextMeshProResourcesImportAttempted";

        static HeliosTextMeshProResources()
        {
            EditorApplication.delayCall += ImportIfMissing;
        }

        public static bool HasEssentialResources
        {
            get
            {
                TMP_Settings defaultSettings =
                    AssetDatabase.LoadAssetAtPath<TMP_Settings>(DefaultSettingsAssetPath);
                if (defaultSettings != null)
                    return true;

                string[] settingGuids = AssetDatabase.FindAssets("t:TMP_Settings");
                for (int i = 0; i < settingGuids.Length; i++)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(settingGuids[i]);
                    if (assetPath.Contains("/Resources/") &&
                        string.Equals(Path.GetFileNameWithoutExtension(assetPath), "TMP Settings",
                            System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static string MissingResourcesMessage =>
            "HeliosDebugger uses TextMeshPro. TMP Essential Resources must be imported before building or running the visual debugger.";

        [MenuItem("Tools/Astra/Helios/Import TMP Essential Resources")]
        public static void ImportEssentialResources()
        {
            if (HasEssentialResources)
            {
                Debug.Log("HeliosDebugger found TMP Essential Resources. Nothing needs to be imported.");
                return;
            }

            string packagePath = GetEssentialResourcesPackagePath();
            if (string.IsNullOrEmpty(packagePath))
            {
                Debug.LogError("Unable to locate TMP Essential Resources in the Unity TextMeshPro package.");
                return;
            }

            AssetDatabase.ImportPackage(packagePath, false);
        }

        private static void ImportIfMissing()
        {
            if (Application.isBatchMode)
                return;

            if (HasEssentialResources || SessionState.GetBool(ImportAttemptKey, false))
                return;

            SessionState.SetBool(ImportAttemptKey, true);
            Debug.Log("HeliosDebugger is importing TMP Essential Resources so runtime text has a valid font asset.");
            ImportEssentialResources();
        }

        private static string GetEssentialResourcesPackagePath()
        {
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (packageInfo == null || string.IsNullOrEmpty(packageInfo.resolvedPath))
                return null;

            string packagePath = Path.Combine(packageInfo.resolvedPath, EssentialResourcesPackage);
            return File.Exists(packagePath) ? packagePath : null;
        }
    }
}
