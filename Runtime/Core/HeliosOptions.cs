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

    public sealed class HeliosOptionsRegistry : IDisposable
    {
        private readonly List<object> _instances = new List<object>();
        private readonly List<HeliosOptionMember> _reflectedOptions = new List<HeliosOptionMember>();
        private readonly List<HeliosReflectedAction> _reflectedActions = new List<HeliosReflectedAction>();
        private readonly List<IHeliosValueOption> _options = new List<IHeliosValueOption>();
        private readonly List<IHeliosActionOption> _actions = new List<IHeliosActionOption>();
        private readonly List<ContainerRegistration> _containers = new List<ContainerRegistration>();
        private HeliosDynamicOptionContainer _directContainer;
        private bool _scanned;
        private int _revision;

        public event Action Changed;

        public HeliosOptionsRegistry()
        {
            HeliosGeneratedOptions.Changed += OnGeneratedOptionsChanged;
        }

        public int Revision
        {
            get
            {
                EnsureScanned();
                return _revision;
            }
        }

        public IReadOnlyList<IHeliosValueOption> Options
        {
            get
            {
                EnsureScanned();
                return _options;
            }
        }

        public IReadOnlyList<IHeliosActionOption> Actions
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
            MarkChanged();
        }

        public bool UnregisterInstance(object instance)
        {
            if (instance == null || !_instances.Remove(instance))
                return false;

            _scanned = false;
            MarkChanged();
            return true;
        }

        public void RegisterStaticType(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            HeliosGeneratedOptions.Register(type);
            _scanned = false;
            MarkChanged();
        }

        public void RegisterOptionContainer(IHeliosOptionContainer container)
        {
            if (container == null || FindContainer(container) != null)
                return;

            ContainerRegistration registration = new ContainerRegistration(container);
            _containers.Add(registration);
            ImportContainer(registration);
            if (container.IsDynamic)
                Subscribe(registration);

            RebuildSnapshots();
            MarkChanged();
        }

        public bool UnregisterOptionContainer(IHeliosOptionContainer container)
        {
            ContainerRegistration registration = FindContainer(container);
            if (registration == null)
                return false;

            Unsubscribe(registration);
            _containers.Remove(registration);
            RebuildSnapshots();
            MarkChanged();
            return true;
        }

        public void AddOption(IHeliosValueOption option)
        {
            if (option == null)
                return;

            EnsureDirectContainer().AddOption(option);
        }

        public bool RemoveOption(IHeliosValueOption option)
        {
            return _directContainer != null && _directContainer.RemoveOption(option);
        }

        public void AddOption(IHeliosActionOption action)
        {
            if (action == null)
                return;

            EnsureDirectContainer().AddAction(action);
        }

        public bool RemoveOption(IHeliosActionOption action)
        {
            return _directContainer != null && _directContainer.RemoveAction(action);
        }

        public void Refresh()
        {
            _scanned = false;
            EnsureScanned();
            MarkChanged();
        }

        public void Dispose()
        {
            HeliosGeneratedOptions.Changed -= OnGeneratedOptionsChanged;
            for (int i = 0; i < _containers.Count; i++)
                Unsubscribe(_containers[i]);

            _containers.Clear();
            _instances.Clear();
            _reflectedOptions.Clear();
            _reflectedActions.Clear();
            _options.Clear();
            _actions.Clear();
            _directContainer = null;
            _scanned = false;
        }

        private void OnGeneratedOptionsChanged()
        {
            _scanned = false;
            MarkChanged();
        }

        private void EnsureScanned()
        {
            if (_scanned)
                return;

            _reflectedOptions.Clear();
            _reflectedActions.Clear();

            HashSet<Type> scannedTypes = new HashSet<Type>();
            IReadOnlyList<Type> generatedTypes = HeliosGeneratedOptions.Types;
            for (int typeIndex = 0; typeIndex < generatedTypes.Count; typeIndex++)
                ScanStaticType(generatedTypes[typeIndex], scannedTypes);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types = GetTypes(assemblies[assemblyIndex]);
                for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
                    ScanStaticType(types[typeIndex], scannedTypes);
            }
#endif

            for (int i = 0; i < _instances.Count; i++)
            {
                object instance = _instances[i];
                Type type = instance.GetType();
                HeliosOptionsAttribute optionsAttribute = type.GetCustomAttribute<HeliosOptionsAttribute>() ?? new HeliosOptionsAttribute(type.Name);
                ScanType(type, instance, optionsAttribute);
            }

            ApplyPersistedValues();
            _scanned = true;
            RebuildSnapshots();
        }

        private void ScanStaticType(Type type, HashSet<Type> scannedTypes)
        {
            if (type == null || !scannedTypes.Add(type))
                return;

            HeliosOptionsAttribute optionsAttribute = type.GetCustomAttribute<HeliosOptionsAttribute>();
            if (optionsAttribute != null)
                ScanType(type, null, optionsAttribute);
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

                _reflectedOptions.Add(HeliosOptionMember.FromField(type, target, field, attribute, typeAttribute));
            }

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                HeliosOptionAttribute attribute = property.GetCustomAttribute<HeliosOptionAttribute>();
                if (attribute == null)
                    continue;

                MethodInfo getter = property.GetGetMethod(true);
                if (getter == null)
                    continue;

                bool isStatic = getter.IsStatic;
                object target = isStatic ? null : instance;
                if (!isStatic && target == null)
                    continue;

                _reflectedOptions.Add(HeliosOptionMember.FromProperty(type, target, property, attribute, typeAttribute));
            }

            MethodInfo[] methods = type.GetMethods(flags);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                HeliosActionAttribute attribute = method.GetCustomAttribute<HeliosActionAttribute>();
                if (attribute == null || !HeliosReflectedAction.Supports(method))
                    continue;

                object target = method.IsStatic ? null : instance;
                if (!method.IsStatic && target == null)
                    continue;

                _reflectedActions.Add(new HeliosReflectedAction(type, target, method, attribute, typeAttribute));
            }
        }

        private void ApplyPersistedValues()
        {
            for (int i = 0; i < _reflectedOptions.Count; i++)
            {
                HeliosOptionMember option = _reflectedOptions[i];
                if (!option.Persist)
                    continue;

                string key = option.PersistenceKey;
                if (!PlayerPrefs.HasKey(key))
                    continue;

                option.TrySetFromString(PlayerPrefs.GetString(key));
            }
        }

        private void RebuildSnapshots()
        {
            _options.Clear();
            _actions.Clear();

            for (int i = 0; i < _reflectedOptions.Count; i++)
                _options.Add(_reflectedOptions[i]);
            for (int i = 0; i < _reflectedActions.Count; i++)
                _actions.Add(_reflectedActions[i]);
            for (int i = 0; i < _containers.Count; i++)
            {
                ContainerRegistration registration = _containers[i];
                for (int optionIndex = 0; optionIndex < registration.Options.Count; optionIndex++)
                {
                    if (!_options.Contains(registration.Options[optionIndex]))
                        _options.Add(registration.Options[optionIndex]);
                }
                for (int actionIndex = 0; actionIndex < registration.Actions.Count; actionIndex++)
                {
                    if (!_actions.Contains(registration.Actions[actionIndex]))
                        _actions.Add(registration.Actions[actionIndex]);
                }
            }

            _options.Sort(CompareOptions);
            _actions.Sort(CompareActions);
        }

        private void ImportContainer(ContainerRegistration registration)
        {
            IEnumerable<IHeliosValueOption> options = registration.Container.GetOptions();
            if (options != null)
            {
                foreach (IHeliosValueOption option in options)
                {
                    if (option != null && !registration.Options.Contains(option))
                        registration.Options.Add(option);
                }
            }

            IEnumerable<IHeliosActionOption> actions = registration.Container.GetActions();
            if (actions != null)
            {
                foreach (IHeliosActionOption action in actions)
                {
                    if (action != null && !registration.Actions.Contains(action))
                        registration.Actions.Add(action);
                }
            }
        }

        private void Subscribe(ContainerRegistration registration)
        {
            registration.OptionAdded = option => OnContainerOptionAdded(registration, option);
            registration.OptionRemoved = option => OnContainerOptionRemoved(registration, option);
            registration.ActionAdded = action => OnContainerActionAdded(registration, action);
            registration.ActionRemoved = action => OnContainerActionRemoved(registration, action);
            registration.Container.OptionAdded += registration.OptionAdded;
            registration.Container.OptionRemoved += registration.OptionRemoved;
            registration.Container.ActionAdded += registration.ActionAdded;
            registration.Container.ActionRemoved += registration.ActionRemoved;
        }

        private void Unsubscribe(ContainerRegistration registration)
        {
            if (registration.OptionAdded != null)
                registration.Container.OptionAdded -= registration.OptionAdded;
            if (registration.OptionRemoved != null)
                registration.Container.OptionRemoved -= registration.OptionRemoved;
            if (registration.ActionAdded != null)
                registration.Container.ActionAdded -= registration.ActionAdded;
            if (registration.ActionRemoved != null)
                registration.Container.ActionRemoved -= registration.ActionRemoved;

            registration.OptionAdded = null;
            registration.OptionRemoved = null;
            registration.ActionAdded = null;
            registration.ActionRemoved = null;
        }

        private void OnContainerOptionAdded(ContainerRegistration registration, IHeliosValueOption option)
        {
            if (option == null || registration.Options.Contains(option))
                return;

            registration.Options.Add(option);
            RebuildSnapshots();
            MarkChanged();
        }

        private void OnContainerOptionRemoved(ContainerRegistration registration, IHeliosValueOption option)
        {
            if (option == null || !registration.Options.Remove(option))
                return;

            RebuildSnapshots();
            MarkChanged();
        }

        private void OnContainerActionAdded(ContainerRegistration registration, IHeliosActionOption action)
        {
            if (action == null || registration.Actions.Contains(action))
                return;

            registration.Actions.Add(action);
            RebuildSnapshots();
            MarkChanged();
        }

        private void OnContainerActionRemoved(ContainerRegistration registration, IHeliosActionOption action)
        {
            if (action == null || !registration.Actions.Remove(action))
                return;

            RebuildSnapshots();
            MarkChanged();
        }

        private HeliosDynamicOptionContainer EnsureDirectContainer()
        {
            if (_directContainer == null)
            {
                _directContainer = new HeliosDynamicOptionContainer();
                RegisterOptionContainer(_directContainer);
            }

            return _directContainer;
        }

        private ContainerRegistration FindContainer(IHeliosOptionContainer container)
        {
            for (int i = 0; i < _containers.Count; i++)
            {
                if (ReferenceEquals(_containers[i].Container, container))
                    return _containers[i];
            }

            return null;
        }

        private void MarkChanged()
        {
            _revision++;
            Changed?.Invoke();
        }

        private static int CompareOptions(IHeliosValueOption left, IHeliosValueOption right)
        {
            int category = string.Compare(NormalizeCategory(left.Category), NormalizeCategory(right.Category), StringComparison.Ordinal);
            if (category != 0)
                return category;
            if (left.Order != right.Order)
                return left.Order.CompareTo(right.Order);
            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
        }

        private static int CompareActions(IHeliosActionOption left, IHeliosActionOption right)
        {
            int category = string.Compare(NormalizeCategory(left.Category), NormalizeCategory(right.Category), StringComparison.Ordinal);
            if (category != 0)
                return category;
            if (left.Order != right.Order)
                return left.Order.CompareTo(right.Order);
            return string.Compare(left.DisplayName, right.DisplayName, StringComparison.Ordinal);
        }

        private static string NormalizeCategory(string category)
        {
            return string.IsNullOrEmpty(category) ? "General" : category;
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

        private sealed class ContainerRegistration
        {
            public readonly IHeliosOptionContainer Container;
            public readonly List<IHeliosValueOption> Options = new List<IHeliosValueOption>();
            public readonly List<IHeliosActionOption> Actions = new List<IHeliosActionOption>();
            public Action<IHeliosValueOption> OptionAdded;
            public Action<IHeliosValueOption> OptionRemoved;
            public Action<IHeliosActionOption> ActionAdded;
            public Action<IHeliosActionOption> ActionRemoved;

            public ContainerRegistration(IHeliosOptionContainer container)
            {
                Container = container;
            }
        }
    }

    public sealed class HeliosOptionMember : IHeliosValueOption
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
            MemberName = field != null ? field.Name : property.Name;
            DisplayName = FirstNonEmpty(attribute.DisplayName, MemberName);
            Description = attribute.Description ?? string.Empty;
            Order = attribute.Order;
            Persist = attribute.Persist;
            Pin = attribute.Pin;
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
        public string MemberName { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public bool Persist { get; }
        public bool Pin { get; }
        public bool IsReadOnly { get; }
        public Type ValueType { get; }
        public HeliosOptionValueKind ValueKind { get; }
        public HeliosRangeAttribute Range { get; }
        // Keyed on the member name rather than the display name: display names
        // are editable labels and two options in one type may share one.
        public string PersistenceKey => $"HeliosOption.{DeclaringType.FullName}.{MemberName}";

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
            return HeliosOptionValueConverter.Format(GetValue(), ValueType);
        }

        public bool TrySetFromString(string text)
        {
            if (IsReadOnly)
                return false;

            try
            {
                object converted = HeliosOptionValueConverter.ConvertFromString(text, ValueType);
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
                decimal amount = decimal.Round(
                    Convert.ToDecimal(step * direction, CultureInfo.InvariantCulture),
                    0,
                    MidpointRounding.ToEven);
                decimal? minimum = Range != null
                    ? decimal.Round(Convert.ToDecimal(Range.Min, CultureInfo.InvariantCulture), 0, MidpointRounding.ToEven)
                    : (decimal?)null;
                decimal? maximum = Range != null
                    ? decimal.Round(Convert.ToDecimal(Range.Max, CultureInfo.InvariantCulture), 0, MidpointRounding.ToEven)
                    : (decimal?)null;
                SetValue(HeliosOptionValueConverter.AdjustInteger(value, ValueType, amount, minimum, maximum));
            }
            else if (ValueKind == HeliosOptionValueKind.Float)
            {
                double next = Convert.ToDouble(value, CultureInfo.InvariantCulture) + step * direction;
                if (Range != null)
                    next = Math.Max(Range.Min, Math.Min(Range.Max, next));
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
            if (values.Length == 0)
                return;

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

        private void PersistIfNeeded()
        {
            if (!Persist)
                return;

            PlayerPrefs.SetString(PersistenceKey, GetDisplayValue());
            HeliosPersistence.MarkDirty();
        }

        public static HeliosOptionValueKind GetValueKind(Type type)
        {
            return HeliosOptionValueConverter.GetValueKind(type);
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

    public sealed class HeliosReflectedAction : IHeliosActionOption
    {
        private readonly object _target;
        private readonly MethodInfo _method;
        private readonly List<HeliosActionParameter> _parameters = new List<HeliosActionParameter>();

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

            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
                _parameters.Add(new HeliosActionParameter(parameters[i]));
        }

        public Type DeclaringType { get; }
        public string Category { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int Order { get; }
        public bool Pin { get; }
        public IReadOnlyList<HeliosActionParameter> Parameters => _parameters;

        public HeliosActionResult Invoke()
        {
            return Invoke(Array.Empty<string>());
        }

        public HeliosActionResult Invoke(IReadOnlyList<string> parameterValues)
        {
            try
            {
                object[] arguments = BuildArguments(parameterValues);
                _method.Invoke(_target, arguments);
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

        public static bool Supports(MethodInfo method)
        {
            if (method == null)
                return false;

            ParameterInfo[] parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                if (HeliosActionParameter.GetValueKind(parameters[i].ParameterType) == HeliosOptionValueKind.Unsupported)
                    return false;
            }

            return true;
        }

        private object[] BuildArguments(IReadOnlyList<string> parameterValues)
        {
            if (_parameters.Count == 0)
                return null;

            object[] arguments = new object[_parameters.Count];
            for (int i = 0; i < _parameters.Count; i++)
            {
                string rawValue = parameterValues != null && i < parameterValues.Count
                    ? parameterValues[i]
                    : _parameters[i].DefaultText;
                arguments[i] = _parameters[i].ConvertFromString(rawValue);
            }

            return arguments;
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

    public sealed class HeliosActionParameter
    {
        private readonly ParameterInfo _parameter;

        public HeliosActionParameter(ParameterInfo parameter)
        {
            _parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
            Name = string.IsNullOrWhiteSpace(parameter.Name) ? $"Parameter{parameter.Position}" : parameter.Name;
            ParameterType = parameter.ParameterType;
            ValueKind = GetValueKind(ParameterType);
            Range = parameter.GetCustomAttribute<HeliosRangeAttribute>();
            DefaultText = BuildDefaultText(parameter, ValueKind);
        }

        public string Name { get; }
        public Type ParameterType { get; }
        public HeliosOptionValueKind ValueKind { get; }
        public HeliosRangeAttribute Range { get; }
        public string DefaultText { get; }

        public object ConvertFromString(string text)
        {
            string value = text ?? string.Empty;
            object converted = HeliosOptionValueConverter.ConvertFromString(value, ParameterType);
            if (Range == null)
                return converted;

            if (ValueKind == HeliosOptionValueKind.Integer)
            {
                decimal minimum = decimal.Round(Convert.ToDecimal(Range.Min, CultureInfo.InvariantCulture), 0, MidpointRounding.ToEven);
                decimal maximum = decimal.Round(Convert.ToDecimal(Range.Max, CultureInfo.InvariantCulture), 0, MidpointRounding.ToEven);
                return HeliosOptionValueConverter.AdjustInteger(converted, ParameterType, 0m, minimum, maximum);
            }
            if (ValueKind == HeliosOptionValueKind.Float)
            {
                double parsed = Convert.ToDouble(converted, CultureInfo.InvariantCulture);
                double clamped = Math.Max(Range.Min, Math.Min(Range.Max, parsed));
                return Convert.ChangeType(clamped, ParameterType, CultureInfo.InvariantCulture);
            }

            return converted;
        }

        public static HeliosOptionValueKind GetValueKind(Type type)
        {
            return HeliosOptionValueConverter.GetValueKind(type);
        }

        private static string BuildDefaultText(ParameterInfo parameter, HeliosOptionValueKind kind)
        {
            if (parameter.HasDefaultValue && parameter.DefaultValue != null)
                return HeliosOptionValueConverter.Format(parameter.DefaultValue, parameter.ParameterType);

            switch (kind)
            {
                case HeliosOptionValueKind.Boolean:
                    return "false";
                case HeliosOptionValueKind.Integer:
                case HeliosOptionValueKind.Float:
                    return "0";
                case HeliosOptionValueKind.String:
                    return string.Empty;
                case HeliosOptionValueKind.Enum:
                    Array values = Enum.GetValues(parameter.ParameterType);
                    return values.Length > 0 ? values.GetValue(0).ToString() : string.Empty;
                case HeliosOptionValueKind.Vector2:
                    return "0,0";
                case HeliosOptionValueKind.Vector3:
                    return "0,0,0";
                case HeliosOptionValueKind.Color:
                    return "0,0,0,1";
                default:
                    return string.Empty;
            }
        }
    }
}
