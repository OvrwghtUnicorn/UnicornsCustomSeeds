using MelonLoader;
using Newtonsoft.Json;
using System.Collections;
using UnicornsCustomSeeds.Seeds;
using UnityEngine;

#if IL2CPP
using Il2Cpp;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Shop;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Messaging;
using ScheduleOne.ObjectScripts;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.UI.Shop;
using GenericCol = System.Collections.Generic;
#endif

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
        
        public static SeedFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredSeeds = new();

        public static ShopInterface Shop = null;
        public static GameObject shopGo = null;

#if IL2CPP
        public static GenericCol.Dictionary<string, SeedDefinition> baseSeedDefinitions = new GenericCol.Dictionary<string, SeedDefinition>();
        public static GenericCol.Dictionary<string, ShopListing> baseShopListing = new GenericCol.Dictionary<string, ShopListing>();
        public delegate bool ValidityCheckDelegate(SendableMessage message, out Il2CppSystem.String invalidReason);
#elif MONO
        public static GenericCol.Dictionary<string, SeedDefinition> baseSeedDefinitions = new GenericCol.Dictionary<string, SeedDefinition>();
        public static GenericCol.Dictionary<string, ShopListing> baseShopListing = new GenericCol.Dictionary<string, ShopListing>();
        public delegate bool ValidityCheckDelegate(SendableMessage message, out String invalidReason);
#endif

        public static bool FirstLoad = false;
        public static void Initialize()
        {
                var shopInterfaces = UnityEngine.Object.FindObjectsOfType<ShopInterface>();
            foreach (ShopInterface shopInterface in shopInterfaces)
            {
                if (shopInterface.gameObject.name == "WeedSupplierInterface")
                {
                    Utility.Log("Found Weed Supplier Interface");
                    shopGo = shopInterface.gameObject;
                    Shop = shopInterface;
                    break;
                }
            }

            if (Shop == null)
            {
                Utility.Error("Shop is null!");
                return;
            }

            ConversationManager.Init();
            SeedQuestManager.Init();

            SeedVisualsManager.LoadSeedMaterial();

            InitDictionary();

            foreach (var seed in DiscoveredSeeds)
            {
                SeedDefinition customDef = Registry.GetItem<SeedDefinition>(seed.Value.seedId);
                if (customDef != null)
                {
                    CreateShopListing(customDef,seed.Value.price);
                }
            }

        }

        public static void ClearAll()
        {
            SeedVisualsManager.seedIcons.Clear();
            SeedVisualsManager.appearanceMap.Clear();
            SeedQuestManager.seedDropoff = null;
            DiscoveredSeeds.Clear();
            factory.DeleteChildren();
            FirstLoad = false;
            //baseSeedDefinitions.Clear();
            //baseShopListing.Clear();
        }

        public static void StartSeedCreation(WeedDefinition weedDef)
        {
            MelonCoroutines.Start(CreateSeed(weedDef));
        }

        public static void BroadcastCustomSeed(UnicornSeedData seed)
        {
            ProductManager prodManager = NetworkSingleton<ProductManager>.Instance;
            string json = JsonConvert.SerializeObject(seed);
            string payload = "[UNISEED]" + json;

            var props = new GenericCol.List<string>();
            var appearance = new WeedAppearanceSettings(
                prodManager.DefaultWeed.MainMat.color,
                prodManager.DefaultWeed.SecondaryMat.color,
                prodManager.DefaultWeed.LeafMat.color,
                prodManager.DefaultWeed.StemMat.color);

            prodManager.CreateWeed_Server(payload, BASE_SEED_ID,
                                             EDrugType.Marijuana, props, appearance);
        }

        public static IEnumerator CreateSeed(WeedDefinition weedDef)
        {
            yield return new WaitForSeconds(StashManager.SynthesizeTime.Value);

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
            CreateShopListing(newSeed, price);

            var pots = GameObject.FindObjectsOfType<Pot>();
            foreach ( Pot pot in pots)
            {
#if IL2CPP
                if (pot.Configuration.TryCast<PotConfiguration>() is PotConfiguration config) {
#elif MONO
                if (pot.Configuration is PotConfiguration config) {
#endif
                    config.Seed.Options.Add(newSeed);
                }
            }

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
#if IL2CPP
                if (listing.Item.TryCast<SeedDefinition>() is SeedDefinition seed)
#elif MONO
                if (listing.Item is SeedDefinition seed)
#endif
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





        public static void CreateShopListing(SeedDefinition newSeed, float price = 10)
        {
            ShopListing baseListing = Shop.Listings[0];
            baseListing.name = "Test";
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

        public static void SeedFactoryLoader()
        {
            var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
            if (baseSeed != null)
            {
                factory = new SeedFactory(baseSeed);
            }
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
