using System;
using System.Collections.Generic;

namespace HeliosDebugger
{
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
