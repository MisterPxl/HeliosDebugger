using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;

namespace HeliosDebugger.Editor
{
    public static class HeliosDebuggerDefineMenu
    {
        private const string DisableDefine = "HELIOS_DEBUGGER_DISABLE";

        [MenuItem("Tools/Astra/Helios/Compilation/Enable Runtime (All Targets)")]
        public static void EnableRuntime()
        {
            SetDisabled(false);
        }

        [MenuItem("Tools/Astra/Helios/Compilation/Disable Runtime (All Targets)")]
        public static void DisableRuntime()
        {
            SetDisabled(true);
        }

        [MenuItem("Tools/Astra/Helios/Compilation/Enable Runtime (All Targets)", true)]
        private static bool ValidateEnableRuntime()
        {
            foreach (NamedBuildTarget target in EnumerateTargets())
            {
                if (HasDefine(target))
                    return true;
            }
            return false;
        }

        [MenuItem("Tools/Astra/Helios/Compilation/Disable Runtime (All Targets)", true)]
        private static bool ValidateDisableRuntime()
        {
            foreach (NamedBuildTarget target in EnumerateTargets())
            {
                if (!HasDefine(target))
                    return true;
            }
            return false;
        }

        private static void SetDisabled(bool disabled)
        {
            List<string> changedTargets = new List<string>();
            foreach (NamedBuildTarget target in EnumerateTargets())
            {
                string current = PlayerSettings.GetScriptingDefineSymbols(target);
                List<string> defines = new List<string>(
                    current.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                bool changed = disabled ? Add(defines, DisableDefine) : defines.Remove(DisableDefine);
                if (!changed)
                    continue;

                PlayerSettings.SetScriptingDefineSymbols(target, defines.ToArray());
                changedTargets.Add(target.TargetName);
            }

            if (changedTargets.Count == 0)
                return;

            UnityEngine.Debug.Log(
                (disabled
                    ? "HeliosDebugger runtime disabled for build targets: "
                    : "HeliosDebugger runtime enabled for build targets: ") +
                string.Join(", ", changedTargets) + ".");
        }

        /// <summary>
        /// Every valid named build target, including the dedicated server,
        /// so the define cannot silently stay applied to a single target.
        /// </summary>
        private static IEnumerable<NamedBuildTarget> EnumerateTargets()
        {
            HashSet<NamedBuildTarget> seen = new HashSet<NamedBuildTarget>();
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (group == BuildTargetGroup.Unknown || IsObsolete(group))
                    continue;

                NamedBuildTarget target;
                try
                {
                    target = NamedBuildTarget.FromBuildTargetGroup(group);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (seen.Add(target))
                    yield return target;
            }

            if (seen.Add(NamedBuildTarget.Server))
                yield return NamedBuildTarget.Server;
        }

        private static bool IsObsolete(BuildTargetGroup group)
        {
            FieldInfo field = typeof(BuildTargetGroup).GetField(group.ToString());
            return field == null || field.IsDefined(typeof(ObsoleteAttribute), false);
        }

        private static bool HasDefine(NamedBuildTarget target)
        {
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
