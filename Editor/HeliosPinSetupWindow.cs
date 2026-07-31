using UnityEditor;
using UnityEngine;

namespace HeliosDebugger.Editor
{
    public sealed class HeliosPinSetupWindow : EditorWindow
    {
        private string _pin = string.Empty;
        private string _confirmation = string.Empty;
        private string _status = string.Empty;

        [MenuItem("Tools/HeliosDebugger/Configure Access PIN")]
        public static void ShowWindow()
        {
            HeliosPinSetupWindow window = GetWindow<HeliosPinSetupWindow>("Helios PIN");
            window.minSize = new Vector2(360f, 150f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Configure Helios access PIN", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The settings asset stores a salted PBKDF2 hash, never the plaintext PIN.",
                MessageType.Info);

            _pin = EditorGUILayout.PasswordField("PIN", _pin);
            _confirmation = EditorGUILayout.PasswordField("Confirm", _confirmation);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_pin) || _pin != _confirmation))
            {
                if (GUILayout.Button("Save PIN"))
                    SavePin();
            }

            if (!string.IsNullOrEmpty(_status))
                EditorGUILayout.LabelField(_status);
        }

        private void SavePin()
        {
            HeliosDebuggerSettings settings =
                AssetDatabase.LoadAssetAtPath<HeliosDebuggerSettings>(
                    "Assets/Resources/HeliosDebuggerSettings.asset");
            if (settings == null)
            {
                _status = "Create the HeliosDebugger settings asset first.";
                return;
            }

            HeliosPinAccessPolicy.CreateCredentials(_pin, out string salt, out string hash);
            Undo.RecordObject(settings, "Configure Helios PIN");
            settings.SetPinCredentials(salt, hash);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            _pin = string.Empty;
            _confirmation = string.Empty;
            _status = "PIN hash saved.";
        }
    }
}
