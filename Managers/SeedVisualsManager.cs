using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.Product;
#elif MONO
using ScheduleOne.Product;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class SeedVisualsManager
    {
        public static Dictionary<string, Sprite> seedIcons = new Dictionary<string, Sprite>();
        // One appearance map per drug type. They cannot share a dictionary: each game
        // appearance class is unrelated, and ShroomAppearanceSettings names its first
        // colour PrimaryColor rather than MainColor. Each manager's ClearAll empties its
        // own map — required, not optional, since returning to the menu invalidates the
        // objects these entries describe.
        public static Dictionary<string, WeedAppearanceSettings> weedAppearanceMap = new Dictionary<string, WeedAppearanceSettings>();
        public static Dictionary<string, CocaineAppearanceSettings> cocaAppearanceMap = new Dictionary<string, CocaineAppearanceSettings>();
        public static Dictionary<string, ShroomAppearanceSettings> shroomAppearanceMap = new Dictionary<string, ShroomAppearanceSettings>();
        public static Dictionary<string, MethAppearanceSettings> pseudoAppearanceMap = new Dictionary<string, MethAppearanceSettings>();

        /// <summary>
        /// Resolves a vial label's two colours for an item ID, whichever drug it belongs
        /// to. Returns false when nothing is registered — callers must not fall back to
        /// the default Color, which is transparent black and paints the label out.
        /// </summary>
        public static bool TryGetLabelColors(string id, out Color main, out Color secondary)
        {
            main = default;
            secondary = default;
            if (string.IsNullOrEmpty(id)) return false;

            if (weedAppearanceMap.TryGetValue(id, out var weed) && weed != null)
            {
                main = weed.MainColor; secondary = weed.SecondaryColor; return true;
            }

            if (cocaAppearanceMap.TryGetValue(id, out var coca) && coca != null)
            {
                main = coca.MainColor; secondary = coca.SecondaryColor; return true;
            }

            // PrimaryColor, not MainColor — this one is shaped differently.
            if (shroomAppearanceMap.TryGetValue(id, out var shroom) && shroom != null)
            {
                main = shroom.PrimaryColor; secondary = shroom.SecondaryColor; return true;
            }

            if (pseudoAppearanceMap.TryGetValue(id, out var pseudo) && pseudo != null)
            {
                main = pseudo.MainColor; secondary = pseudo.SecondaryColor; return true;
            }

            return false;
        }

        public static Shader customShader;
        public static Material customMat;
        public static Sprite baseSeedSprite;
        public static Sprite baseCocaSeedSprite;
        public static Sprite baseShroomSpawnSprite;
        public static Sprite baseSyringeSprite;
        public static Sprite basePseudoSprite;
        public static Sprite baseLiquidMethSprite;
        public static Sprite baseCocaLeafSprite;
        public static Sprite baseCocaBaseSprite;
        public static Sprite baseQuestIconSprite;
        public static Sprite weedQuestIconSprite;
        public static Sprite methQuestIconSprite;
        public static Sprite shroomQuestIconSprite;
        public static Sprite cocaineQuestIconSprite;

        public static void LoadSeedMaterial()
        {
            try
            {
                var bundle = AssetBundleUtils.LoadAssetBundle("customshaders");
                //if (bundle != null)
                //{
                //    var assetNames = bundle.GetAllAssetNames();
                //    Utility.Log($"[SeedVisualsManager] 'customshaders' bundle contains {assetNames.Length} asset(s):");
                //    foreach (var assetName in assetNames)
                //    {
                //        Utility.Log($"  - {assetName}");
                //    }
                //}

                Sprite BaseSeedIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("customseedicon.png", "customshaders");

                if (BaseSeedIconSprite != null)
                {
                    baseSeedSprite = BaseSeedIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseSeedSprite);
                }

                Sprite BaseCocaSeedIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cokecustomseedicon.png", "customshaders");

                if (BaseCocaSeedIconSprite != null)
                {
                    baseCocaSeedSprite = BaseCocaSeedIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseCocaSeedSprite);
                }

                Sprite BaseShroomSpawnIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("shroomspawnicon.png", "customshaders");

                if (BaseShroomSpawnIconSprite != null)
                {
                    baseShroomSpawnSprite = BaseShroomSpawnIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseShroomSpawnSprite);
                }

                Sprite BaseSyringeIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("syringeicon.png", "customshaders");

                if (BaseSyringeIconSprite != null)
                {
                    baseSyringeSprite = BaseSyringeIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseSyringeSprite);
                }

                Sprite BasePseudoIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("pseduoicon.png", "customshaders");

                if (BasePseudoIconSprite != null)
                {
                    basePseudoSprite = BasePseudoIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(basePseudoSprite);
                }

                Sprite BaseLiquidMethIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("liquidmethicon.png", "customshaders");

                if (BaseLiquidMethIconSprite != null)
                {
                    baseLiquidMethSprite = BaseLiquidMethIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseLiquidMethSprite);
                }

                Sprite BaseCocaLeafIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cocaleaficon.png", "customshaders");

                if (BaseCocaLeafIconSprite != null)
                {
                    baseCocaLeafSprite = BaseCocaLeafIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseCocaLeafSprite);
                }

                Sprite BaseCocaBaseIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cocaainebaseicon.png", "customshaders");

                if (BaseCocaBaseIconSprite != null)
                {
                    baseCocaBaseSprite = BaseCocaBaseIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseCocaBaseSprite);
                }

                Sprite BaseQuestIconSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("basequest_icon.png", "customshaders");

                if (BaseQuestIconSprite != null)
                {
                    baseQuestIconSprite = BaseQuestIconSprite;
                    UnityEngine.Object.DontDestroyOnLoad(baseQuestIconSprite);
                }

                Sprite WeedQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("weedquest_icon.png", "customshaders");

                if (WeedQuestSprite != null)
                {
                    weedQuestIconSprite = WeedQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(weedQuestIconSprite);
                }

                Sprite MethQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("methquest_icon.png", "customshaders");

                if (MethQuestSprite != null)
                {
                    methQuestIconSprite = MethQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(methQuestIconSprite);
                }

                Sprite ShroomQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("shroomquest_icon.png", "customshaders");

                if (ShroomQuestSprite != null)
                {
                    shroomQuestIconSprite = ShroomQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(shroomQuestIconSprite);
                }

                Sprite CocaineQuestSprite = AssetBundleUtils.LoadAssetFromBundle<Sprite>("cocainequest_icon.png", "customshaders");

                if (CocaineQuestSprite != null)
                {
                    cocaineQuestIconSprite = CocaineQuestSprite;
                    UnityEngine.Object.DontDestroyOnLoad(cocaineQuestIconSprite);
                }

                Shader labelGradient = AssetBundleUtils.LoadAssetFromBundle<Shader>("labelgradient.shader", "customshaders");
                if (labelGradient != null)
                {
                    customShader = labelGradient;
                    Material newMat = new Material(customShader);
                    if (newMat != null)
                    {
                        customMat = newMat;
                        UnityEngine.Object.DontDestroyOnLoad(labelGradient);
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

        // Artists paint the "gradient goes here" area of an icon in pure magenta (#FF00FF).
        // A colour key is immune to the anti-aliasing/shadow noise that can live in a source
        // PNG's alpha channel, unlike filling by transparency. The fill below is built around
        // magenta specifically (red and blue high, green low), so the placeholder must stay
        // exactly that colour.

        /// <summary>
        /// Generates a gradient-filled version of any icon sprite by replacing the magenta
        /// placeholder with the gradient, leaving every other pixel — including transparent
        /// background and any noisy/soft alpha — untouched.
        ///
        /// Pixels move as Color32 (4 bytes each) rather than Color (16 bytes), so the
        /// GetPixels32/SetPixels32 copies across the IL2CPP boundary carry a quarter of the data.
        /// </summary>
        public static Sprite GenerateIconWithKeyColorFill(Sprite baseIcon, Color topColor, Color bottomColor)
        {
            if (baseIcon == null || baseIcon.texture == null)
            {
                Utility.Error("[GradientIconFill] Base icon sprite is missing.");
                return null;
            }

            Texture2D spriteTexture = baseIcon.texture;
            int texWidth = spriteTexture.width;
            int texHeight = spriteTexture.height;

            Rect spriteRect = baseIcon.rect;
            int x0 = Mathf.FloorToInt(spriteRect.x);
            int y0 = Mathf.FloorToInt(spriteRect.y);
            int width = Mathf.FloorToInt(spriteRect.width);
            int height = Mathf.FloorToInt(spriteRect.height);

            // GetPixels32 has no sub-rect overload. The bundle's icons are each their own
            // texture, so this is normally the whole array; crop only for an atlased sprite.
            Color32[] spritePixels = spriteTexture.GetPixels32();
            if (x0 != 0 || y0 != 0 || width != texWidth || height != texHeight)
                spritePixels = CropPixels(spritePixels, texWidth, x0, y0, width, height);

            FillKeyColorGaps(spritePixels, width, height, topColor, bottomColor);

            // No mipmaps: they blurred the icon noticeably at inventory-slot size. Without them
            // the outline can shimmer slightly as an item moves between slots, which is the
            // accepted trade-off. Clamp stops bilinear sampling from wrapping the opposite
            // edge onto the border.
            Texture2D copiedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            copiedTexture.wrapMode = TextureWrapMode.Clamp;
            copiedTexture.SetPixels32(spritePixels);
            copiedTexture.Apply();
            copiedTexture.name = baseIcon.name + "_KeyColorFill";

            return Sprite.Create(copiedTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }

        private static Color32[] CropPixels(Color32[] source, int sourceWidth, int x0, int y0, int width, int height)
        {
            Color32[] cropped = new Color32[width * height];
            for (int y = 0; y < height; y++)
                Array.Copy(source, (y0 + y) * sourceWidth + x0, cropped, y * width, width);
            return cropped;
        }

        // Magenta shares below this (about 20/255) are left alone, so warm or reddish
        // art pixels that read as faintly magenta are not tinted.
        private const float MinKeyAmount = 0.08f;

        /// <summary>
        /// Replaces the magenta placeholder with a top-to-bottom gradient, green-screen style.
        ///
        /// Each pixel is treated as a blend of magenta and whatever art sits under it:
        ///   pixel = k * magenta + (1 - k) * art
        /// For neutral (grey/black) outline art, min(r, b) - g recovers k exactly: magenta
        /// contributes equally to r and b and nothing to g, while grey contributes equally to
        /// all three. The fill then subtracts the magenta share and adds the same share of
        /// gradient. A fully magenta pixel becomes pure gradient; an anti-aliased edge pixel
        /// that was half magenta becomes half gradient, half outline, with no pink left over.
        ///
        /// The previous version matched by RGB distance within a 0.25 tolerance, which left
        /// edge pixels beyond that radius pink. The in-game downscale then spread that rim.
        ///
        /// Alpha is kept as-is so soft edges against a transparent background stay soft.
        ///
        /// HOT PATH — runs once per pixel of a full icon (262,144 at 512x512, or 65,536 at
        /// 256x256). Icons display at roughly 100px in-game, so keep the base art at 256x256.
        ///
        /// Every UnityEngine helper called here crosses the IL2CPP interop boundary. The
        /// original version used Color.Lerp, Mathf.Clamp01, Mathf.Sqrt (via
        /// ColorDistanceRgb) and 4x Mathf.Lerp — about 6 interop transitions per pixel, or
        /// ~1.5 million for one 512x512 icon. Profiling measured 251ms in that version alone.
        ///
        /// Everything below is therefore plain C# arithmetic. Do NOT reintroduce
        /// Mathf/Color helper calls inside these loops.
        /// </summary>
        private static void FillKeyColorGaps(Color32[] pixels, int width, int height, Color top, Color bottom)
        {
            if (pixels == null || width <= 0 || height <= 0) return;

            float topR = top.r * 255f,    topG = top.g * 255f,    topB = top.b * 255f;
            float botR = bottom.r * 255f, botG = bottom.g * 255f, botB = bottom.b * 255f;

            const float inv255 = 1f / 255f;
            int denom = height - 1 <= 0 ? 1 : height - 1;

            for (int y = 0; y < height; y++)
            {
                float t = (float)y / denom;
                float gradR = botR + (topR - botR) * t;
                float gradG = botG + (topG - botG) * t;
                float gradB = botB + (topB - botB) * t;

                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    Color32 dst = pixels[idx];

                    // Cheap early-out: no magenta share without green below both red and blue.
                    int minRB = dst.r < dst.b ? dst.r : dst.b;
                    if (minRB <= dst.g) continue;

                    float k = (minRB - dst.g) * inv255;
                    if (k < MinKeyAmount) continue;
                    if (k > 1f) k = 1f;

                    // Remove k * magenta (255, 0, 255), then add k * gradient. Green has no
                    // magenta share, so it only gains the gradient.
                    float magenta = 255f * k;

                    // Mutate the local struct and write it back, rather than calling the
                    // Color32 constructor, to keep the whole body interop-free.
                    dst.r = ToByte(dst.r - magenta + gradR * k);
                    dst.g = ToByte(dst.g + gradG * k);
                    dst.b = ToByte(dst.b - magenta + gradB * k);
                    pixels[idx] = dst;
                }
            }
        }

        private static byte ToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)(v + 0.5f);
        }

        /// <summary>
        /// Euclidean RGB distance — 0 for identical colors, up to ~1.73 for opposite corners
        /// of the RGB cube (e.g. black vs. white). Used both for key-color pixel matching and,
        /// more generally, as a "how different do these two colors look" metric.
        /// </summary>
        public static float ColorDistanceRgb(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        /// <summary>
        /// If top/bottom are too close to produce a visible gradient (e.g. cocaine/meth mixes
        /// that land on nearly the same off-white), spreads them apart in HSV space instead of
        /// picking a 3rd stored color (unlike shrooms, coca/meth only have two colors to work
        /// with). Lightens the lighter one toward white and darkens the darker one toward black,
        /// each by half the remaining headroom, so separation happens regardless of how close to
        /// the extremes the original values already are. Also nudges saturation up slightly,
        /// since near-identical colors are usually low-saturation, where hue barely reads at all.
        /// Each color keeps its own hue — this reshades, it doesn't recolor.
        /// </summary>
        public static (Color top, Color bottom) BoostContrastIfSimilar(Color top, Color bottom, float similarityThreshold = 0.15f)
        {
            if (ColorDistanceRgb(top, bottom) >= similarityThreshold)
                return (top, bottom);

            Color.RGBToHSV(top, out float topH, out float topS, out float topV);
            Color.RGBToHSV(bottom, out float botH, out float botS, out float botV);

            topS = Mathf.Clamp01(topS + 0.2f);
            botS = Mathf.Clamp01(botS + 0.2f);

            float boostedTopV = Mathf.Lerp(topV, 1f, 0.5f);
            float boostedBotV = Mathf.Lerp(botV, 0f, 0.5f);

            Color boostedTop = Color.HSVToRGB(topH, topS, boostedTopV);
            boostedTop.a = top.a;
            Color boostedBottom = Color.HSVToRGB(botH, botS, boostedBotV);
            boostedBottom.a = bottom.a;

            return (boostedTop, boostedBottom);
        }
    }
}
