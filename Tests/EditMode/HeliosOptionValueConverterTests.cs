using NUnit.Framework;
using UnityEngine;

namespace Astra.Helios.Tests
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, "HeliosDebugger.Tests", "HeliosDebugger.EditMode.Tests")]
    public sealed class HeliosOptionValueConverterTests
    {
        [Test]
        public void Vector3RoundTripsInvariantComponents()
        {
            Vector3 expected = new Vector3(1.25f, -2.5f, 30f);
            string text = HeliosOptionValueConverter.Format(expected, typeof(Vector3));

            Vector3 actual = (Vector3)HeliosOptionValueConverter.ConvertFromString(text, typeof(Vector3));

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ColorAcceptsRgbAndDefaultsAlpha()
        {
            Color value = (Color)HeliosOptionValueConverter.ConvertFromString("0.1,0.2,0.3", typeof(Color));

            Assert.AreEqual(0.1f, value.r);
            Assert.AreEqual(0.2f, value.g);
            Assert.AreEqual(0.3f, value.b);
            Assert.AreEqual(1f, value.a);
        }

        [Test]
        public void UnsignedIntegralAdjustmentClampsWithoutOverflow()
        {
            object value = HeliosOptionValueConverter.AdjustInteger(
                ulong.MaxValue,
                typeof(ulong),
                10m);

            Assert.AreEqual(ulong.MaxValue, value);
        }
    }
}
