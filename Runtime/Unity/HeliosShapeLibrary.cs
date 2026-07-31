using System.Collections.Generic;
using UnityEngine;

namespace HeliosDebugger
{
    public static class HeliosShapeLibrary
    {
        private const int TextureSize = 64;
        private const int PixelsPerUnit = 100;
        private static readonly Dictionary<int, Sprite> RoundedSprites = new Dictionary<int, Sprite>();
        private static Sprite _circleSprite;
        private static Sprite _softShadowSprite;

        public static Sprite RoundedRect(float radius)
        {
            int roundedRadius = Mathf.Clamp(Mathf.RoundToInt(radius), 0, 28);
            if (RoundedSprites.TryGetValue(roundedRadius, out Sprite sprite))
                return sprite;

            Texture2D texture = CreateRoundedRectTexture(roundedRadius, false);
            float border = Mathf.Max(1f, roundedRadius + 2f);
            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = $"HeliosRoundedRect_{roundedRadius}";
            RoundedSprites[roundedRadius] = sprite;
            return sprite;
        }

        public static Sprite Circle()
        {
            if (_circleSprite != null)
                return _circleSprite;

            Texture2D texture = CreateRoundedRectTexture(TextureSize / 2 - 2, false);
            _circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit);
            _circleSprite.name = "HeliosCircle";
            return _circleSprite;
        }

        public static Sprite SoftShadow()
        {
            if (_softShadowSprite != null)
                return _softShadowSprite;

            Texture2D texture = CreateRoundedRectTexture(16, true);
            _softShadowSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                new Vector4(24f, 24f, 24f, 24f));
            _softShadowSprite.name = "HeliosSoftShadow";
            return _softShadowSprite;
        }

        private static Texture2D CreateRoundedRectTexture(int radius, bool shadow)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = shadow ? "HeliosSoftShadowTexture" : $"HeliosRoundedRectTexture_{radius}",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float half = (TextureSize - 1) * 0.5f;
            float rectHalf = shadow ? half - 14f : half - 1f;
            float cornerRadius = Mathf.Max(0f, radius);
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float px = x - half;
                    float py = y - half;
                    float distance = SignedRoundedBoxDistance(new Vector2(px, py), new Vector2(rectHalf, rectHalf), cornerRadius);
                    float alpha;
                    if (shadow)
                    {
                        alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0f, 14f, distance));
                        alpha *= alpha * 0.72f;
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(0.5f - distance);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            return texture;
        }

        private static float SignedRoundedBoxDistance(Vector2 position, Vector2 halfSize, float radius)
        {
            Vector2 q = new Vector2(Mathf.Abs(position.x), Mathf.Abs(position.y)) - halfSize + new Vector2(radius, radius);
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + Vector2.Max(q, Vector2.zero).magnitude - radius;
        }
    }
}
