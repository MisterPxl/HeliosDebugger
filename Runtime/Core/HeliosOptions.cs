using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace HeliosDebugger
{
    public enum HeliosOptionValueKind
    {
        Unsupported,
        Boolean,
        Integer,
        Float,
        String,
        Enum,
        Vector2,
        Vector3,
        Color
    }

    public sealed class HeliosOptionsRegistry
    {
        private readonly List<object> _instances = new List<object>();
        private readonly List<HeliosOptionMember> _options = new List<HeliosOptionMember>();
        private readonly List<HeliosReflectedAction> _actions = new List<HeliosReflectedAction>();
        private bool _scanned;

        public IReadOnlyList<HeliosOptionMember> Options
        {
            get
            {
                EnsureScanned();
                return _options;
            }
        }

        public IReadOnlyList<HeliosReflectedAction> Actions
        {
            get
            {
                EnsureScanned();
                return _actions;
            }
        }

        public void RegisterInstance(object instance)
        {
            if (instance == null || _instances.Contains(instance))
                return;

            _instances.Add(instance);
            _scanned = false;
        }

        public void Refresh()
        {
            _scanned = false;
            EnsureScanned();
        }

        private void EnsureScanned()
        {
            if (_scanned)
                return;

            _options.Clear();
            _actions.Clear();

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types = GetTypes(assemblies[assemblyIndex]);
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    Type type = types[typeIndex];
                    if (type == null)
                        continue;

                    HeliosOptionsAttribute optionsAttribute = type.GetCustomAttribute<HeliosOptionsAttribute>();
                    if (optionsAttribute == null)
                        continue;

                    ScanType(type, null, optionsAttribute);
                }
            }

            for (int i = 0; i < _instances.Count; i++)
            {
                object instance = _instances[i];
                Type type = instance.GetType();
                HeliosOptionsAttribute optionsAttribute = type.GetCustomAttribute<HeliosOptionsAttribute>() ?? new HeliosOptionsAttribute(type.Name);
                ScanType(type, instance, optionsAttribute);
            }

            _options.Sort((left, right) => left.Order != right.Order
                ? left.Order.CompareTo(right.Order)
                : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));
            _actions.Sort((left, right) => left.Order != right.Order
                ? left.Order.CompareTo(right.Order)
                : string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal));

            ApplyPersistedValues();
            _scanned = true;
        }

        private void ScanType(Type type, object instance, HeliosOptionsAttribute typeAttribute)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                HeliosOptionAttribute attribute = field.GetCustomAttribute<HeliosOptionAttribute>();
                if (attribute == null)
                    continue;

                object target = field.IsStatic ? null : instance;
                if (!field.IsStatic && target == null)
                    continue;

                _options.Add(HeliosOptionMember.FromField(type, target, field, attribute, typeAttribute));
            }

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                HeliosOptionAttribute attribute = property.GetCustomAttribute<HeliosOptionAttribute>();
                if (attribute == null)
                    continue;

                MethodInfo getter = property.GetGetMethod(true);
                bool isStatic = getter != null && getter.IsStatic;
                object target = isStatic ? null : instance;
                if (!isStatic && target == null)
                    continue;

                _options.Add(HeliosOptionMember.FromProperty(type, target, property, attribute, typeAttribute));
            }

            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                HeliosActionAttribute attribute = method.GetCustomAttribute<HeliosActionAttribute>();
                if (attribute == null || method.GetParameters().Length > 0)
                    continue;

                object target = method.IsStatic ? null : instance;
                if (!method.IsStatic && target == null)
                    continue;

                _actions.Add(new HeliosReflectedAction(type, target, method, attribute, typeAttribute));
            }
        }

        private void ApplyPersistedValues()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                HeliosOptionMember option = _options[i];
                if (!option.Persist)
                    continue;

                string key = option.PersistenceKey;
                if (!PlayerPrefs.HasKey(key))
                    continue;

                option.TrySetFromString(PlayerPrefs.GetString(key));
            }
        }

        private static Type[] GetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null).ToArray();
            }
            catch (Exception)
            {
                return Array.Empty<Type>();
            }
        }
    }

    public sealed class HeliosOptionMember
    {
        private readonly object _target;
        private readonly FieldInfo _field;
        private readonly PropertyInfo _property;
        private readonly object _initialValue;

        private HeliosOptionMember(
            Type declaringType,
            object target,
            FieldInfo field,
            PropertyInfo property,
            HeliosOptionAttribute attribute,
            HeliosOptionsAttribute typeAttribute)
        {
            DeclaringType = declaringType;
            _target = target;
            _field = field;
            _property = property;
            Attribute = attribute;
            Category = FirstNonEmpty(attribute.Category, typeAttribute.Category, declaringType.Name);
            DisplayName = FirstNonEmpty(attribute.DisplayName, field != null ? field.Name : property.Name);
            Description = attribute.Description ?? string.Empty;
            Order = attribute.Order;
            Persist = attribute.Persist;
            ValueType = field != null ? field.FieldType : property.PropertyType;
            ValueKind = GetValueKind(ValueType);
            Range = field != null
                ? field.GetCustomAttribute<HeliosRangeAttribute>()
                : property.GetCustomAttribute<HeliosRangeAttribute>();
            IsReadOnly = attribute.ReadOnly || (property != null && property.GetSetMethod(true) == null);
            _initialValue = GetValue();
        }

        public Type DeclaringType { get; }
        public HeliosOptionAttribute Attribute { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public bool Persist { get; }
        public bool IsReadOnly { get; }
        public Type ValueType { get; }
        public HeliosOptionValueKind ValueKind { get; }
        public HeliosRangeAttribute Range { get; }
        public string PersistenceKey => $"HeliosOption.{DeclaringType.FullName}.{DisplayName}";

        public static HeliosOptionMember FromField(Type declaringType, object target, FieldInfo field, HeliosOptionAttribute attribute, HeliosOptionsAttribute typeAttribute)
        {
            return new HeliosOptionMember(declaringType, target, field, null, attribute, typeAttribute);
        }

        public static HeliosOptionMember FromProperty(Type declaringType, object target, PropertyInfo property, HeliosOptionAttribute attribute, HeliosOptionsAttribute typeAttribute)
        {
            return new HeliosOptionMember(declaringType, target, null, property, attribute, typeAttribute);
        }

        public object GetValue()
        {
            return _field != null ? _field.GetValue(_target) : _property.GetValue(_target, null);
        }

        public string GetDisplayValue()
        {
            object value = GetValue();
            return value == null ? "<null>" : value.ToString();
        }

        public bool TrySetFromString(string text)
        {
            if (IsReadOnly)
                return false;

            try
            {
                object converted = ConvertFromString(text);
                SetValue(converted);
                PersistIfNeeded();
                return true;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"Helios option '{DisplayName}' rejected value '{text}': {ex.Message}");
                return false;
            }
        }

        public void Reset()
        {
            if (IsReadOnly)
                return;

            SetValue(_initialValue);
            PersistIfNeeded();
        }

        public void Adjust(float direction)
        {
            if (IsReadOnly)
                return;

            object value = GetValue();
            float step = Range != null ? Range.Step : 1f;
            if (ValueKind == HeliosOptionValueKind.Integer)
            {
                int next = Convert.ToInt32(value, CultureInfo.InvariantCulture) + Mathf.RoundToInt(step * direction);
                if (Range != null)
                    next = Mathf.Clamp(next, Mathf.RoundToInt(Range.Min), Mathf.RoundToInt(Range.Max));
                SetValue(Convert.ChangeType(next, ValueType, CultureInfo.InvariantCulture));
            }
            else if (ValueKind == HeliosOptionValueKind.Float)
            {
                float next = Convert.ToSingle(value, CultureInfo.InvariantCulture) + step * direction;
                if (Range != null)
                    next = Mathf.Clamp(next, Range.Min, Range.Max);
                SetValue(Convert.ChangeType(next, ValueType, CultureInfo.InvariantCulture));
            }

            PersistIfNeeded();
        }

        public void ToggleBoolean()
        {
            if (!IsReadOnly && ValueKind == HeliosOptionValueKind.Boolean)
            {
                SetValue(!(bool)GetValue());
                PersistIfNeeded();
            }
        }

        public void CycleEnum()
        {
            if (IsReadOnly || ValueKind != HeliosOptionValueKind.Enum)
                return;

            Array values = Enum.GetValues(ValueType);
            object current = GetValue();
            int index = Array.IndexOf(values, current);
            object next = values.GetValue((index + 1) % values.Length);
            SetValue(next);
            PersistIfNeeded();
        }

        private void SetValue(object value)
        {
            if (_field != null)
                _field.SetValue(_target, value);
            else
                _property.SetValue(_target, value, null);
        }

        private object ConvertFromString(string text)
        {
            switch (ValueKind)
            {
                case HeliosOptionValueKind.Boolean:
                    return bool.Parse(text);
                case HeliosOptionValueKind.Integer:
                    return Convert.ChangeType(int.Parse(text, CultureInfo.InvariantCulture), ValueType, CultureInfo.InvariantCulture);
                case HeliosOptionValueKind.Float:
                    return Convert.ChangeType(float.Parse(text, CultureInfo.InvariantCulture), ValueType, CultureInfo.InvariantCulture);
                case HeliosOptionValueKind.String:
                    return text;
                case HeliosOptionValueKind.Enum:
                    return Enum.Parse(ValueType, text, true);
                default:
                    throw new NotSupportedException($"Unsupported option type: {ValueType.Name}");
            }
        }

        private void PersistIfNeeded()
        {
            if (!Persist)
                return;

            PlayerPrefs.SetString(PersistenceKey, GetDisplayValue());
            PlayerPrefs.Save();
        }

        private static HeliosOptionValueKind GetValueKind(Type type)
        {
            if (type == typeof(bool)) return HeliosOptionValueKind.Boolean;
            if (type == typeof(string)) return HeliosOptionValueKind.String;
            if (type.IsEnum) return HeliosOptionValueKind.Enum;
            if (type == typeof(Vector2)) return HeliosOptionValueKind.Vector2;
            if (type == typeof(Vector3)) return HeliosOptionValueKind.Vector3;
            if (type == typeof(Color)) return HeliosOptionValueKind.Color;
            if (type == typeof(float) || type == typeof(double)) return HeliosOptionValueKind.Float;
            if (type == typeof(byte) || type == typeof(short) || type == typeof(int) || type == typeof(long)) return HeliosOptionValueKind.Integer;
            return HeliosOptionValueKind.Unsupported;
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

    public sealed class HeliosReflectedAction
    {
        private readonly object _target;
        private readonly MethodInfo _method;

        public HeliosReflectedAction(Type declaringType, object target, MethodInfo method, HeliosActionAttribute attribute, HeliosOptionsAttribute typeAttribute)
        {
            DeclaringType = declaringType;
            _target = target;
            _method = method;
            Category = FirstNonEmpty(attribute.Category, typeAttribute.Category, declaringType.Name);
            DisplayName = FirstNonEmpty(attribute.DisplayName, method.Name);
            Description = attribute.Description ?? string.Empty;
            Order = attribute.Order;
            Pin = attribute.Pin;
        }

        public Type DeclaringType { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public bool Pin { get; }

        public HeliosActionResult Invoke()
        {
            try
            {
                _method.Invoke(_target, null);
                return HeliosActionResult.Succeed($"Executed {DisplayName}.");
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                return HeliosActionResult.Fail(inner.Message, inner);
            }
            catch (Exception ex)
            {
                return HeliosActionResult.Fail(ex.Message, ex);
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
}
