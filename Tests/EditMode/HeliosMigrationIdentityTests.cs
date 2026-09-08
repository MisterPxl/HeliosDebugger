using System;
using NUnit.Framework;

namespace Astra.Helios.Tests
{
    public sealed class HeliosMigrationIdentityTests
    {
        [TestCase(typeof(HeliosConsoleTab), "HeliosDebugger.HeliosConsoleTab")]
        [TestCase(typeof(HeliosProfilerTab), "HeliosDebugger.HeliosProfilerTab")]
        [TestCase(typeof(HeliosOptionsTab), "HeliosDebugger.HeliosOptionsTab")]
        [TestCase(typeof(HeliosSystemInfoTab), "HeliosDebugger.HeliosSystemInfoTab")]
        [TestCase(typeof(HeliosBugReporterTab), "HeliosDebugger.HeliosBugReporterTab")]
        public void BuiltInTabIdentitiesRemainStable(Type type, string id)
        {
            Assert.That(HeliosTypeIdentityAttribute.GetId(type), Is.EqualTo(id));
        }

        [Test]
        public void RenamedOptionsKeepTheirPersistenceKey()
        {
            var field = typeof(RenamedOptions).GetField(nameof(RenamedOptions.Count));
            var option = HeliosOptionMember.FromField(typeof(RenamedOptions), null, field,
                new HeliosOptionAttribute("Count") { Persist = true }, new HeliosOptionsAttribute("Migration"));
            Assert.That(option.PersistenceKey, Is.EqualTo("HeliosOption.Legacy.Game.Options.Count"));
        }

        [Test]
        public void IdentityIsNotInheritedByAnotherType()
        {
            Assert.That(HeliosTypeIdentityAttribute.GetId(typeof(DerivedOptions)), Is.EqualTo(typeof(DerivedOptions).FullName));
            Assert.Throws<ArgumentException>(() => new HeliosTypeIdentityAttribute(" "));
        }

        [HeliosTypeIdentity("Legacy.Game.Options")]
        private class RenamedOptions { public static int Count; }
        private sealed class DerivedOptions : RenamedOptions { }
    }
}
