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

        // ?? Test mix IDs — replace placeholders with real IDs as they are discovered ??
        // OnCocaSynthesisRequested iterates this array and synthesizes the first mix
        // that does not yet have a coca seed in the Registry.
        public static readonly string[] TEST_COCAINE_MIX_IDS = new string[]
        {
            "bighaze",        // confirmed working
            "gorillaghost",  // placeholder
            "lasmegma",  // placeholder
            "hairygold",  // placeholder
            "miraclefruit",  // placeholder
            "afghancake",  // placeholder
        };

        public static CocaFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredCocaSeeds = new Dictionary<string, UnicornSeedData>();

        public static ShopInterface SalvadorShop = null;
        public static Salvador salvador = null;

        public static void Initialize()
        {
            // Restore cauldron leaf filters for all previously discovered coca seeds.
            // Must run before Salvador setup so filters are in place regardless of
            // whether the player interacts with Salvador this session.
            RestoreLeafFilters();

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
            // Iterate the mix array and synthesize the first one not yet in the Registry.
            ProductDefinition target = null;
            foreach (string mixId in TEST_COCAINE_MIX_IDS)
            {
                if (DiscoveredCocaSeeds.ContainsKey(mixId))
                    continue; // already synthesized this session

                if (Registry.ItemExists(mixId + "_customcocaseed"))
                    continue; // already in Registry from a previous session

                var def = Registry.GetItem<ProductDefinition>(mixId);
                if (def == null)
                {
                    Utility.Log($"CustomCocaSeedsManager: Mix '{mixId}' not found in Registry — skipping.");
                    continue;
                }

                target = def;
                break;
            }

            if (target == null)
            {
                Utility.Log("CustomCocaSeedsManager: All test mixes already synthesized or not found in Registry.");
                ConversationManager.SendMessage("Salvador", "All available coca seeds have already been synthesized.");
                return;
            }

            MelonCoroutines.Start(CreateCocaSeed(target));
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

        /// <summary>
        /// For every entry in DiscoveredCocaSeeds:
        ///   - If the seed is not yet in the Registry (e.g. CreateCocaine fired before
        /// DiscoveredCocaSeeds was populated), rebuild the full factory chain now.
        ///   - Call AddLeafToCauldrons so every cauldron in the scene accepts the leaf.
        ///
        /// Called from Initialize() which runs on onLoadComplete, guaranteeing that the
        /// factory, cauldrons, and cocaine ProductDefinitions are all available.
        /// </summary>
        public static void RestoreLeafFilters()
        {
            if (DiscoveredCocaSeeds.Count == 0) return;

            if (factory == null)
            {
                Utility.Error("CustomCocaSeedsManager.RestoreLeafFilters: factory is null — cannot restore leaf filters.");
                return;
            }

            foreach (var kvp in DiscoveredCocaSeeds)
            {
                string mixId = kvp.Key;

                // Rebuild the full seed chain if it was not recreated by the CreateCocaine patch
                // (which silently skips when DiscoveredCocaSeeds is empty at load time).
                if (!Registry.ItemExists(mixId + "_customcocaseed"))
                {
                    var cocaineDef = Registry.GetItem<ProductDefinition>(mixId);
                    if (cocaineDef == null)
                    {
                        Utility.Error($"CustomCocaSeedsManager.RestoreLeafFilters: ProductDefinition '{mixId}' not in Registry — skipping.");
                        continue;
                    }

                    var newSeed = factory.CreateCocaSeedDefinition(cocaineDef);
                    if (newSeed == null)
                    {
                        Utility.Error($"CustomCocaSeedsManager.RestoreLeafFilters: CreateCocaSeedDefinition returned null for '{mixId}'.");
                        continue;
                    }

                    Singleton<Registry>.Instance.AddToRegistry(newSeed);
                    Utility.Log($"CustomCocaSeedsManager.RestoreLeafFilters: Rebuilt seed '{newSeed.ID}'.");
                }

                // Always re-add to cauldron filters — they are runtime objects reset each load.
                string leafId = $"{mixId}_customcocaleaf";
                var leaf = Registry.GetItem<QualityItemDefinition>(leafId);
                if (leaf != null)
                {
                    CocaFactory.AddLeafToCauldrons(leaf);
                    Utility.Log($"CustomCocaSeedsManager.RestoreLeafFilters: Added '{leafId}' to cauldron filters.");
                }
                else
                {
                    Utility.Error($"CustomCocaSeedsManager.RestoreLeafFilters: Could not resolve '{leafId}' from Registry.");
                }
            }
        }
    }
}
