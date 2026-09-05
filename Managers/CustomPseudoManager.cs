using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;
using Il2CppScheduleOne.Economy;


#if IL2CPP
using Il2Cpp;
using Il2CppFishNet;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Messaging;
using Il2CppScheduleOne.Misc;
using Il2CppScheduleOne.NPCs.CharacterClasses;
using Il2CppScheduleOne.PlayerScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
#elif MONO
using FishNet;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ItemFramework;
using ScheduleOne.Messaging;
using ScheduleOne.Misc;
using ScheduleOne.NPCs.CharacterClasses;
using ScheduleOne.PlayerScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class CustomPseudoManager
    {
        public const string PSEUDO_BASE_ID     = "pseudo";
        public const string BASE_LIQUIDMETH_ID = "liquidmeth";

        // ── Test mix IDs — replace placeholders with real IDs as discovered ──
        // OnPseudoSynthesisRequested iterates this array and synthesizes the
        // first mix not yet represented in the Registry.
        public static readonly string[] TEST_METH_MIX_IDS = new string[]
        {
            "laghost",          // confirmed
            "aspenmonkey",      // confirmed
            "kryptonitesucks",  // confirmed
            "nightmaregrool",   // confirmed
            "meth_mix_05",      // placeholder
            "meth_mix_06",      // placeholder
        };

        public static PseudoFactory factory;
        public static Dictionary<string, UnicornSeedData> DiscoveredPseudoSeeds
            = new Dictionary<string, UnicornSeedData>();

        public static Shirley shirley = null;

        // ─────────────────────────────────────────────────────────────────────
        // Initialize — called from InitMod on onLoadComplete.
        //
        // RestorePseudoFilters runs first so station filters and canvas recipes
        // are in place before the player can interact with any chemistry station.
        // ─────────────────────────────────────────────────────────────────────
        public static void Initialize()
        {
            RestorePseudoFilters();

            shirley = GameObject.FindObjectOfType<Shirley>();
            if (shirley != null)
            {
                if (shirley.MSGConversation != null)
                    ConversationManager.RegisterConversation("Shirley", shirley.MSGConversation);

                SetupShirleyConversation();
            }
            else
            {
                Utility.Error("CustomPseudoManager: Could not find Shirley NPC in scene. Verify she is present and unlocked.");
            }
        }

        private static void SetupShirleyConversation()
        {
            MSGConversation convo = ConversationManager.GetConversation("Shirley");
            if (convo != null)
            {
                SendableMessage sendable = convo.CreateSendableMessage("Synthesize Pseudo");
                sendable.onSent += (Action)OnPseudoSynthesisRequested;
                Utility.Log("CustomPseudoManager: 'Synthesize Pseudo' message registered on Shirley.");
            }
            else
            {
                Utility.Error("CustomPseudoManager: Could not get Shirley conversation from ConversationManager.");
            }
        }

        public static void OnPseudoSynthesisRequested()
        {
            MethDefinition target = null;
            foreach (string mixId in TEST_METH_MIX_IDS)
            {
                if (DiscoveredPseudoSeeds.ContainsKey(mixId))
                    continue; // already synthesized this session

                if (Registry.ItemExists(mixId + "_custompseudo"))
                    continue; // already in Registry from a previous session

#if IL2CPP
                MethDefinition methDef = Registry.GetItem<ProductDefinition>(mixId)?.TryCast<MethDefinition>();
#elif MONO
                MethDefinition methDef = Registry.GetItem<MethDefinition>(mixId);
#endif
                if (methDef == null)
                {
                    Utility.Log($"CustomPseudoManager: Mix '{mixId}' not found in Registry as MethDefinition — skipping.");
                    continue;
                }

                target = methDef;
                break;
            }

            if (target == null)
            {
                Utility.Log("CustomPseudoManager: All test mixes already synthesized or not found in Registry.");
                ConversationManager.SendMessage("Shirley", "All available pseudo chains have already been synthesized.");
                return;
            }

            MelonCoroutines.Start(CreatePseudoChain(target));
        }

        public static IEnumerator CreatePseudoChain(MethDefinition methDef)
        {
            yield return new WaitForSeconds(5f);

            if (factory == null)
            {
                Utility.Error("CustomPseudoManager: PseudoFactory is null! Ensure factory was initialized in Core.OnSceneWasLoaded.");
                yield break;
            }

            if (DiscoveredPseudoSeeds.ContainsKey(methDef.ID))
            {
                Utility.Log($"CustomPseudoManager: Pseudo chain for '{methDef.ID}' already exists — skipping.");
                yield break;
            }

            QualityItemDefinition customPseudo = factory.CreatePseudoChain(methDef);
            if (customPseudo == null)
            {
                Utility.Error("CustomPseudoManager: CreatePseudoChain returned null.");
                yield break;
            }

            PseudoFactory.AddPseudoToChemistryStations(customPseudo);

            UnicornSeedData newData = new UnicornSeedData
            {
                seedId   = customPseudo.ID,
                mixId    = methDef.ID,
                drugType = EDrugType.Methamphetamine,
                price    = 100f,
            };
            DiscoveredPseudoSeeds.Add(newData.mixId, newData);

            DeadDrop randomDrop = DeadDrop.GetRandomEmptyDrop(Player.Local.transform.position);
            if (randomDrop != null && InstanceFinder.IsServer)
            {
                ItemInstance defaultInstance = customPseudo.GetDefaultInstance();
                defaultInstance.SetQuantity(5);
                randomDrop.Storage.InsertItem(defaultInstance, true);

                string guidString = GUIDManager.GenerateUniqueGUID().ToString();
                NetworkSingleton<QuestManager>.Instance.CreateDeaddropCollectionQuest(null, randomDrop.GUID.ToString(), guidString);
                ConversationManager.SendMessage("Shirley", $"{methDef.name} pseudo synthesized and placed in a dead drop.");
                Utility.Log($"CustomPseudoManager: Placed 5x '{customPseudo.ID}' in dead drop '{randomDrop.GUID}'.");
            }
            else
            {
                Utility.Error("CustomPseudoManager: No available dead drop for pseudo placement, or not server.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // RestorePseudoFilters — called from Initialize() on onLoadComplete.
        //
        // For every entry in DiscoveredPseudoSeeds:
        //   - If the pseudo is NOT in the Registry → full rebuild via factory
        //     (covers the case where Patch_ProductManager_CreateMeth ran before
        //     DiscoveredPseudoSeeds was populated, or factory was not yet ready).
        //   - If the pseudo IS in the Registry → re-inject the StationRecipe only
        //     (the canvas may be a fresh instance after scene reload).
        //   - Always re-add to chemistry station slot filters (runtime objects
        //     that reset every scene load).
        // ─────────────────────────────────────────────────────────────────────
        public static void RestorePseudoFilters()
        {
            if (DiscoveredPseudoSeeds.Count == 0) return;

            if (factory == null)
            {
                Utility.Error("CustomPseudoManager.RestorePseudoFilters: factory is null — cannot restore.");
                return;
            }

            foreach (var kvp in DiscoveredPseudoSeeds)
            {
                string mixId    = kvp.Key;
                string pseudoId = $"{mixId}_custompseudo";

                if (!Registry.ItemExists(pseudoId))
                {
#if IL2CPP
                    MethDefinition methDef = Registry.GetItem<ProductDefinition>(mixId)?.TryCast<MethDefinition>();
#elif MONO
                    MethDefinition methDef = Registry.GetItem<MethDefinition>(mixId);
#endif
                    if (methDef == null)
                    {
                        Utility.Error($"CustomPseudoManager.RestorePseudoFilters: MethDefinition '{mixId}' not in Registry — skipping.");
                        continue;
                    }

                    QualityItemDefinition rebuilt = factory.CreatePseudoChain(methDef);
                    if (rebuilt == null)
                    {
                        Utility.Error($"CustomPseudoManager.RestorePseudoFilters: CreatePseudoChain returned null for '{mixId}'.");
                        continue;
                    }

                    Utility.Log($"CustomPseudoManager.RestorePseudoFilters: Rebuilt chain for '{mixId}'.");
                }
                else
                {
                    // Assets already in Registry — re-inject the recipe into the canvas
                    // (canvas.Recipes resets each scene, so this must run every load).
                    factory.InjectRecipeForMix(mixId);
                }

                // Always re-add to station filters — runtime objects reset each load
                var pseudo = Registry.GetItem<QualityItemDefinition>(pseudoId);
                if (pseudo != null)
                {
                    PseudoFactory.AddPseudoToChemistryStations(pseudo);
                    Utility.Log($"CustomPseudoManager.RestorePseudoFilters: Restored filters for '{pseudoId}'.");
                }
                else
                {
                    Utility.Error($"CustomPseudoManager.RestorePseudoFilters: Could not resolve '{pseudoId}' from Registry after rebuild.");
                }
            }
        }

        public static void ClearAll()
        {
            DiscoveredPseudoSeeds.Clear();
            shirley = null;
            if (factory != null) factory.DeleteChildren();
        }
    }
}
