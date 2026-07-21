using System;
using System.Collections.Generic;
using System.Globalization;

namespace HeliosDebugger
{
    public interface IHeliosValueOption
    {
        string Category { get; }
        string DisplayName { get; }
        string Description { get; }
        int Order { get; }
        bool IsReadOnly { get; }
        Type ValueType { get; }
        HeliosOptionValueKind ValueKind { get; }
        HeliosRangeAttribute Range { get; }
        object GetValue();
        string GetDisplayValue();
        bool TrySetFromString(string text);
        void Reset();
        void Adjust(float direction);
        void ToggleBoolean();
        void CycleEnum();
    }

    public interface IHeliosActionOption
    {
        string Category { get; }
        string DisplayName { get; }
        string Description { get; }
        int Order { get; }
        bool Pin { get; }
        IReadOnlyList<HeliosActionParameter> Parameters { get; }
        HeliosActionResult Invoke();
        HeliosActionResult Invoke(IReadOnlyList<string> parameterValues);
    }

    public interface IHeliosOptionContainer
    {
        IEnumerable<IHeliosValueOption> GetOptions();
        IEnumerable<IHeliosActionOption> GetActions();
        bool IsDynamic { get; }
        event Action<IHeliosValueOption> OptionAdded;
        event Action<IHeliosValueOption> OptionRemoved;
        event Action<IHeliosActionOption> ActionAdded;
        event Action<IHeliosActionOption> ActionRemoved;
    }

    public static class HeliosOptionDefinition
    {
        public static HeliosOptionDefinition<T> Create<T>(
            string displayName,
            Func<T> getter,
            Action<T> setter = null,
            string category = "General",
            int order = 0)
        {
            return new HeliosOptionDefinition<T>(displayName, getter, setter, category, null, order);
        }

        public static HeliosDynamicActionDefinition FromMethod(
            string displayName,
            Action execute,
            string category = "General",
            int order = 0)
        {
            return new HeliosDynamicActionDefinition(displayName, execute, category, null, order);
        }
    }

    public sealed class HeliosOptionDefinition<T> : IHeliosValueOption
    {
        private readonly Func<T> _getter;
        private readonly Action<T> _setter;
        private readonly T _initialValue;

        public HeliosOptionDefinition(
            string displayName,
            Func<T> getter,
            Action<T> setter = null,
            string category = "General",
            string description = null,
            int order = 0,
            HeliosRangeAttribute range = null)
        {
            if (getter == null)
                throw new ArgumentNullException(nameof(getter));

            ValueType = typeof(T);
            ValueKind = HeliosOptionMember.GetValueKind(ValueType);
            if (!IsSupportedValueKind(ValueKind))
                throw new NotSupportedException($"Unsupported dynamic option type: {ValueType.Name}");

            DisplayName = FirstNonEmpty(displayName, ValueType.Name);
            Category = FirstNonEmpty(category, "General");
            Description = description ?? string.Empty;
            Order = order;
            Range = range;
            _getter = getter;
            _setter = setter;
            _initialValue = getter();
        }

        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public bool IsReadOnly => _setter == null;
        public Type ValueType { get; }
        public HeliosOptionValueKind ValueKind { get; }
        public HeliosRangeAttribute Range { get; }

        public object GetValue()
        {
            return _getter();
        }

        public string GetDisplayValue()
        {
            object value = GetValue();
            return value == null ? "<null>" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        public bool TrySetFromString(string text)
        {
            if (IsReadOnly)
                return false;

            try
            {
                object converted = ConvertFromString(text);
                SetValue((T)converted);
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Helios dynamic option '{DisplayName}' rejected value '{text}': {ex.Message}");
                return false;
            }
        }

        public void Reset()
        {
            if (!IsReadOnly)
                SetValue(_initialValue);
        }

        public void Adjust(float direction)
        {
            if (IsReadOnly)
                return;

            if (ValueKind == HeliosOptionValueKind.Integer)
            {
                long current = Convert.ToInt64(GetValue(), CultureInfo.InvariantCulture);
                long step = Math.Max(1L, (long)Math.Round(Math.Abs(Range != null ? Range.Step : 1f)));
                long next = current + (direction < 0f ? -step : step);
                if (Range != null)
                    next = Math.Max((long)Math.Round(Range.Min), Math.Min((long)Math.Round(Range.Max), next));
                SetValue((T)Convert.ChangeType(next, ValueType, CultureInfo.InvariantCulture));
            }
            else if (ValueKind == HeliosOptionValueKind.Float)
            {
                double current = Convert.ToDouble(GetValue(), CultureInfo.InvariantCulture);
                double step = (Range != null ? Range.Step : 1f) * direction;
                double next = current + step;
                if (Range != null)
                    next = Math.Max(Range.Min, Math.Min(Range.Max, next));
                SetValue((T)Convert.ChangeType(next, ValueType, CultureInfo.InvariantCulture));
            }
        }

        public void ToggleBoolean()
        {
            if (!IsReadOnly && ValueKind == HeliosOptionValueKind.Boolean)
                SetValue((T)(object)!(bool)GetValue());
        }

        public void CycleEnum()
        {
            if (IsReadOnly || ValueKind != HeliosOptionValueKind.Enum)
                return;

            Array values = Enum.GetValues(ValueType);
            object current = GetValue();
            int index = Array.IndexOf(values, current);
            object next = values.GetValue((index + 1) % values.Length);
            SetValue((T)next);
        }

        private void SetValue(T value)
        {
            _setter(value);
        }

        private object ConvertFromString(string text)
        {
            string value = text ?? string.Empty;
            switch (ValueKind)
            {
                case HeliosOptionValueKind.Boolean:
                    return bool.Parse(value);
                case HeliosOptionValueKind.Integer:
                    return Convert.ChangeType(long.Parse(value, CultureInfo.InvariantCulture), ValueType, CultureInfo.InvariantCulture);
                case HeliosOptionValueKind.Float:
                    return Convert.ChangeType(double.Parse(value, CultureInfo.InvariantCulture), ValueType, CultureInfo.InvariantCulture);
                case HeliosOptionValueKind.String:
                    return value;
                case HeliosOptionValueKind.Enum:
                    return Enum.Parse(ValueType, value, true);
                default:
                    throw new NotSupportedException($"Unsupported dynamic option type: {ValueType.Name}");
            }
        }

        private static bool IsSupportedValueKind(HeliosOptionValueKind kind)
        {
            switch (kind)
            {
                case HeliosOptionValueKind.Boolean:
                case HeliosOptionValueKind.Integer:
                case HeliosOptionValueKind.Float:
                case HeliosOptionValueKind.String:
                case HeliosOptionValueKind.Enum:
                    return true;
                default:
                    return false;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                    return values[i];
            }

            return string.Empty;
        }
    }

    public sealed class HeliosDynamicActionDefinition : IHeliosActionOption
    {
        private static readonly HeliosActionParameter[] NoParameters = new HeliosActionParameter[0];
        private readonly Action _execute;

        public HeliosDynamicActionDefinition(
            string displayName,
            Action execute,
            string category = "General",
            string description = null,
            int order = 0,
            bool pin = false)
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Action" : displayName;
            Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
            Description = description ?? string.Empty;
            Order = order;
            Pin = pin;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public bool Pin { get; }
        public IReadOnlyList<HeliosActionParameter> Parameters => NoParameters;

        public HeliosActionResult Invoke()
        {
            try
            {
                _execute();
                return HeliosActionResult.Succeed($"Executed {DisplayName}.");
            }
            catch (Exception ex)
            {
                return HeliosActionResult.Fail(ex.Message, ex);
            }
        }

        public HeliosActionResult Invoke(IReadOnlyList<string> parameterValues)
        {
            return Invoke();
        }
    }

    public sealed class HeliosDynamicOptionContainer : IHeliosOptionContainer
    {
        private readonly List<IHeliosValueOption> _options = new List<IHeliosValueOption>();
        private readonly List<IHeliosActionOption> _actions = new List<IHeliosActionOption>();

        public bool IsDynamic => true;

        public event Action<IHeliosValueOption> OptionAdded;
        public event Action<IHeliosValueOption> OptionRemoved;
        public event Action<IHeliosActionOption> ActionAdded;
        public event Action<IHeliosActionOption> ActionRemoved;

        public IEnumerable<IHeliosValueOption> GetOptions()
        {
            return _options;
        }

        public IEnumerable<IHeliosActionOption> GetActions()
        {
            return _actions;
        }

        public bool AddOption(IHeliosValueOption option)
        {
            if (option == null || _options.Contains(option))
                return false;

            _options.Add(option);
            OptionAdded?.Invoke(option);
            return true;
        }

        public HeliosOptionDefinition<T> AddOption<T>(
            string displayName,
            Func<T> getter,
            Action<T> setter = null,
            string category = "General",
            int order = 0)
        {
            HeliosOptionDefinition<T> option = new HeliosOptionDefinition<T>(displayName, getter, setter, category, null, order);
            AddOption(option);
            return option;
        }

        public bool RemoveOption(IHeliosValueOption option)
        {
            if (option == null || !_options.Remove(option))
                return false;

            OptionRemoved?.Invoke(option);
            return true;
        }

        public bool AddAction(IHeliosActionOption action)
        {
            if (action == null || _actions.Contains(action))
                return false;

            _actions.Add(action);
            ActionAdded?.Invoke(action);
            return true;
        }

        public HeliosDynamicActionDefinition AddAction(
            string displayName,
            Action execute,
            string category = "General",
            int order = 0)
        {
            HeliosDynamicActionDefinition action = new HeliosDynamicActionDefinition(displayName, execute, category, null, order);
            AddAction(action);
            return action;
        }

        public bool RemoveAction(IHeliosActionOption action)
        {
            if (action == null || !_actions.Remove(action))
                return false;

            ActionRemoved?.Invoke(action);
            return true;
        }
    }
}
