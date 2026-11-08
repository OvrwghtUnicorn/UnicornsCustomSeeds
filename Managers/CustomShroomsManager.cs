using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2Cpp;
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Shop;
using Il2CppScheduleOne.UI.Phone;
using Il2CppScheduleOne.UI.Phone.Messages;
using Il2CppScheduleOne.UI.Phone.Delivery;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.ObjectScripts;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Shop;
using ScheduleOne.UI.Phone;
using ScheduleOne.UI.Phone.Messages;
using ScheduleOne.UI.Phone.Delivery;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class CustomShroomsManager
    {
        public const string BASE_SYRINGE_ID = "sporesyringe";

        public static SyringeFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredShrooms = new();

        public static ShopInterface PhilShop = null;
        public static GameObject PhilShopGo = null;
        public static Phil phil = null;

        /// <summary>
        /// See CustomSeedsManager.Initialize — this runs on the shared
        /// LoadManager.onLoadComplete UnityEvent, where an escaping exception would abort
        /// the remaining listeners and hang a client's load.
        /// </summary>
        public static void Initialize()
        {
            // Before this save creates anything: if entries from a previous save are still
            // here, the prefab edit leaked across the menu.
            LogSpawnStationPrefabState("on load");

            // The prefab is process-scoped, so a previous save's custom syringes are still
            // whitelisted on it. Put it back to vanilla before this save appends its own.
            RestoreSpawnStationPrefabBaseline();

            try { InitializeInternal(); }
            catch (Exception e)
            {
                Utility.Error("CustomShroomsManager.Initialize failed — continuing so other listeners still run.");
                Utility.PrintException(e);
            }
        }

        private static void InitializeInternal()
        {
            phil = GameObject.FindObjectOfType<Phil>();
            if (phil != null)
            {
                PhilShop = phil.Shop;
                PhilShopGo = PhilShop?.gameObject;

                if (PhilShop == null)
                    Utility.Error("CustomShroomsManager: Phil's shop is null!");

                // Register Phil's conversation
                if (phil.MSGConversation != null)
                    ConversationManager.RegisterConversation("Phil", phil.MSGConversation);

                ShroomQuestManager.Init();

                // Reload any syringes that were discovered in a previous session
                foreach (var kvp in DiscoveredShrooms)
                {
                    if (!Registry.ItemExists(kvp.Value.seedId))
                        SyringeDefinitionLoader(kvp.Value);
                    else
                    {
                        var existing = Registry.GetItem<SporeSyringeDefinition>(kvp.Value.seedId);
                        AddSyringeToSpawnStations(existing);
                        CreateShopListing(existing, kvp.Value.price);
                    }
                }
            }
            else
            {
                Utility.Error("CustomShroomsManager: Could not find Phil.");
            }
        }

        /// <summary>
        /// Rebuilds a SporeSyringeDefinition from a saved UnicornSeedData record.
        /// Called by the persistence patch after the game replays CreateShroom on load.
        /// </summary>
        public static SporeSyringeDefinition SyringeDefinitionLoader(UnicornSeedData data)
        {
            if (factory == null)
            {
                Utility.Error($"SyringeDefinitionLoader: factory is null for '{data.mixId}'.");
                return null;
            }

            ShroomDefinition shroomDef = Registry.GetItem<ShroomDefinition>(data.mixId);
            if (shroomDef == null)
            {
                Utility.Error($"SyringeDefinitionLoader: Could not resolve ShroomDefinition '{data.mixId}'.");
                return null;
            }

            SporeSyringeDefinition newSyringe = factory.CreateSyringeDefinition(shroomDef);
            if (newSyringe == null) return null;

            Singleton<Registry>.Instance.AddToRegistry(newSyringe);

            try { Singleton<ManagementUtilities>.Instance.MushroomSpawns.Add(newSyringe.SpawnDefinition); }
            catch (Exception ex) { Utility.PrintException(ex); }

            AddSyringeToSpawnStations(newSyringe);
            CreateShopListing(newSyringe, data.price);
            Utility.Log($"SyringeDefinitionLoader: Reloaded syringe '{newSyringe.ID}'.");
            return newSyringe;
        }

        public static void StartSyringeCreation(ShroomDefinition shroomDef)
        {
            MelonCoroutines.Start(CreateSyringe(shroomDef));
        }

        public static IEnumerator CreateSyringe(ShroomDefinition shroomDef)
        {
            yield return new WaitForSeconds(StashManager.SynthesizeTime.Value);

            if (factory == null)
            {
                Utility.Error("SyringeFactory is null!");
                yield break;
            }

            SporeSyringeDefinition newSyringe = factory.CreateSyringeDefinition(shroomDef);
            if (newSyringe == null)
            {
                Utility.Error("Failed to create custom syringe definition.");
                yield break;
            }
            Utility.Log($"Created new syringe definition: {newSyringe.ID} for shroom: {shroomDef.ID}");
            Singleton<Registry>.Instance.AddToRegistry(newSyringe);

            Singleton<ManagementUtilities>.Instance.MushroomSpawns.Add(newSyringe.SpawnDefinition);
            AddSpawnToMushroomBeds(newSyringe.SpawnDefinition);

            // Add custom syringe to all spawn stations so they accept it
            AddSyringeToSpawnStations(newSyringe);
            newSyringe.BasePurchasePrice += StashManager.GetIngredientCost(shroomDef);
            UnicornSeedData newData = new UnicornSeedData
            {
                mixId = shroomDef.ID,
                drugType = EDrugType.Shrooms,
            };
            newData.SetSingleVariant(BASE_SYRINGE_ID, newSyringe.ID, newSyringe.BasePurchasePrice);
            DiscoveredShrooms.Add(newData.mixId, newData);
            CreateShopListing(newSyringe, newData.price);

            DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomDrop != null && InstanceFinder.IsServer)
            {
                ItemInstance defaultInstance = newSyringe.GetDefaultInstance();
                defaultInstance.SetQuantity(3);
                randomDrop.Storage.InsertItem(defaultInstance, true);
                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
                ConversationManager.SendMessage("Phil", $"{shroomDef.name} syringe synthesized and placed in a dead drop.");
            }
            else
            {
                Utility.Error("No available dead drop for syringe placement.");
            }

            NetworkSyncManager.Broadcast(newData);
        }

        /// <summary>
        /// Pushes a new ShroomSpawnDefinition into every MushroomBed already present in the
        /// scene, mirroring CustomSeedsManager.AddSeedToPots. MushroomBedConfiguration.Spawn.Options
        /// is only populated from ManagementUtilities.MushroomSpawns when a bed's config first
        /// initializes, so beds already spawned need the new spawn definition pushed directly.
        /// </summary>
        public static void AddSpawnToMushroomBeds(ShroomSpawnDefinition newSpawn)
        {
            var beds = GameObject.FindObjectsOfType<MushroomBed>();
            foreach (MushroomBed bed in beds)
            {
#if IL2CPP
                if (bed.Configuration.TryCast<MushroomBedConfiguration>() is MushroomBedConfiguration config)
                {
#elif MONO
                if (bed.Configuration is MushroomBedConfiguration config) {
#endif
                    config.Spawn.Options.Add(newSpawn);
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // TEST — does updating the PREFAB reach stations placed later?
        //
        // MushroomSpawnStation.InitializeGridItem builds its syringe filter fresh per
        // instance from the serialized _validSporeSyringeDefinitions array:
        //
        //     foreach (SporeSyringeDefinition d in this._validSporeSyringeDefinitions)
        //         list.Add(d.ID);
        //     this.SyringeSlot.AddFilter(new ItemFilter_ID(list));
        //
        // So patching live instances (below) can never reach a station placed afterwards.
        // If the prefab's array is the source every new instance reads from, appending to
        // it should fix new stations permanently. That is what this verifies.
        //
        // Reached by scanning the Registry rather than by item ID, so the log also tells us
        // what that ID actually is.
        // ─────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Pristine vanilla contents, captured the first time the prefab is seen in this
        /// process. Plain strings on purpose: a cached MushroomSpawnStation reference could
        /// go stale across a scene change, which is the very thing being measured.
        /// </summary>
        private static string[] spawnStationPrefabBaseline = null;

        /// <summary>
        /// Dumps the prefab's current syringe list. Call on load and on teardown to see
        /// whether edits made in one save are still present in the next.
        ///
        /// Re-scans every call rather than caching the prefab — a stale cached reference
        /// would confound exactly the result this is testing for.
        /// </summary>
        public static void LogSpawnStationPrefabState(string when)
        {
            try
            {
                var all = Singleton<Registry>.Instance?.GetAllItems();
                if (all == null)
                {
                    Utility.Error($"[PREFABTEST] {when}: Registry.GetAllItems() returned null.");
                    return;
                }

                foreach (ItemDefinition def in all)
                {
#if IL2CPP
                    BuildableItemDefinition buildable = def?.TryCast<BuildableItemDefinition>();
#elif MONO
                    BuildableItemDefinition buildable = def as BuildableItemDefinition;
#endif
                    if (buildable == null || buildable.BuiltItem == null) continue;

#if IL2CPP
                    MushroomSpawnStation st = buildable.BuiltItem.TryCast<MushroomSpawnStation>();
#elif MONO
                    MushroomSpawnStation st = buildable.BuiltItem as MushroomSpawnStation;
#endif
                    if (st == null) continue;

                    var cur = st._validSporeSyringeDefinitions;
                    var ids = new List<string>();
                    if (cur != null)
                        foreach (var d in cur) ids.Add(d != null ? d.ID : "<null>");

                    string baselineNote = spawnStationPrefabBaseline == null
                        ? "baseline not yet captured"
                        : $"baseline={spawnStationPrefabBaseline.Length}";

                    Utility.Log($"[PREFABTEST] {when}: prefab '{buildable.ID}' has {ids.Count} entries " +
                                $"({baselineNote}) -> [{string.Join(", ", ids)}]");
                    return;
                }

                Utility.Error($"[PREFABTEST] {when}: no MushroomSpawnStation prefab found in Registry.");
            }
            catch (Exception e)
            {
                Utility.Error($"[PREFABTEST] {when}: dump threw.");
                Utility.PrintException(e);
            }
        }

        /// <summary>
        /// Puts the prefab's syringe list back to the vanilla contents captured by
        /// AddSyringeToSpawnStationPrefab. Called on load, where the Registry is known to be
        /// alive, rather than on teardown.
        ///
        /// Restores to a snapshot rather than removing entries, so running it twice is a
        /// no-op. All-or-nothing: if any baseline ID no longer resolves, the prefab is left
        /// untouched instead of being narrowed.
        /// </summary>
        public static void RestoreSpawnStationPrefabBaseline()
        {
            // Nothing captured means nothing was ever appended in this process.
            if (spawnStationPrefabBaseline == null) return;

            try
            {
                var all = Singleton<Registry>.Instance?.GetAllItems();
                if (all == null)
                {
                    Utility.Error("[PREFABTEST] restore: Registry.GetAllItems() returned null.");
                    return;
                }

                var vanilla = new List<SporeSyringeDefinition>();
                foreach (string id in spawnStationPrefabBaseline)
                {
                    var def = Registry.GetItem<SporeSyringeDefinition>(id);
                    if (def == null)
                    {
                        Utility.Error($"[PREFABTEST] restore: baseline syringe '{id}' no longer in Registry " +
                                      "— leaving the prefab untouched.");
                        return;
                    }
                    vanilla.Add(def);
                }

                foreach (ItemDefinition def in all)
                {
#if IL2CPP
                    BuildableItemDefinition buildable = def?.TryCast<BuildableItemDefinition>();
#elif MONO
                    BuildableItemDefinition buildable = def as BuildableItemDefinition;
#endif
                    if (buildable == null || buildable.BuiltItem == null) continue;

#if IL2CPP
                    MushroomSpawnStation station = buildable.BuiltItem.TryCast<MushroomSpawnStation>();
#elif MONO
                    MushroomSpawnStation station = buildable.BuiltItem as MushroomSpawnStation;
#endif
                    if (station == null) continue;

                    int before = station._validSporeSyringeDefinitions != null
                        ? station._validSporeSyringeDefinitions.Length
                        : 0;

                    if (before == vanilla.Count) continue;   // already vanilla

                    station._validSporeSyringeDefinitions = vanilla.ToArray();
                    Utility.Log($"[PREFABTEST] restore: prefab '{buildable.ID}' _validSporeSyringeDefinitions " +
                                $"{before} -> {vanilla.Count}, back to vanilla.");
                    return;
                }

                Utility.Error("[PREFABTEST] restore: no MushroomSpawnStation prefab found in Registry.");
            }
            catch (Exception e)
            {
                Utility.Error("[PREFABTEST] restore threw.");
                Utility.PrintException(e);
            }
        }

        public static void AddSyringeToSpawnStationPrefab(SporeSyringeDefinition newSyringe)
        {
            try
            {
                var all = Singleton<Registry>.Instance?.GetAllItems();
                if (all == null)
                {
                    Utility.Error("[PREFABTEST] Registry.GetAllItems() returned null.");
                    return;
                }

                int prefabsFound = 0;

                foreach (ItemDefinition def in all)
                {
#if IL2CPP
                    BuildableItemDefinition buildable = def?.TryCast<BuildableItemDefinition>();
#elif MONO
                    BuildableItemDefinition buildable = def as BuildableItemDefinition;
#endif
                    if (buildable == null || buildable.BuiltItem == null) continue;

#if IL2CPP
                    MushroomSpawnStation station = buildable.BuiltItem.TryCast<MushroomSpawnStation>();
#elif MONO
                    MushroomSpawnStation station = buildable.BuiltItem as MushroomSpawnStation;
#endif
                    if (station == null) continue;

                    prefabsFound++;

                    // Capture the vanilla list once, before this process ever appends to it.
                    if (spawnStationPrefabBaseline == null)
                    {
                        var baseIds = new List<string>();
                        if (station._validSporeSyringeDefinitions != null)
                            foreach (var d in station._validSporeSyringeDefinitions)
                                if (d != null) baseIds.Add(d.ID);

                        spawnStationPrefabBaseline = baseIds.ToArray();
                        Utility.Log($"[PREFABTEST] captured baseline for '{buildable.ID}': " +
                                    $"{spawnStationPrefabBaseline.Length} entries -> [{string.Join(", ", spawnStationPrefabBaseline)}]");
                    }

                    var current = station._validSporeSyringeDefinitions;
                    int before = current != null ? current.Length : 0;

                    bool already = false;
                    if (current != null)
                    {
                        foreach (var existing in current)
                            if (existing != null && existing.ID == newSyringe.ID) { already = true; break; }
                    }

                    if (already)
                    {
                        Utility.Log($"[PREFABTEST] prefab '{buildable.ID}' already lists '{newSyringe.ID}' ({before} entries).");
                        continue;
                    }

                    // Array, not List — append means replacing it.
                    var updated = HarmonyLib.CollectionExtensions.AddItem(current, newSyringe);
                    station._validSporeSyringeDefinitions = updated.ToArray();

                    Utility.Log($"[PREFABTEST] prefab '{buildable.ID}' _validSporeSyringeDefinitions " +
                                $"{before} -> {station._validSporeSyringeDefinitions.Length}, added '{newSyringe.ID}'.");
                }

                if (prefabsFound == 0)
                    Utility.Error("[PREFABTEST] no BuildableItemDefinition with a MushroomSpawnStation BuiltItem found.");
            }
            catch (Exception e)
            {
                Utility.Error("[PREFABTEST] prefab update threw.");
                Utility.PrintException(e);
            }
        }

        public static void AddSyringeToSpawnStations(SporeSyringeDefinition newSyringe)
        {
            // Stations placed AFTER this call rebuild their filter from the prefab, so the
            // prefab has to be updated too — that is the half this test is checking.
            AddSyringeToSpawnStationPrefab(newSyringe);

            var stations = GameObject.FindObjectsOfType<MushroomSpawnStation>();
            foreach (MushroomSpawnStation station in stations)
            {
                if (station.SyringeSlot != null)
                {
                    // Find the ItemFilter_ID on the syringe slot and add our custom ID
                    foreach (var filter in station.SyringeSlot.HardFilters)
                    {
#if IL2CPP
                        ItemFilter_ID idFilter = filter.TryCast<ItemFilter_ID>();
#elif MONO
                        ItemFilter_ID idFilter = filter as ItemFilter_ID;
#endif
                        if (idFilter != null && !idFilter.IDs.Contains(newSyringe.ID))
                        {
                            idFilter.IDs.Add(newSyringe.ID);
                        }
                    }
                }
            }
        }

        public static void CreateShopListing(SporeSyringeDefinition newSyringe, float price = 10f)
        {
            if (PhilShop == null) return;

            ShopListing newListing = new ShopListing();
            newListing.name = $"{newSyringe.ID} (${price}) (Shrooms, )";
            newListing.Item = newSyringe;
            newListing.IconTint = new Color(0.6f, 0.2f, 0.8f, 1f);
            newListing.MinimumGameCreationVersion = 27;
            newListing.DefaultStock = 1000;
            newListing.CurrentStock = 100000;
            newListing.CanBeDelivered = true;
            PhilShop.Listings.Add(newListing);
            PhilShop.CreateListingUI(newListing);
            CreatePhoneShopListing(newSyringe);
            CreateDeliveryListing(newListing);
            PhilShop.RefreshShownItems();
        }

        public static void CreatePhoneShopListing(SporeSyringeDefinition newSyringe)
        {
            if (phil == null) return;
            PhoneShopInterface.Listing newEntry = new PhoneShopInterface.Listing(newSyringe);
            var updated = HarmonyLib.CollectionExtensions.AddItem(phil.SupplierData.DeliveryShopListings, newEntry);
            phil.SupplierData.DeliveryShopListings = updated.ToArray();
        }

        public static void CreateDeliveryListing(ShopListing newListing)
        {
            if (PhilShop == null) return;
            var deliveryShop = PlayerSingleton<DeliveryApp>.Instance?.GetShop(PhilShop.ShopName);
            if (deliveryShop == null) return;
            ListingEntry entry = UnityEngine.Object.Instantiate<ListingEntry>(deliveryShop.ListingEntryPrefab, deliveryShop.ListingContainer);
            entry.Initialize(newListing);
            entry.onQuantityChanged.AddListener((UnityEngine.Events.UnityAction)deliveryShop.RefreshCart);
            deliveryShop.listingEntries.Add(entry);
            deliveryShop.ListingContainer.sizeDelta = new Vector2(deliveryShop.ListingContainer.sizeDelta.x, 230f + (float)Math.Ceiling(deliveryShop.listingEntries.Count / 2.0) * 60f);
        }

        public static void ClearAll()
        {
            LogSpawnStationPrefabState("on teardown");

            DiscoveredShrooms.Clear();
            SeedVisualsManager.shroomAppearanceMap.Clear();
            PhilShop = null;
            PhilShopGo = null;
            phil = null;
            if (factory != null) factory.DeleteChildren();
        }
    }
}