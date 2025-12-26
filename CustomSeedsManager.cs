using Il2Cpp;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.UI.Shop;
using MelonLoader;
using Newtonsoft.Json;
using S1API.DeadDrops;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnicornsCustomSeeds.CustomQuests;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.SupplierStashes;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using Il2Generic = Il2CppSystem.Collections.Generic;

namespace UnicornsCustomSeeds
{

    public struct SeedComponents
    {
        public string seedId;
        public string baseSeedId;
    }

    public static class CustomSeedsManager
    {
        public static CustomSeedQuest seedDropoff;
        public static MSGConversation albertsConvo;
        public static Dictionary<string, Sprite> seedIcons = new Dictionary<string, Sprite>();
        public static Dictionary<string, WeedAppearanceSettings> appearanceMap = new Dictionary<string, WeedAppearanceSettings>();
        public static Queue<SeedDefinition> SeedQueue = new Queue<SeedDefinition>();
        public static Dictionary<string, SeedFactory> factories = new();
        public static Dictionary<string, UnicornSeedData> DiscoveredSeeds = new();
        public static Shader customShader;
        public static Material customMat;
        public static Sprite baseSeedSprite;
        public static string baseSeedId = "<SEEDID>_customseeddefinition";

        public enum BlendMode { Lerp, Multiply, Add, Screen }
        public static BlendMode blendMode = BlendMode.Lerp;
        public static FilterMode outputFilter = FilterMode.Bilinear;
        public static Rect gradientArea01 = new Rect(0.37f, 0.312f, 0.24f, 0.38f);
        public static float gradientOpacity = 1f;


        public static ShopInterface Shop = null;
        public static GameObject shopGo = null;

        public static Il2Generic.Dictionary<string, SeedDefinition> baseSeedDefinitions = new Il2Generic.Dictionary<string, SeedDefinition>();
        public static Il2Generic.Dictionary<string, ShopListing> baseShopListing = new Il2Generic.Dictionary<string, ShopListing>();
        public delegate bool ValidityCheckDelegate(SendableMessage message, out Il2CppSystem.String invalidReason);

        public static void Initialize()
        {
            var shopInterfaces = GameObject.FindObjectsOfType<ShopInterface>();
            foreach (ShopInterface shopInterface in shopInterfaces)
            {
                if (shopInterface.gameObject.name == "WeedSupplierInterface")
                {
                    shopGo = shopInterface.gameObject;
                    Shop = shopInterface;
                    break;
                }
            }

            if (Shop == null)
            {
                MelonLogger.Msg("Shop is null!");
                return;
            }

            GetAlbertHoover();

            InitDictionary();
            if (!baseSeedDefinitions.ContainsKey("ogkushseed"))
            {
                MelonLogger.Msg("ogkushseed doesn't exist!");
                return;
            }

            foreach (var seed in DiscoveredSeeds)
            {
                SeedDefinition customDef = Registry.GetItem<SeedDefinition>(seed.Key);
                if (customDef != null)
                {
                    CreateShopListing(customDef, seed.Value.baseSeedId);
                }
            }

        }

        public static void ClearAll()
        {
            seedIcons.Clear();
            appearanceMap.Clear();
            DiscoveredSeeds.Clear();
            SeedQueue.Clear();
        }

        public static void GetAlbertHoover()
        {
            Albert albert = GameObject.FindObjectOfType<Albert>();
            if (albert != null)
            {
                Utility.Log("Found Albert Hoover");
                MSGConversation convo = albert.MSGConversation;
                if (convo != null)
                {
                    Utility.Log("Found Alberts Conversation");
                    albertsConvo = convo;
                    MessageSenderInterface senderInterface = convo.senderInterface;
                    SendableMessage sendable = albertsConvo.CreateSendableMessage("Order Seeds");
                    sendable.onSent += (Action) OnSent;
                }

            }
        }

        public static void OnSent()
        {
            MessageChain messageChain = new MessageChain();
            messageChain.Messages.Add("Drop the weed mix and cash in my drop box.");
            messageChain.id = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            albertsConvo.SendMessageChain(messageChain, 0.5f, true, true);


            if (seedDropoff == null)
            {
                Utility.Log("Quest Started");
                seedDropoff = S1API.Quests.QuestManager.CreateQuest<CustomSeedQuest>() as CustomSeedQuest;
            }
        }

        public static void CompleteQuest(WeedDefinition weedDef)
        {
            if (seedDropoff != null)
            {
                seedDropoff.Complete();
                seedDropoff = null;
                albertsConvo.SendMessage(new Message("I will begin synthesizing the seed", Message.ESenderType.Other, true, -1), true, true);
                MelonCoroutines.Start(CreateSeed(10f, weedDef));
            }
        }

        public static void BroadcastCustomSeed(UnicornSeedData seed)
        {
            ProductManager prodManager = NetworkSingleton<ProductManager>.Instance;
            string json = JsonConvert.SerializeObject(seed);
            string payload = "[UNISEED]" + json;

            var props = new Il2Generic.List<string>();
            var appearance = new WeedAppearanceSettings(
                prodManager.DefaultWeed.MainMat.color,
                prodManager.DefaultWeed.SecondaryMat.color,
                prodManager.DefaultWeed.LeafMat.color,
                prodManager.DefaultWeed.StemMat.color);

            prodManager.CreateWeed_Server(payload, "ogkushseed",
                                             EDrugType.Marijuana, props, appearance);
        }

        public static IEnumerator CreateSeed(float delaySeconds, WeedDefinition weedDef)
        {
            yield return new WaitForSeconds(delaySeconds);

            WeedDefinition baseSeed = StashManager.GetBaseStrain(weedDef);

            var newSeed = factories[baseSeed.ID].CreateSeedDefinition(weedDef);

            Singleton<Registry>.Instance.AddToRegistry(newSeed);
            float price = StashManager.GetIngredientCost(weedDef);
            UnicornSeedData newSeedData = new UnicornSeedData
            {
                seedId = newSeed.ID,
                weedId = weedDef.ID,
                baseSeedId = baseSeed.ID,
                price = price,
            };
            DiscoveredSeeds.Add(newSeed.ID, newSeedData);
            CreateShopListing(newSeed, baseSeed.ID, price);
            SeedQueue.Enqueue(newSeed);

            // 3. You can yield return other things, like waiting for a request, 
            // or just yield return null to wait until the next frame.
            yield return null;

            // 4. Repeat logic steps or actions.
            Utility.Log("Coroutine performing second action in the next frame.");

            // --- Coroutine finishes ---
            // When the method reaches the end (or hits 'yield break'), the coroutine is complete.
            Utility.Log("Coroutine finished successfully.");

            DeadDrop randomEmptyDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomEmptyDrop != null)
            {
                ItemInstance defaultInstance = newSeed.GetDefaultInstance();
                defaultInstance.SetQuantity(10);
                randomEmptyDrop.Storage.InsertItem(defaultInstance, true);
                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomEmptyDrop.GUID.ToString(), guidString);
                albertsConvo.SendMessage(new Message($"{weedDef.name} is synthesized and placed in the deaddrop", Message.ESenderType.Other, true, -1), true, true);
            }
            else
            {
                albertsConvo.SendMessage(new Message($"{weedDef.name} is synthesized and available in the shop", Message.ESenderType.Other, true, -1), true, true);
            }

        }

        public static void OnSelected()
        {
            albertsConvo.senderInterface.SetVisibility(MessageSenderInterface.EVisibility.Docked);
        }

        public static void InitDictionary()
        {
            var listings = Shop.Listings;
            foreach (ShopListing listing in listings)
            {

                if (listing.Item.TryCast<SeedDefinition>() is SeedDefinition seed)
                {
                    if (!baseSeedDefinitions.ContainsKey(seed.ID))
                    {
                        baseSeedDefinitions.Add(seed.ID, seed);
                        baseShopListing.Add(seed.ID, listing);
                    }
                }
                else
                {
                    MelonLogger.Warning($"{listing.name} could not be cast to SeedDefinition");
                }
            }
        }

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

        //public static Sprite CreateSeedIcon(Transform model, Color? tint = null)
        //{
        //    if (model == null) return null;

        //    Utility.Log("[Create Icon] Attempting to Create");
        //    var generator = Singleton<IconGenerator>.Instance;
        //    Texture2D icon = generator.GetTexture(model);

        //    if (icon == null) return null;

        //    if (tint.HasValue)
        //    {
        //        Color[] pixels = icon.GetPixels();
        //        for (int i = 0; i < pixels.Length; i++)
        //        {
        //            pixels[i] *= tint.Value; // multiply tint
        //        }
        //        icon.SetPixels(pixels);
        //    }

        //    icon.Apply();

        //    return Sprite.Create(
        //        icon,
        //        new Rect(0f, 0f, icon.width, icon.height),
        //        new Vector2(0.5f, 0.5f)
        //    );
        //}

        public static void CreateShopListing(SeedDefinition newSeed, string baseSeed, float price = 10)
        {
            ShopListing baseListing = baseShopListing[baseSeed];
            ShopListing newListing = new ShopListing();
            newListing.name = $"{newSeed.ID} (${price}) (Agriculture, )";
            newListing.OverridePrice = true;
            newListing.OverriddenPrice = price;
            newListing.Item = newSeed;
            newListing.IconTint = new Color(0, 0.859f, 1, 1);
            newListing.MinimumGameCreationVersion = 27;
            newListing.DefaultStock = 1000;
            newListing.CurrentStock = 100000;
            Shop.Listings.Add(newListing);
            Shop.CreateListingUI(newListing);
            Shop.RefreshShownItems();
        }

        public static SeedDefinition SeedDefinitionLoader(UnicornSeedData newSeedData)
        {
            SeedDefinition newSeed = null;
            WeedDefinition weedDef = Registry.GetItem<WeedDefinition>(newSeedData.weedId);
            SeedDefinition seedDef = Registry.GetItem<SeedDefinition>(newSeedData.baseSeedId);

            if (seedDef != null && weedDef != null)
            {
                SeedFactory baseFactory;
                if (factories.ContainsKey(newSeedData.baseSeedId))
                {
                    baseFactory = factories[newSeedData.baseSeedId];
                }
                else
                {
                    baseFactory = new SeedFactory(seedDef);
                    factories.Add(newSeedData.baseSeedId, baseFactory);
                }

                newSeed = baseFactory.CreateSeedDefinition(weedDef);
                if (newSeed == null)
                {
                    Utility.Log("Manager New Seed is null?");
                }
                Singleton<Registry>.Instance.AddToRegistry(newSeed);
                //DiscoveredSeeds.Add(newSeed.ID, newSeedData);
            }
            else
            {
                MelonLogger.Error("Could not parse weed and seed defitions");
            }

            return newSeed;
        }
    }
}
