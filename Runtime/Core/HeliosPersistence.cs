using UnityEngine;

namespace Astra.Helios
{
    /// <summary>
    /// Batches PlayerPrefs writes: option changes mark the store dirty and the
    /// debugger root flushes at most once per frame boundary it chooses,
    /// instead of paying a synchronous disk save on every value change.
    /// </summary>
    internal static class HeliosPersistence
    {
        private static bool _dirty;

        public static void MarkDirty()
        {
            _dirty = true;
        }

        public static void FlushIfDirty()
        {
            if (!_dirty)
                return;

            _dirty = false;
            PlayerPrefs.Save();
        }
    }
}
