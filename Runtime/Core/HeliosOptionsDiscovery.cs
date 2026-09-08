using System;
using System.Collections.Generic;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public static class HeliosGeneratedOptions
    {
        private static readonly List<Type> RegisteredTypes = new List<Type>();

        public static IReadOnlyList<Type> Types => RegisteredTypes;
        public static event Action Changed;

        public static void Register(Type type)
        {
            if (type == null || RegisteredTypes.Contains(type))
                return;
            RegisteredTypes.Add(type);
            Changed?.Invoke();
        }
    }
}
