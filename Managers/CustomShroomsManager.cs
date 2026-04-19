using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.StationFramework;


#if IL2CPP
using Il2Cpp;
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.UI.Shop;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Messaging;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.StationFramework;
using ScheduleOne.UI.Shop;
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

        public static void Initialize()
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

                // Add sendable message for testing
                SetupPhilConversation();
            }
            else
            {
                Utility.Error("CustomShroomsManager: Could not find Phil.");
            }
        }

        private static void SetupPhilConversation()
        {
            MSGConversation convo = ConversationManager.GetConversation("Phil");
            if (convo != null)
            {
                SendableMessage sendable = convo.CreateSendableMessage("Synthesize Shrooms");
                sendable.onSent += (Action)OnShroomSynthesisRequested;
            }
        }

        public static void OnShroomSynthesisRequested()
        {
            // TESTING: hardcoded to ultramonkey
            ShroomDefinition shroomDef = Registry.GetItem<ShroomDefinition>("ultramonkey");
            if (shroomDef == null)
            {
                Utility.Error("CustomShroomsManager: Could not find ultramonkey ShroomDefinition.");
                return;
            }
            MelonCoroutines.Start(CreateSyringe(shroomDef));
        }

        public static void StartSyringeCreation(ShroomDefinition shroomDef)
        {
            MelonCoroutines.Start(CreateSyringe(shroomDef));
        }

        public static IEnumerator CreateSyringe(ShroomDefinition shroomDef)
        {
            yield return new WaitForSeconds(5f);

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

            // Add custom syringe to all spawn stations so they accept it
            AddSyringeToSpawnStations(newSyringe);

            UnicornSeedData newData = new UnicornSeedData
            {
                seedId = newSyringe.ID,
                mixId = shroomDef.ID,
                drugType = EDrugType.Shrooms,
                price = 10f,
            };
            DiscoveredShrooms.Add(newData.mixId, newData);

            // Place in a dead drop for testing retrieval
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
        }

        public static void AddSyringeToSpawnStations(SporeSyringeDefinition newSyringe)
        {
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

        public static void ClearAll()
        {
            DiscoveredShrooms.Clear();
            PhilShop = null;
            PhilShopGo = null;
            phil = null;
            if (factory != null) factory.DeleteChildren();
        }
    }
}
