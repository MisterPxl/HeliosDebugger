using System;
using System.Linq;
using NUnit.Framework;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosOptionReadTests
    {
        [Test]
        public void FailingGetterDoesNotHideHealthyOptionsAndCanRecover()
        {
            var instance = new IntermittentOptions();
            using (var registry = new HeliosOptionsRegistry())
            {
                registry.RegisterInstance(instance);
                var broken = (HeliosOptionMember)registry.Options.Single(x => x.DisplayName == "Intermittent");
                Assert.AreEqual("10", registry.Options.Single(x => x.DisplayName == "Healthy").GetDisplayValue());
                Assert.AreEqual("<unavailable>", broken.GetDisplayValue());
                Assert.IsNull(broken.GetValue());
                Assert.DoesNotThrow(() => broken.Adjust(1));
                Assert.DoesNotThrow(() => broken.Reset());
                Assert.AreEqual(7, instance.Value);

                instance.Ready = true;
                Assert.AreEqual("7", broken.GetDisplayValue());
                broken.Adjust(1);
                Assert.AreEqual(8, instance.Value);
                broken.Reset();
                Assert.AreEqual(7, instance.Value, "Reset uses the first successfully read value.");

                instance.Ready = false;
                Assert.AreEqual("<unavailable>", broken.GetDisplayValue());
                Assert.AreEqual("10", registry.Options.Single(x => x.DisplayName == "Healthy").GetDisplayValue());
            }
        }

        [Test]
        public void LegitimateNullIsAReadableValue()
        {
            using (var registry = new HeliosOptionsRegistry())
            {
                registry.RegisterInstance(new NullOptions());
                var option = (HeliosOptionMember)registry.Options.Single(x => x.DisplayName == "NullableText");
                Assert.IsTrue(option.TryGetValue(out object value));
                Assert.IsNull(value);
                Assert.AreNotEqual("<unavailable>", option.GetDisplayValue());
            }
        }

        private sealed class IntermittentOptions
        {
            public bool Ready;
            public int Value = 7;
            [HeliosOption("Intermittent")]
            public int Intermittent
            {
                get => Ready ? Value : throw new InvalidOperationException("Subsystem not ready");
                set => Value = value;
            }
            [HeliosOption("Healthy")] public int Healthy = 10;
        }

        private sealed class NullOptions
        {
            [HeliosOption("NullableText")] public string Text => null;
        }
    }
}
