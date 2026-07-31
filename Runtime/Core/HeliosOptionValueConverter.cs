using System;
using System.Globalization;
using UnityEngine;

namespace HeliosDebugger
{
    public static class HeliosOptionValueConverter
    {
        private const NumberStyles IntegerStyles = NumberStyles.Integer;
        private const NumberStyles FloatStyles = NumberStyles.Float;

        public static HeliosOptionValueKind GetValueKind(Type type)
        {
            if (type == null)
                return HeliosOptionValueKind.Unsupported;
            if (type == typeof(bool))
                return HeliosOptionValueKind.Boolean;
            if (type == typeof(string))
                return HeliosOptionValueKind.String;
            if (type.IsEnum)
                return HeliosOptionValueKind.Enum;
            if (type == typeof(Vector2))
                return HeliosOptionValueKind.Vector2;
            if (type == typeof(Vector3))
                return HeliosOptionValueKind.Vector3;
            if (type == typeof(Color))
                return HeliosOptionValueKind.Color;
            if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
                return HeliosOptionValueKind.Float;
            if (IsIntegralType(type))
                return HeliosOptionValueKind.Integer;

            return HeliosOptionValueKind.Unsupported;
        }

        public static bool IsSupported(Type type)
        {
            return GetValueKind(type) != HeliosOptionValueKind.Unsupported;
        }

        public static bool TryConvertFromString(string text, Type targetType, out object value)
        {
            try
            {
                value = ConvertFromString(text, targetType);
                return true;
            }
            catch (Exception)
            {
                value = null;
                return false;
            }
        }

        public static object ConvertFromString(string text, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            string value = text ?? string.Empty;
            if (targetType == typeof(string))
                return value;
            if (targetType == typeof(bool))
                return bool.Parse(value);
            if (targetType == typeof(sbyte))
                return sbyte.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(byte))
                return byte.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(short))
                return short.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(ushort))
                return ushort.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(int))
                return int.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(uint))
                return uint.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(long))
                return long.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(ulong))
                return ulong.Parse(value, IntegerStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(float))
                return float.Parse(value, FloatStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(double))
                return double.Parse(value, FloatStyles, CultureInfo.InvariantCulture);
            if (targetType == typeof(decimal))
                return decimal.Parse(value, FloatStyles, CultureInfo.InvariantCulture);
            if (targetType.IsEnum)
                return Enum.Parse(targetType, value, true);
            if (targetType == typeof(Vector2))
            {
                float[] components = ParseComponents(value, 2, 2, nameof(Vector2));
                return new Vector2(components[0], components[1]);
            }
            if (targetType == typeof(Vector3))
            {
                float[] components = ParseComponents(value, 3, 3, nameof(Vector3));
                return new Vector3(components[0], components[1], components[2]);
            }
            if (targetType == typeof(Color))
            {
                float[] components = ParseComponents(value, 3, 4, nameof(Color));
                float alpha = components.Length == 4 ? components[3] : 1f;
                return new Color(components[0], components[1], components[2], alpha);
            }

            throw new NotSupportedException($"Unsupported option type: {targetType.Name}");
        }

        public static bool TryFormat(object value, Type valueType, out string text)
        {
            try
            {
                text = Format(value, valueType);
                return true;
            }
            catch (Exception)
            {
                text = null;
                return false;
            }
        }

        public static string Format(object value, Type valueType)
        {
            if (valueType == null)
                throw new ArgumentNullException(nameof(valueType));
            if (value == null)
                return "<null>";
            if (!valueType.IsInstanceOfType(value) && !valueType.IsValueType)
                throw new ArgumentException($"Value is not assignable to {valueType.Name}.", nameof(value));

            if (valueType == typeof(float))
                return ((float)value).ToString("R", CultureInfo.InvariantCulture);
            if (valueType == typeof(double))
                return ((double)value).ToString("R", CultureInfo.InvariantCulture);
            if (valueType == typeof(decimal))
                return ((decimal)value).ToString(CultureInfo.InvariantCulture);
            if (valueType == typeof(Vector2))
            {
                Vector2 vector = (Vector2)value;
                return Join(vector.x, vector.y);
            }
            if (valueType == typeof(Vector3))
            {
                Vector3 vector = (Vector3)value;
                return Join(vector.x, vector.y, vector.z);
            }
            if (valueType == typeof(Color))
            {
                Color color = (Color)value;
                return Join(color.r, color.g, color.b, color.a);
            }
            if (valueType.IsEnum)
                return Enum.Format(valueType, value, "G");
            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);

            return value.ToString();
        }

        public static object AdjustInteger(
            object value,
            Type valueType,
            decimal amount,
            decimal? minimum = null,
            decimal? maximum = null)
        {
            if (!IsIntegralType(valueType))
                throw new NotSupportedException($"Unsupported integer option type: {valueType?.Name ?? "<null>"}");
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            GetIntegralBounds(valueType, out decimal typeMinimum, out decimal typeMaximum);
            decimal effectiveMinimum = minimum.HasValue ? Math.Max(typeMinimum, minimum.Value) : typeMinimum;
            decimal effectiveMaximum = maximum.HasValue ? Math.Min(typeMaximum, maximum.Value) : typeMaximum;
            if (effectiveMinimum > effectiveMaximum)
                throw new ArgumentOutOfRangeException(nameof(minimum), "The effective integer range is empty.");

            decimal current = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            decimal adjusted;
            try
            {
                adjusted = current + amount;
            }
            catch (OverflowException)
            {
                adjusted = amount < 0m ? effectiveMinimum : effectiveMaximum;
            }

            decimal clamped = Math.Max(effectiveMinimum, Math.Min(effectiveMaximum, adjusted));
            return ConvertDecimalToInteger(clamped, valueType);
        }

        public static bool IsIntegralType(Type type)
        {
            return type == typeof(sbyte)
                   || type == typeof(byte)
                   || type == typeof(short)
                   || type == typeof(ushort)
                   || type == typeof(int)
                   || type == typeof(uint)
                   || type == typeof(long)
                   || type == typeof(ulong);
        }

        private static float[] ParseComponents(string text, int minimumCount, int maximumCount, string typeName)
        {
            string normalized = text.Trim();
            int openingParenthesis = normalized.IndexOf('(');
            if (openingParenthesis >= 0 && normalized.EndsWith(")", StringComparison.Ordinal))
                normalized = normalized.Substring(openingParenthesis + 1, normalized.Length - openingParenthesis - 2);

            string[] parts = normalized.Split(',');
            if (parts.Length < minimumCount || parts.Length > maximumCount)
                throw new FormatException($"{typeName} requires {FormatComponentCount(minimumCount, maximumCount)} invariant-culture components.");

            float[] components = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                components[i] = float.Parse(parts[i].Trim(), FloatStyles, CultureInfo.InvariantCulture);

            return components;
        }

        private static string FormatComponentCount(int minimumCount, int maximumCount)
        {
            return minimumCount == maximumCount
                ? minimumCount.ToString(CultureInfo.InvariantCulture)
                : $"{minimumCount.ToString(CultureInfo.InvariantCulture)} or {maximumCount.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string Join(float first, float second)
        {
            return $"{FormatFloat(first)},{FormatFloat(second)}";
        }

        private static string Join(float first, float second, float third)
        {
            return $"{FormatFloat(first)},{FormatFloat(second)},{FormatFloat(third)}";
        }

        private static string Join(float first, float second, float third, float fourth)
        {
            return $"{FormatFloat(first)},{FormatFloat(second)},{FormatFloat(third)},{FormatFloat(fourth)}";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void GetIntegralBounds(Type type, out decimal minimum, out decimal maximum)
        {
            if (type == typeof(sbyte))
            {
                minimum = sbyte.MinValue;
                maximum = sbyte.MaxValue;
                return;
            }
            if (type == typeof(byte))
            {
                minimum = byte.MinValue;
                maximum = byte.MaxValue;
                return;
            }
            if (type == typeof(short))
            {
                minimum = short.MinValue;
                maximum = short.MaxValue;
                return;
            }
            if (type == typeof(ushort))
            {
                minimum = ushort.MinValue;
                maximum = ushort.MaxValue;
                return;
            }
            if (type == typeof(int))
            {
                minimum = int.MinValue;
                maximum = int.MaxValue;
                return;
            }
            if (type == typeof(uint))
            {
                minimum = uint.MinValue;
                maximum = uint.MaxValue;
                return;
            }
            if (type == typeof(long))
            {
                minimum = long.MinValue;
                maximum = long.MaxValue;
                return;
            }
            if (type == typeof(ulong))
            {
                minimum = ulong.MinValue;
                maximum = ulong.MaxValue;
                return;
            }

            throw new NotSupportedException($"Unsupported integer option type: {type?.Name ?? "<null>"}");
        }

        private static object ConvertDecimalToInteger(decimal value, Type type)
        {
            if (type == typeof(sbyte))
                return decimal.ToSByte(value);
            if (type == typeof(byte))
                return decimal.ToByte(value);
            if (type == typeof(short))
                return decimal.ToInt16(value);
            if (type == typeof(ushort))
                return decimal.ToUInt16(value);
            if (type == typeof(int))
                return decimal.ToInt32(value);
            if (type == typeof(uint))
                return decimal.ToUInt32(value);
            if (type == typeof(long))
                return decimal.ToInt64(value);
            if (type == typeof(ulong))
                return decimal.ToUInt64(value);

            throw new NotSupportedException($"Unsupported integer option type: {type?.Name ?? "<null>"}");
        }
    }
}
