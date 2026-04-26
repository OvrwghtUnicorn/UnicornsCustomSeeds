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
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Misc;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using Il2CppScheduleOne.UI.Shop;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Economy;
using ScheduleOne.ItemFramework;
using ScheduleOne.Messaging;
using ScheduleOne.Misc;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using ScheduleOne.UI.Shop;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class CustomCocaSeedsManager
    {
        public const string BASE_SEED_ID = "cocaseed";
        public const string BASE_LEAF_ID = "cocaleaf";
        public const string BASE_BASE_ID = "cocainebase";
        // Hardcoded test target — verify this ID via Registry logging at startup
        public const string TEST_COCAINE_MIX_ID = "bighaze";

        public static CocaFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredCocaSeeds = new Dictionary<string, UnicornSeedData>();

        public static ShopInterface SalvadorShop = null;
        public static Salvador salvador = null;

        public static void Initialize()
        {
            salvador = GameObject.FindObjectOfType<Salvador>();
            if (salvador != null)
            {
                SalvadorShop = salvador.Shop;

                if (SalvadorShop == null)
                    Utility.Error("CustomCocaSeedsManager: Salvador's shop is null!");

                if (salvador.MSGConversation != null)
                    ConversationManager.RegisterConversation("Salvador", salvador.MSGConversation);

                SetupSalvadorConversation();
            }
            else
            {
                Utility.Error("CustomCocaSeedsManager: Could not find Salvador NPC in scene. Verify Salvador is present and unlocked.");
            }
        }

        private static void SetupSalvadorConversation()
        {
            MSGConversation convo = ConversationManager.GetConversation("Salvador");
            if (convo != null)
            {
                SendableMessage sendable = convo.CreateSendableMessage("Synthesize Coca");
                sendable.onSent += (Action)OnCocaSynthesisRequested;
                Utility.Log("CustomCocaSeedsManager: 'Synthesize Coca' message registered on Salvador.");
            }
            else
            {
                Utility.Error("CustomCocaSeedsManager: Could not get Salvador conversation from ConversationManager.");
            }
        }

        public static void OnCocaSynthesisRequested()
        {
            // TESTING ONLY: hardcoded to TEST_COCAINE_MIX_ID
            // Phase 2 will resolve this from the player's discovered cocaine mixes
            // Log Registry.GetItem(TEST_COCAINE_MIX_ID)?.GetType().Name to confirm type
            ProductDefinition cocaineDef = Registry.GetItem<ProductDefinition>(TEST_COCAINE_MIX_ID);
            if (cocaineDef == null)
            {
                Utility.Error($"CustomCocaSeedsManager: Could not find ProductDefinition with ID '{TEST_COCAINE_MIX_ID}'. " +
        "Log all Registry items at startup to find the correct cocaine mix ID.");
                return;
            }
            MelonCoroutines.Start(CreateCocaSeed(cocaineDef));
        }

        public static IEnumerator CreateCocaSeed(ProductDefinition cocaineDef)
        {
            yield return new WaitForSeconds(5f);

            if (factory == null)
            {
                Utility.Error("CustomCocaSeedsManager: CocaFactory is null! Ensure factory was initialized in Core.OnSceneWasLoaded.");
                yield break;
            }

            if (DiscoveredCocaSeeds.ContainsKey(cocaineDef.ID))
            {
                Utility.Log($"CustomCocaSeedsManager: Coca seed for '{cocaineDef.ID}' already exists, skipping.");
                yield break;
            }

            var newSeed = factory.CreateCocaSeedDefinition(cocaineDef);
            if (newSeed == null)
            {
                Utility.Error("CustomCocaSeedsManager: CreateCocaSeedDefinition returned null.");
                yield break;
            }

            Singleton<Registry>.Instance.AddToRegistry(newSeed);
            Utility.Log($"CustomCocaSeedsManager: Registered seed '{newSeed.ID}' in Registry.");

            // Add the custom leaf ID to all cauldron ingredient slot filters
            // so the Cauldron UI accepts it (mirrors AddSyringeToSpawnStations pattern)
            var customLeaf = Registry.GetItem<QualityItemDefinition>($"{cocaineDef.ID}_customcocaleaf");
            if (customLeaf != null)
                CocaFactory.AddLeafToCauldrons(customLeaf);
            else
                Utility.Error("CustomCocaSeedsManager: Could not resolve custom leaf from Registry after registration.");

            // EDrugType.Cocaine — verify member name via Enum.GetNames(typeof(EDrugType)) at startup
            // Fallback: use EDrugType.Weed (value 0) as placeholder if Cocaine member does not exist
            UnicornSeedData newData = new UnicornSeedData
            {
                seedId = newSeed.ID,
                mixId = cocaineDef.ID,
                drugType = EDrugType.Cocaine,
                price = 100f,
            };
            DiscoveredCocaSeeds.Add(newData.mixId, newData);

            DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomDrop != null && InstanceFinder.IsServer)
            {
                ItemInstance defaultInstance = newSeed.GetDefaultInstance();
                defaultInstance.SetQuantity(3);
                randomDrop.Storage.InsertItem(defaultInstance, true);

                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
                ConversationManager.SendMessage("Salvador", $"{cocaineDef.name} coca seed synthesized and placed in a dead drop.");
                Utility.Log($"CustomCocaSeedsManager: Placed 3x '{newSeed.ID}' in dead drop '{randomDrop.GUID}'.");
            }
            else
            {
                Utility.Error("CustomCocaSeedsManager: No available dead drop for coca seed placement, or not server.");
            }
        }

        public static void ClearAll()
        {
            DiscoveredCocaSeeds.Clear();
            SalvadorShop = null;
            salvador = null;
            if (factory != null) factory.DeleteChildren();
        }
    }
}
