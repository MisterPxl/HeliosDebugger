using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace HeliosDebugger.Editor
{
    public static class HeliosDebuggerDefineMenu
    {
        private const string DisableDefine = "HELIOS_DEBUGGER_DISABLE";

        [MenuItem("Tools/HeliosDebugger/Compilation/Enable Runtime")]
        public static void EnableRuntime()
        {
            SetDisabled(false);
        }

        [MenuItem("Tools/HeliosDebugger/Compilation/Disable Runtime")]
        public static void DisableRuntime()
        {
            SetDisabled(true);
        }

        [MenuItem("Tools/HeliosDebugger/Compilation/Enable Runtime", true)]
        private static bool ValidateEnableRuntime()
        {
            return HasDefine();
        }

        [MenuItem("Tools/HeliosDebugger/Compilation/Disable Runtime", true)]
        private static bool ValidateDisableRuntime()
        {
            return !HasDefine();
        }

        private static void SetDisabled(bool disabled)
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(group);
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            List<string> defines = new List<string>(
                current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            bool changed = disabled ? Add(defines, DisableDefine) : defines.Remove(DisableDefine);
            if (!changed)
                return;

            PlayerSettings.SetScriptingDefineSymbols(target, defines.ToArray());
            UnityEngine.Debug.Log(
                disabled
                    ? "HeliosDebugger runtime disabled for the selected build target."
                    : "HeliosDebugger runtime enabled for the selected build target.");
        }

        private static bool HasDefine()
        {
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup);
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            string[] defines = current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < defines.Length; i++)
            {
                if (string.Equals(defines[i], DisableDefine, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool Add(List<string> values, string value)
        {
            if (values.Contains(value))
                return false;
            values.Add(value);
            return true;
        }
    }
}
