using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Product;
using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

namespace UnicornsCustomSeeds.Managers
{
    public static class SeedVisualsManager
    {
        public static Dictionary<string, Sprite> seedIcons = new Dictionary<string, Sprite>();
        public static Dictionary<string, WeedAppearanceSettings> appearanceMap = new Dictionary<string, WeedAppearanceSettings>();
        
        public static Shader customShader;
        public static Material customMat;
        public static Sprite baseSeedSprite;

        public enum BlendMode { Lerp, Multiply, Add, Screen }
        public static BlendMode blendMode = BlendMode.Lerp;
        public static Rect gradientArea01 = new Rect(0.37f, 0.312f, 0.24f, 0.38f);
        public static float gradientOpacity = 1f;

        public static void LoadSeedMaterial()
        {
            try
            {
                AssetBundleUtils.LoadAssetBundle("customshaders");
                Sprite BaseIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("customseed_icon.png", "customshaders");

                if (BaseIconSprite != null)
                {
                    baseSeedSprite = BaseIconSprite;
                }
                Shader labelGradient = AssetBundleUtils.LoadAssetFromBundle<Shader>("labelgradient.shader", "customshaders");
                if (labelGradient != null)
                {
                    customShader = labelGradient;
                    Material newMat = new Material(customShader);
                    if (newMat != null)
                    {
                        customMat = newMat;
                    }
                }
                else
                {
                    Utility.Error("Fail");
                }
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        public static Sprite GenerateSpriteWithGradient(Color topColor, Color bottomColor)
        {
            if (baseSeedSprite == null || baseSeedSprite.texture == null)
            {
                Utility.Error("[VialTextureGenerator] Base Sprite is missing.");
                return null;
            }

            Texture2D spriteTexture = baseSeedSprite.texture;

            Rect spriteRect = baseSeedSprite.rect;
            spriteRect.x /= spriteTexture.width;
            spriteRect.y /= spriteTexture.height;
            spriteRect.width /= spriteTexture.width;
            spriteRect.height /= spriteTexture.height;

            Color[] spritePixels = spriteTexture.GetPixels(
              Mathf.FloorToInt(spriteRect.x * spriteTexture.width),
              Mathf.FloorToInt(spriteRect.y * spriteTexture.height),
              Mathf.FloorToInt(spriteRect.width * spriteTexture.width),
              Mathf.FloorToInt(spriteRect.height * spriteTexture.height)
            );

            ApplyVerticalGradientInRect(
                spritePixels,
                Mathf.FloorToInt(spriteRect.width * spriteTexture.width),
                Mathf.FloorToInt(spriteRect.height * spriteTexture.height),
                gradientArea01,
                topColor,
                bottomColor,
                gradientOpacity,
                blendMode
                );

            int newTextureWidth = Mathf.FloorToInt(spriteRect.width * spriteTexture.width);
            int newTextureHeight = Mathf.FloorToInt(spriteRect.height * spriteTexture.height);
            Texture2D copiedTexture = new Texture2D(newTextureWidth, newTextureHeight, TextureFormat.RGBA32, false);
            copiedTexture.SetPixels(spritePixels);
            copiedTexture.Apply();
            copiedTexture.name = "Copy";

            var sprite = Sprite.Create(copiedTexture, new Rect(0, 0, newTextureWidth, newTextureHeight), new Vector2(0.5f, 0.5f));
            return sprite;
        }

        private static void ApplyVerticalGradientInRect(
            Color[] pixels, int width, int height,
            Rect rect01, Color top, Color bottom, float opacity,
            BlendMode mode)
        {
            // Convert normalized rect to pixel-space
            int rx = Mathf.RoundToInt(rect01.x * width);
            int ry = Mathf.RoundToInt(rect01.y * height);
            int rw = Mathf.RoundToInt(rect01.width * width);
            int rh = Mathf.RoundToInt(rect01.height * height);

            // Clamp to bounds
            rx = Mathf.Clamp(rx, 0, width);
            ry = Mathf.Clamp(ry, 0, height);
            rw = Mathf.Clamp(rw, 0, width - rx);
            rh = Mathf.Clamp(rh, 0, height - ry);
            if (rw <= 0 || rh <= 0) return;

            // For each y in rect, compute t = (y - ry) / rh; top at rect top
            // NOTE: texture origin is bottom-left; "top" should be at higher y.
            for (int y = 0; y < rh; y++)
            {
                float t = (float)y / (float)(rh - 1 <= 0 ? 1 : (rh - 1));
                // t=0 at bottom of rect → bottom color; we want top color at rect top, so:
                Color grad = Color.Lerp(bottom, top, t);
                grad.a *= opacity;

                int py = ry + y;
                int row = py * width;

                for (int x = 0; x < rw; x++)
                {
                    int px = rx + x;
                    int idx = row + px;

                    var dst = pixels[idx];
                    pixels[idx] = Blend(grad, dst, mode);
                }
            }
        }

        private static Color Blend(Color dst, Color src, BlendMode mode)
        {
            switch (mode)
            {
                case BlendMode.Multiply:
                    return new Color(
                            dst.r * Mathf.Lerp(1f, src.r, src.a),
                            dst.g * Mathf.Lerp(1f, src.g, src.a),
                            dst.b * Mathf.Lerp(1f, src.b, src.a),
                            Mathf.Max(dst.a, src.a)
                    );

                case BlendMode.Add:
                    return new Color(
                            Mathf.Clamp01(dst.r + src.r * src.a),
                            Mathf.Clamp01(dst.g + src.g * src.a),
                            Mathf.Clamp01(dst.b + src.b * src.a),
                            Mathf.Max(dst.a, src.a)
                    );

                case BlendMode.Screen:
                    // screen = 1 - (1 - A) * (1 - B)
                    float r = 1f - (1f - dst.r) * (1f - src.r * src.a);
                    float g = 1f - (1f - dst.g) * (1f - src.g * src.a);
                    float b = 1f - (1f - dst.b) * (1f - src.b * src.a);
                    return new Color(r, g, b, Mathf.Max(dst.a, src.a));

                case BlendMode.Lerp:
                default:
                    // “Normal” over: lerp RGB by src alpha, preserve max alpha
                    return new Color(
                            Mathf.Lerp(dst.r, src.r, src.a),
                            Mathf.Lerp(dst.g, src.g, src.a),
                            Mathf.Lerp(dst.b, src.b, src.a),
                            Mathf.Max(dst.a, src.a)
                    );
            }
        }
    }
}
