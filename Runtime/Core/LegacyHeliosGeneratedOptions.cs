using System;

namespace HeliosDebugger
{
    /// <summary>Lets existing generated catalogs compile so the Editor generator can replace them.</summary>
    [Obsolete("Regenerate the Helios options catalog to use Astra.Helios.HeliosGeneratedOptions.")]
    public static class HeliosGeneratedOptions
    {
        public static void Register(Type type) => global::Astra.Helios.HeliosGeneratedOptions.Register(type);
    }
}
