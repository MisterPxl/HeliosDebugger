using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Astra.Helios
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public sealed class HeliosOptionControlContext
    {
        private readonly Action<Action> _registerRefresh;
        private readonly Func<bool> _canEdit;

        public HeliosOptionControlContext(
            HeliosWidgetFactory widgets,
            Transform parent,
            Action<Action> registerRefresh)
            : this(widgets, parent, registerRefresh, null)
        {
        }

        public HeliosOptionControlContext(
            HeliosWidgetFactory widgets,
            Transform parent,
            Action<Action> registerRefresh,
            Func<bool> canEdit)
        {
            Widgets = widgets;
            Parent = parent;
            _registerRefresh = registerRefresh;
            _canEdit = canEdit;
        }

        public HeliosWidgetFactory Widgets { get; }
        public Transform Parent { get; }
        /// <summary>Custom controls should check this immediately before changing an option.</summary>
        public bool CanEdit => _canEdit == null || _canEdit();

        public void RegisterRefresh(Action refresh)
        {
            if (refresh != null)
                _registerRefresh(refresh);
        }
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public interface IHeliosOptionControlBuilder
    {
        int Order { get; }
        bool CanBuild(IHeliosValueOption option);
        void Build(HeliosOptionControlContext context, IHeliosValueOption option);
    }

    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger", "HeliosDebugger.Runtime")]
    public static class HeliosBuiltInOptionControls
    {
        public static IEnumerable<IHeliosOptionControlBuilder> Create()
        {
            yield return new HeliosReadOnlyOptionControlBuilder();
            yield return new HeliosBooleanOptionControlBuilder();
            yield return new HeliosEnumOptionControlBuilder();
            yield return new HeliosNumericOptionControlBuilder();
            yield return new HeliosTextOptionControlBuilder();
            yield return new HeliosCompositeOptionControlBuilder();
            yield return new HeliosFallbackOptionControlBuilder();
        }
    }

    internal sealed class HeliosReadOnlyOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => 0;
        public bool CanBuild(IHeliosValueOption option) => option.IsReadOnly;

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            TextMeshProUGUI value = context.Widgets.CreateText("Value", context.Parent, option.GetDisplayValue(), 14, TextAnchor.MiddleRight);
            context.Widgets.AddLayout(value.gameObject, -1f, 32f);
            context.RegisterRefresh(() => value.text = option.GetDisplayValue());
        }
    }

    internal sealed class HeliosBooleanOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => 10;
        public bool CanBuild(IHeliosValueOption option) => option.ValueType == typeof(bool);

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            bool isOn = option.GetValue() is bool boolValue && boolValue;
            HeliosSwitchControl control = context.Widgets.CreateSwitch("Toggle", context.Parent, isOn, option.GetDisplayValue(), null);
            control.Button.onClick.AddListener(() =>
            {
                if (!context.CanEdit)
                    return;
                option.ToggleBoolean();
                control.SetValue(option.GetValue() is bool current && current, option.GetDisplayValue());
            });
            context.RegisterRefresh(() => control.SetValue(option.GetValue() is bool current && current, option.GetDisplayValue()));
        }
    }

    internal sealed class HeliosEnumOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => 20;
        public bool CanBuild(IHeliosValueOption option) => option.ValueType.IsEnum;

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            Button button = context.Widgets.CreateButton("Enum", context.Parent, option.GetDisplayValue(), null);
            TextMeshProUGUI label = HeliosWidgetFactory.GetButtonLabel(button);
            button.onClick.AddListener(() =>
            {
                if (!context.CanEdit)
                    return;
                option.CycleEnum();
                label.text = option.GetDisplayValue();
            });
            context.RegisterRefresh(() => label.text = option.GetDisplayValue());
        }
    }

    internal sealed class HeliosNumericOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => 30;
        public bool CanBuild(IHeliosValueOption option)
        {
            Type type = option.ValueType;
            return type == typeof(byte) || type == typeof(sbyte) ||
                   type == typeof(short) || type == typeof(ushort) ||
                   type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong) ||
                   type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            Button minus = context.Widgets.CreateButton("Minus", context.Parent, "-", () =>
            {
                if (context.CanEdit)
                    option.Adjust(-1f);
            });
            context.Widgets.AddLayout(minus.gameObject, 32f);
            minus.GetComponent<LayoutElement>().preferredWidth = 36f;
            TMP_InputField value = context.Widgets.CreateInput("Value", context.Parent, option.GetDisplayValue(), null);
            value.text = option.GetDisplayValue();
            value.onEndEdit.AddListener(text =>
            {
                if (!context.CanEdit)
                    return;
                option.TrySetFromString(text);
                value.text = option.GetDisplayValue();
            });
            context.Widgets.AddLayout(value.gameObject, -1f, 32f);
            Button plus = context.Widgets.CreateButton("Plus", context.Parent, "+", () =>
            {
                if (context.CanEdit)
                    option.Adjust(1f);
            });
            context.Widgets.AddLayout(plus.gameObject, 32f);
            plus.GetComponent<LayoutElement>().preferredWidth = 36f;
            context.RegisterRefresh(() =>
            {
                if (!value.isFocused)
                    value.text = option.GetDisplayValue();
            });
        }
    }

    internal sealed class HeliosTextOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => 40;
        public bool CanBuild(IHeliosValueOption option) => option.ValueType == typeof(string);

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            TMP_InputField input = context.Widgets.CreateInput("Value", context.Parent, option.GetDisplayValue(), null);
            input.text = option.GetDisplayValue();
            input.onEndEdit.AddListener(text =>
            {
                if (!context.CanEdit)
                    return;
                option.TrySetFromString(text);
                input.text = option.GetDisplayValue();
            });
            context.Widgets.AddLayout(input.gameObject, -1f, 32f);
            context.RegisterRefresh(() =>
            {
                if (!input.isFocused)
                    input.text = option.GetDisplayValue();
            });
        }
    }

    internal sealed class HeliosCompositeOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => 50;
        public bool CanBuild(IHeliosValueOption option)
        {
            return option.ValueType == typeof(Vector2) ||
                   option.ValueType == typeof(Vector3) ||
                   option.ValueType == typeof(Color);
        }

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            Image swatch = null;
            if (option.ValueType == typeof(Color))
            {
                GameObject swatchObject = context.Widgets.CreatePanel("Swatch", context.Parent,
                    option.GetValue() is Color color ? color : Color.clear);
                swatch = swatchObject.GetComponent<Image>();
                context.Widgets.AddLayout(swatchObject, 32f, 32f);
                swatchObject.GetComponent<LayoutElement>().preferredWidth = 32f;
            }

            TMP_InputField input = context.Widgets.CreateInput("Components", context.Parent, option.GetDisplayValue(), null);
            input.text = option.GetDisplayValue();
            input.onEndEdit.AddListener(text =>
            {
                if (!context.CanEdit)
                    return;
                option.TrySetFromString(text);
                Refresh(input, swatch, option);
            });
            context.Widgets.AddLayout(input.gameObject, -1f, 32f);
            context.RegisterRefresh(() => Refresh(input, swatch, option));
        }

        private static void Refresh(TMP_InputField input, Image swatch, IHeliosValueOption option)
        {
            if (!input.isFocused)
                input.text = option.GetDisplayValue();
            if (swatch != null)
                swatch.color = option.GetValue() is Color color ? color : Color.clear;
        }
    }

    internal sealed class HeliosFallbackOptionControlBuilder : IHeliosOptionControlBuilder
    {
        public int Order => int.MaxValue;
        public bool CanBuild(IHeliosValueOption option) => true;

        public void Build(HeliosOptionControlContext context, IHeliosValueOption option)
        {
            TextMeshProUGUI value = context.Widgets.CreateText("Unsupported", context.Parent, option.GetDisplayValue(), 14, TextAnchor.MiddleRight);
            context.Widgets.AddLayout(value.gameObject, -1f, 32f);
            context.RegisterRefresh(() => value.text = option.GetDisplayValue());
        }
    }
}
