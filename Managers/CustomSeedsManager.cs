using MelonLoader;
using Newtonsoft.Json;
using System.Collections;
using UnicornsCustomSeeds.Seeds;
using UnityEngine;
using Il2CppFishNet;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.UI.Phone;
using Harmony;
using Il2CppScheduleOne.UI.Phone.Messages;
using UnityEngine.UI;
using Il2CppScheduleOne.UI.Phone.Delivery;
using UnityEngine.Events;

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

            AddScrollToPhoneInterface();            

            foreach (var seed in DiscoveredSeeds)
            {
                SeedDefinition customDef = Registry.GetItem<SeedDefinition>(seed.Value.seedId);
                if (customDef != null)
                {
                    CreateShopListing(customDef, seed.Value.price);
                    if (ConversationManager.albert != null)
                    {
                        PhoneShopInterface.Listing newSeed = new PhoneShopInterface.Listing(customDef);
                        var updatedItems = HarmonyLib.CollectionExtensions.AddItem(ConversationManager.albert.OnlineShopItems, newSeed);
                        ConversationManager.albert.OnlineShopItems = updatedItems.ToArray();


                    }
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

        public static void DebugPhoneInterfaceLayout()
        {
            var phoneShopTransform = PlayerSingleton<MessagesApp>.Instance.PhoneShopInterface.transform;
            Transform shopEntries = phoneShopTransform.Find("Shade/Content/Entries");

            if (shopEntries == null)
            {
                Utility.Error("Could not find Shade/Content/Entries");
                return;
            }

            GameObject entries = shopEntries.gameObject;
            RectTransform entriesRect = entries.GetComponent<RectTransform>();

            if (entriesRect == null)
            {
                Utility.Error("Entries does not have a RectTransform component");
                return;
            }

            // === ENTRIES GAMEOBJECT ===
            Utility.Log("========== ENTRIES GAMEOBJECT ==========");
            Utility.Log($"Name: {entries.name}");
            Utility.Log($"AnchorMin: {entriesRect.anchorMin}");
            Utility.Log($"AnchorMax: {entriesRect.anchorMax}");
            Utility.Log($"Pivot: {entriesRect.pivot}");
            Utility.Log($"AnchoredPosition: {entriesRect.anchoredPosition}");
            Utility.Log($"SizeDelta: {entriesRect.sizeDelta}");
            Utility.Log($"Rect: {entriesRect.rect}");

            // Vertical Layout Group
            VerticalLayoutGroup layoutGroup = entries.GetComponent<VerticalLayoutGroup>();
            if (layoutGroup != null)
            {
                Utility.Log("--- Vertical Layout Group ---");
                Utility.Log($"ChildAlignment: {layoutGroup.childAlignment}");
                Utility.Log($"Spacing: {layoutGroup.spacing}");
                Utility.Log($"Padding: L:{layoutGroup.padding.left} R:{layoutGroup.padding.right} T:{layoutGroup.padding.top} B:{layoutGroup.padding.bottom}");
                Utility.Log($"ChildControlWidth: {layoutGroup.childControlWidth}");
                Utility.Log($"ChildControlHeight: {layoutGroup.childControlHeight}");
                Utility.Log($"ChildForceExpandWidth: {layoutGroup.childForceExpandWidth}");
                Utility.Log($"ChildForceExpandHeight: {layoutGroup.childForceExpandHeight}");
            }

            // === PARENT GAMEOBJECT ===
            if (entriesRect.parent != null)
            {
                Utility.Log("\n========== PARENT GAMEOBJECT ==========");
                RectTransform parentRect = entriesRect.parent.GetComponent<RectTransform>();
                if (parentRect != null)
                {
                    Utility.Log($"Name: {parentRect.gameObject.name}");
                    Utility.Log($"AnchorMin: {parentRect.anchorMin}");
                    Utility.Log($"AnchorMax: {parentRect.anchorMax}");
                    Utility.Log($"Pivot: {parentRect.pivot}");
                    Utility.Log($"AnchoredPosition: {parentRect.anchoredPosition}");
                    Utility.Log($"SizeDelta: {parentRect.sizeDelta}");
                    Utility.Log($"Rect: {parentRect.rect}");
                }
            }

            // === FIRST CHILD GAMEOBJECT ===
            if (entriesRect.childCount > 0)
            {
                Utility.Log("\n========== FIRST CHILD GAMEOBJECT ==========");
                Transform firstChild = entriesRect.GetChild(0);
                RectTransform firstChildRect = firstChild.GetComponent<RectTransform>();

                if (firstChildRect != null)
                {
                    Utility.Log($"Name: {firstChild.name}");
                    Utility.Log($"AnchorMin: {firstChildRect.anchorMin}");
                    Utility.Log($"AnchorMax: {firstChildRect.anchorMax}");
                    Utility.Log($"Pivot: {firstChildRect.pivot}");
                    Utility.Log($"AnchoredPosition: {firstChildRect.anchoredPosition}");
                    Utility.Log($"SizeDelta: {firstChildRect.sizeDelta}");
                    Utility.Log($"Rect: {firstChildRect.rect}");
                }
                else
                {
                    Utility.Log($"First child '{firstChild.name}' does not have RectTransform");
                }
            }
            else
            {
                Utility.Log("\n========== NO CHILDREN ==========");
            }

            Utility.Log("\n========================================");
        }

        public static void AddScrollToPhoneInterface()
        {
            var phoneShopTransform = PlayerSingleton<MessagesApp>.Instance.PhoneShopInterface.transform;
            Transform shopEntries = phoneShopTransform.Find("Shade/Content/Entries");
            if (shopEntries != null)
            {
                GameObject entries = shopEntries.gameObject;

                // Get the RectTransform of entries to copy dimensions
                RectTransform entriesRect = entries.GetComponent<RectTransform>();
                Image entriesImage = entries.GetComponent<Image>();
                if (entriesRect == null)
                {
                    Utility.Error("Entries does not have a RectTransform component");
                    return;
                }

                // Create new GameObject for ScrollRect
                GameObject scrollViewGO = new GameObject("ScrollView");
                scrollViewGO.transform.SetParent(entriesRect.parent, false);

                // Add RectTransform and copy dimensions from entries
                RectTransform scrollViewRect = scrollViewGO.AddComponent<RectTransform>();
                scrollViewRect.anchorMin = new Vector2(0,1);
                scrollViewRect.anchorMax = new Vector2(1, 1);
                scrollViewRect.pivot = new Vector2(0.5f, 1);
                scrollViewRect.anchoredPosition = new Vector2(0, -140);
                scrollViewRect.sizeDelta = entriesRect.sizeDelta;

                // Add Mask component
                scrollViewGO.AddComponent<Mask>();
                entries.GetComponent<Mask>().enabled = false;

                // Add Image component (required for Mask)
                Image maskImage = scrollViewGO.AddComponent<Image>();
                maskImage.color = entriesImage.color; // Transparent

                // Add ScrollRect component
                ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = true;
                scrollRect.scrollSensitivity = 20f;
                
                entriesRect.pivot = new Vector2(0.5f, 1);
                //entriesRect.anchorMax = new Vector2(0.5f, 1);
                //entriesRect.anchorMin = new Vector2(0,0.5f);

                var temp = entries.AddComponent<ContentSizeFitter>();
                if(temp != null)
                {
                    temp.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                VerticalLayoutGroup layoutGroup = entries.GetComponent<VerticalLayoutGroup>();
                layoutGroup.childAlignment = TextAnchor.UpperCenter;
                layoutGroup.spacing = 20;
                layoutGroup.childForceExpandHeight = false;
                layoutGroup.childForceExpandWidth = false;
                layoutGroup.childControlHeight = false;
                layoutGroup.childControlWidth = false;
                layoutGroup.padding.top = 20;
                layoutGroup.padding.bottom = 20;
                // Reparent entries to be child of ScrollView
                entries.transform.SetParent(scrollViewGO.transform, false);

                // Set entries as the content of the ScrollRect
                scrollRect.content = entriesRect;

                // Reset entries position to top
                entriesRect.anchoredPosition = Vector2.zero;

                Utility.Log("ScrollRect successfully added to phone shop interface");
            }
            else
            {
                Utility.Error("Could not find Shade/Content/Entries in phone shop interface");
            }
        }

        public static void StartSeedCreation(WeedDefinition weedDef)
        {
            MelonCoroutines.Start(CreateSeed(weedDef));
        }

        public static void BroadcastCustomSeed(UnicornSeedData seed)
        {
            ProductManager prodManager = NetworkSingleton<ProductManager>.Instance;
            string json = JsonConvert.SerializeObject(seed);
            string payload = "[NET-JSON]" + json;

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
            DiscoveredSeeds.Add(newSeedData.weedId, newSeedData);
            CreateShopListing(newSeed, price);
            AddSeedToPots(newSeed);

            DeadDrop randomEmptyDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomEmptyDrop != null && InstanceFinder.IsServer)
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
                Utility.Error("No Dead Drop found");
                SeedQuestManager.SendMessage($"{weedDef.name} is synthesized and available in the shop");
            }
            BroadcastCustomSeed(newSeedData);

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



        public static void CreateDeliveryListing(ShopListing newListing)
        {
            var albertDeliveryShop = PlayerSingleton<DeliveryApp>.Instance.GetShop("Albert Hoover");
            ListingEntry listingEntry = UnityEngine.Object.Instantiate<ListingEntry>(albertDeliveryShop.ListingEntryPrefab, albertDeliveryShop.ListingContainer);
            listingEntry.Initialize(newListing);
            listingEntry.onQuantityChanged.AddListener((UnityAction)albertDeliveryShop.RefreshCart);
            albertDeliveryShop.listingEntries.Add(listingEntry);
            albertDeliveryShop.ContentsContainer.sizeDelta = new Vector2(albertDeliveryShop.ContentsContainer.sizeDelta.x, 230f + (float)Math.Ceiling(albertDeliveryShop.listingEntries.Count / 2.0) * 60f);
            Utility.Log($"SizeDelta: {albertDeliveryShop.ContentsContainer.sizeDelta}, Count: {albertDeliveryShop.listingEntries.Count}");
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
            newListing.CanBeDelivered = true;
            Shop.Listings.Add(newListing);
            Shop.CreateListingUI(newListing);
            CreateDeliveryListing(newListing);
            Shop.RefreshShownItems();
        }

        public static void AddSeedToPots(SeedDefinition newSeed)
        {
            var pots = GameObject.FindObjectsOfType<Pot>();
            foreach (Pot pot in pots)
            {
#if IL2CPP
                if (pot.Configuration.TryCast<PotConfiguration>() is PotConfiguration config)
                {
#elif MONO
                if (pot.Configuration is PotConfiguration config) {
#endif
                    config.Seed.Options.Add(newSeed);
                }
            }
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
                    Utility.Error("New seed not created");
                }
                Singleton<Registry>.Instance.AddToRegistry(newSeed);
            }
            else
            {
                MelonLogger.Error("Could not parse weed and seed defitions");
            }

            return newSeed;
        }
    }
}
