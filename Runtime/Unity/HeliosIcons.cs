using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeliosDebugger
{
    public interface IHeliosTabIcon
    {
        Sprite Icon { get; }
    }

    [CreateAssetMenu(fileName = "HeliosIconSet", menuName = "Astra/Helios/Icon Set")]
    public sealed class HeliosIconSet : ScriptableObject
    {
        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public bool TryGet(string id, out Sprite sprite)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry != null && string.Equals(entry.Id, id, StringComparison.Ordinal))
                {
                    sprite = entry.Sprite;
                    return sprite != null;
                }
            }

            sprite = null;
            return false;
        }

        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _sprite;

            public string Id => _id;
            public Sprite Sprite => _sprite;
        }
    }

    public static class HeliosIcons
    {
        public const string Console = "terminal";
        public const string Profiler = "activity";
        public const string Options = "sliders";
        public const string System = "cpu";
        public const string BugReporter = "bug";
        public const string Tweens = "zap";

        private const string ResourceRoot = "HeliosDebugger/Icons/";
        private static readonly List<HeliosIconSet> RegisteredSets = new List<HeliosIconSet>();
        private static readonly Dictionary<string, Sprite> ResourceSprites = new Dictionary<string, Sprite>();

        public static void Register(HeliosIconSet iconSet)
        {
            if (iconSet != null && !RegisteredSets.Contains(iconSet))
                RegisteredSets.Add(iconSet);
        }

        public static bool Unregister(HeliosIconSet iconSet)
        {
            return iconSet != null && RegisteredSets.Remove(iconSet);
        }

        /// <summary>
        /// Destroys the sprites generated from Resources textures. The source
        /// textures are assets and are left untouched; sprites are recreated
        /// lazily on the next lookup.
        /// </summary>
        public static void ClearCache()
        {
            foreach (KeyValuePair<string, Sprite> pair in ResourceSprites)
                HeliosObjectUtility.Destroy(pair.Value);
            ResourceSprites.Clear();
        }

        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            for (int i = RegisteredSets.Count - 1; i >= 0; i--)
            {
                if (RegisteredSets[i] != null && RegisteredSets[i].TryGet(id, out Sprite sprite))
                    return sprite;
            }

            if (ResourceSprites.TryGetValue(id, out Sprite cached))
                return cached;

            Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + id);
            if (texture == null)
                return null;

            Sprite created = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                Mathf.Max(texture.width, texture.height));
            created.name = $"HeliosIcon_{id}";
            ResourceSprites[id] = created;
            return created;
        }
    }
}
