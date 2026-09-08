using System;
using System.Reflection;

namespace Astra.Helios
{
    /// <summary>Stable identity for persisted tabs and reflected option keys when a consumer type is renamed.</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class HeliosTypeIdentityAttribute : Attribute
    {
        public string Id { get; }
        public HeliosTypeIdentityAttribute(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable type identity is required.", nameof(id));
            Id = id;
        }
        public static string GetId(Type type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            return type.GetCustomAttribute<HeliosTypeIdentityAttribute>(false)?.Id ?? type.FullName ?? type.Name;
        }
    }
}
