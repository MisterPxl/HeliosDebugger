using System;

namespace HeliosDebugger
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class HeliosOptionsAttribute : Attribute
    {
        public HeliosOptionsAttribute(string category = null)
        {
            Category = category;
        }

        public string Category { get; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class HeliosOptionAttribute : Attribute
    {
        public HeliosOptionAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public bool ReadOnly { get; set; }
        public bool Persist { get; set; }
        public bool Pin { get; set; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    public sealed class HeliosActionAttribute : Attribute
    {
        public HeliosActionAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }

        public string DisplayName { get; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int Order { get; set; }
        public bool Pin { get; set; }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Parameter, Inherited = true)]
    public sealed class HeliosRangeAttribute : Attribute
    {
        public HeliosRangeAttribute(float min, float max, float step = 1f)
        {
            Min = min;
            Max = max;
            Step = step;
        }

        public float Min { get; }
        public float Max { get; }
        public float Step { get; }
    }
}
