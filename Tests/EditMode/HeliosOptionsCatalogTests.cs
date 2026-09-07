using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using HeliosDebugger.Editor;
using NUnit.Framework;

namespace HeliosDebugger.Tests
{
    public sealed class HeliosOptionsCatalogTests
    {
        [Test]
        public void GeneratedPreservationFileIsDiscoveredByUnity()
        {
            HeliosOptionsCatalogGenerator.Generate();
            const string path = "Assets/HeliosDebuggerGenerated/link.xml";
            Assert.IsTrue(File.Exists(path));
            Assert.IsFalse(File.Exists("Assets/HeliosDebuggerGenerated/HeliosGeneratedOptions.link.xml"));
            var stripper = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.AssemblyStripper");
            MethodInfo discover = stripper.GetMethod("GetUserBlacklistFiles", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            var paths = ((IEnumerable)discover.Invoke(null, null)).Cast<object>().Select(x => x.ToString());
            Assert.IsTrue(paths.Any(p => Path.GetFullPath(p) == Path.GetFullPath(path)));
        }
    }
}
