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
using UnicornsCustomSeeds.Seeds;
using UnityEngine;
using Il2Generic = Il2CppSystem.Collections.Generic;

namespace UnicornsCustomSeeds.Managers
{

    public struct SeedComponents
    {
        public string seedId;
        public string baseSeedId;
    }

    public static class CustomSeedsManager
    {
        public const string BASE_SEED_ID = "ogkushseed";
        
        // Commit ID where this was working: 371da19bae50ee14c40430cf9e333c01f93262e1
        public static Queue<SeedDefinition> SeedQueue = new Queue<SeedDefinition>();
        public static SeedFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredSeeds = new();

        public static ShopInterface Shop = null;
        public static GameObject shopGo = null;

        public static Il2Generic.Dictionary<string, SeedDefinition> baseSeedDefinitions = new Il2Generic.Dictionary<string, SeedDefinition>();
        public static Il2Generic.Dictionary<string, ShopListing> baseShopListing = new Il2Generic.Dictionary<string, ShopListing>();
        public delegate bool ValidityCheckDelegate(SendableMessage message, out Il2CppSystem.String invalidReason);

        public static bool FirstLoad = false;
        public static void Initialize()
        {
                var shopInterfaces = UnityEngine.Object.FindObjectsOfType<ShopInterface>();
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

            ConversationManager.Init();
            SeedQuestManager.Init();

            SeedVisualsManager.LoadSeedMaterial();

            InitDictionary();
            if (!baseSeedDefinitions.ContainsKey(BASE_SEED_ID))
            {
                MelonLogger.Msg($"{BASE_SEED_ID} doesn't exist!");
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
            SeedVisualsManager.seedIcons.Clear();
            SeedVisualsManager.appearanceMap.Clear();
            DiscoveredSeeds.Clear();
            SeedQueue.Clear();
            //factories.Clear();
            //baseSeedDefinitions.Clear();
            //baseShopListing.Clear();
        }

        public static void StartSeedCreation(WeedDefinition weedDef)
        {
            MelonCoroutines.Start(CreateSeed(10f, weedDef));
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

            prodManager.CreateWeed_Server(payload, BASE_SEED_ID,
                                             EDrugType.Marijuana, props, appearance);
        }

        public static IEnumerator CreateSeed(float delaySeconds, WeedDefinition weedDef)
        {
            yield return new WaitForSeconds(delaySeconds);

            var newSeed = factory.CreateSeedDefinition(weedDef);

            Singleton<Registry>.Instance.AddToRegistry(newSeed);
            float price = StashManager.GetIngredientCost(weedDef);
            UnicornSeedData newSeedData = new UnicornSeedData
            {
                seedId = newSeed.ID,
                weedId = weedDef.ID,
                baseSeedId = BASE_SEED_ID,
                price = price,
            };
            DiscoveredSeeds.Add(newSeed.ID, newSeedData);
            CreateShopListing(newSeed, BASE_SEED_ID, price);
            SeedQueue.Enqueue(newSeed);



            DeadDrop randomEmptyDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomEmptyDrop != null)
            {
                ItemInstance defaultInstance = newSeed.GetDefaultInstance();
                defaultInstance.SetQuantity(10);
                randomEmptyDrop.Storage.InsertItem(defaultInstance, true);
                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomEmptyDrop.GUID.ToString(), guidString);
                SeedQuestManager.SendMessage($"{weedDef.name} is synthesized and placed in the deaddrop");
            }
            else
            {
                SeedQuestManager.SendMessage($"{weedDef.name} is synthesized and available in the shop");
            }

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

            if (weedDef != null)
            {

                newSeed = factory.CreateSeedDefinition(weedDef);
                if (newSeed == null)
                {
                    Utility.Log("New Seed returned null?");
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
