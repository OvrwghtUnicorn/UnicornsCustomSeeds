using HarmonyLib;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.SeedQuests;
using UnicornsCustomSeeds.TemplateUtils;


#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.Quests;
using GenericCol = Il2CppSystem.Collections.Generic;
# elif MONO
using FishNet;
using FishNet.Connection;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.Management;
using ScheduleOne.ObjectScripts;
using ScheduleOne.Product;
using ScheduleOne.Quests;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Patches
{

    [HarmonyPatch(typeof(QuestManager), nameof(QuestManager.OnSpawnServer))]
    public static class QuestManager_OnSpawnServer_Patch
    {
        public static void Postfix(NetworkConnection connection)
        {
            if (!InstanceFinder.IsServer) return;

            // S1API's GetQuestByName returns the FIRST quest whose title matches.
            // The superseded CustomSeedQuest shared the weed title, so on a host
            // loading it from save data it shadowed the real CustomSynthesisQuest and
            // this cast produced null — the late-join broadcast never fired. That
            // class has been deleted, so the title is now unambiguous.
            //
            // Covers all four drug types — previously only weed's active quest was
            // rebroadcast to a joining client here.
            if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") is CustomSynthesisQuest)
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Marijuana);

            if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Shroom Mix") is CustomSynthesisQuest)
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Shrooms);

            if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Cocaine Mix") is CustomSynthesisQuest)
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Cocaine);

            if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Meth Mix") is CustomSynthesisQuest)
                NetworkSyncManager.BroadcastQuestConfig(EDrugType.Methamphetamine);
        }
    }

    // ============================
    // SERVER → CLIENT SEND TEST
    // ============================
    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.OnSpawnServer))]
    public static class ProductManager_OnSpawnServer_NetTest
    {
        public static void Postfix(ProductManager __instance, NetworkConnection connection)
        {
            if (connection == null)
                return;

            if (!InstanceFinder.IsServer)
                return;

            if (connection.IsHost)
                return;

            var props = new GenericCol.List<string>();
            var appearance = new WeedAppearanceSettings(
                __instance.DefaultWeed.MainMat.color,
                __instance.DefaultWeed.SecondaryMat.color,
                __instance.DefaultWeed.LeafMat.color,
                __instance.DefaultWeed.StemMat.color);

            foreach (var seed in CustomSeedsManager.DiscoveredSeeds)
            {
                //Utility.Log($"requesting to create {seed.Value.seedId}");
                __instance.CreateWeed_Server(
                    "[NET-JSON]" + JsonConvert.SerializeObject(seed.Value, Formatting.None),
                    "ogkushseed",
                    EDrugType.Marijuana,
                    props,
                    appearance);
            }

            // Coca / shroom / pseudo ride their own Create*_Server channels.
            NetworkSyncManager.BroadcastAllDiscovered();

            // Every custom definition for this connection has now been written. Containers
            // that relied on the vanilla onProductDataSentToConnection hook flushed one
            // step ago — before the payloads above — so release the ones we parked.
            JoinSyncManager.OnCustomDataSent(connection);
        }
    }

    // ============================
    // COCA / SHROOM / PSEUDO RECEIVE
    //
    // Prefix (not Postfix, as weed uses): these three RpcLogic methods log
    // Console.LogError("Product with ID X already exists") on their sentinel
    // early-return, so letting the original run would spam the game console on
    // every synced item. Returning false skips it entirely.
    // ============================

    [HarmonyPatch(typeof(ProductManager), "RpcLogic___CreateCocaine_1327282946")]
    public static class Patch_ProductManager_RpcLogic_CreateCocaine_Sync
    {
        public static bool Prefix(string name, string id)
        {
            if (!NetworkSyncManager.TryParsePayload(name, id, NetworkSyncManager.SENTINEL_COCA, out var data))
                return true;

            NetworkSyncManager.RebuildFromNetwork(data);
            return false;
        }
    }

    [HarmonyPatch(typeof(ProductManager), "RpcLogic___CreateShroom_Client_812995776")]
    public static class Patch_ProductManager_RpcLogic_CreateShroom_Sync
    {
        public static bool Prefix(string name, string id)
        {
            if (!NetworkSyncManager.TryParsePayload(name, id, NetworkSyncManager.SENTINEL_SHROOM, out var data))
                return true;

            NetworkSyncManager.RebuildFromNetwork(data);
            return false;
        }
    }

    [HarmonyPatch(typeof(ProductManager), "RpcLogic___CreateMeth_1869045686")]
    public static class Patch_ProductManager_RpcLogic_CreateMeth_Sync
    {
        public static bool Prefix(string name, string id)
        {
            if (!NetworkSyncManager.TryParsePayload(name, id, NetworkSyncManager.SENTINEL_METH, out var data))
                return true;

            NetworkSyncManager.RebuildFromNetwork(data);
            return false;
        }
    }

    // ============================
    // CLIENT/SERVER RECEIVE TEST
    // ============================
    [HarmonyPatch(typeof(ProductManager), "RpcLogic___CreateWeed_1777266891")]
    public static class ProductManager_RpcLogic_CreateWeed_NetTest
    {
        public static void Postfix(
            ProductManager __instance,
            NetworkConnection conn,
            string name,
            string id,
            EDrugType type,
            GenericCol.List<string> properties,
            WeedAppearanceSettings appearance)
        {
            if (id != "ogkushseed")
            {
                // A real weed mix just finished replicating. Any custom seed payload that
                // landed before its mix existed can now be built — see
                // DeferredSeedRebuildManager. Postfix, so the mix is already registered.
                DeferredSeedRebuildManager.TryRebuild(id);
                return;
            }

            if (name == null)
                return;

            // Client -> server: a client started a cauldron cook with a custom leaf.
            // Server-only: the initiating client already swapped its own copy locally,
            // and other clients don't create the output so they don't need it.
            if (name.StartsWith(NetworkSyncManager.COOK_PREFIX))
            {
                if (InstanceFinder.IsServer)
                    NetworkSyncManager.HandleCauldronCook(name);
                return;
            }

            if (name.StartsWith("[NET-QUEST]") && InstanceFinder.IsClientOnly)
            {
                string parsed = name.Replace("[NET-QUEST]", "");
                EDrugType questDrugType = EDrugType.Marijuana;

                // Parse the comma-separated values: drugType,cost,qty,time.
                // Previously 3 values (cost,qty,time) with drugType implicitly
                // Marijuana — this channel is now shared by all four quest managers
                // (see NetworkSyncManager.BroadcastQuestConfig), so drugType rides
                // along as the first field.
                try
                {
                    string[] values = parsed.Split(',');
                    if (values.Length == 4)
                    {
                        if (int.TryParse(values[0], out int drugTypeInt))
                        {
                            questDrugType = (EDrugType)drugTypeInt;
                        }
                        if (int.TryParse(values[1], out int stashCost))
                        {
                            StashManager.StashCostEntry.Value = stashCost;
                        }
                        if (int.TryParse(values[2], out int stashQty))
                        {
                            StashManager.StashQtyEntry.Value = stashQty;
                        }
                        if (int.TryParse(values[3], out int synthesizeTime))
                        {
                            StashManager.SynthesizeTime.Value = synthesizeTime;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utility.PrintException(ex);
                }

                if (StashManager.albertsStash == null)
                {
                    StashManager.GetAlbertsStash();
                }

                // Dispatch to whichever manager's quest this actually is. Previously
                // hardcoded to weed's "Drop off the Mix" / SeedQuestManager regardless
                // of payload — combined with Coca/Shroom/Pseudo never broadcasting at
                // all (fixed in their OnSent()), those three quests never reached a
                // client before.
                switch (questDrugType)
                {
                    case EDrugType.Marijuana:
                        if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Mix") is not CustomSynthesisQuest)
                            SeedQuestManager.CreateQuestAsync();
                        break;

                    case EDrugType.Shrooms:
                        if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Shroom Mix") is not CustomSynthesisQuest)
                            ShroomQuestManager.CreateQuestAsync();
                        break;

                    case EDrugType.Cocaine:
                        if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Cocaine Mix") is not CustomSynthesisQuest)
                            CocaQuestManager.CreateQuestAsync();
                        break;

                    case EDrugType.Methamphetamine:
                        if (S1API.Quests.QuestManager.GetQuestByName("Drop off the Meth Mix") is not CustomSynthesisQuest)
                            PseudoQuestManager.CreateQuestAsync();
                        break;

                    default:
                        Utility.Error($"[NET-QUEST] unsupported drugType '{questDrugType}'.");
                        break;
                }
                return;
            }

            if (!name.StartsWith("[NET-JSON]"))
                return;

            string serializedString = name.Replace("[NET-JSON]", "");
            try
            {
                UnicornSeedData seedData = JsonConvert.DeserializeObject<UnicornSeedData>(serializedString);

                // One line per payload: how many of the host's seeds actually arrived, and
                // which of them lost the race against their own base mix.
                bool mixReady = Registry.GetItem<WeedDefinition>(seedData.mixId) != null;
                bool built = CustomSeedsManager.RebuildFromPayload(seedData);

                Utility.Log($"[NET-JSON] seed='{seedData.seedId}' mix='{seedData.mixId}' " +
                            $"mixReady={mixReady} built={built}");

                if (!built)
                    DeferredSeedRebuildManager.Park(seedData);
            }
            catch (Exception ex)
            {
                Utility.PrintException(ex);
            }
        }
    }

    [HarmonyPatch(typeof(Pot), nameof(Pot.RpcLogic___PlantSeed_Client_4077118173))]
    public static class Patch_Pot_RpcLogic___PlantSeed_Client_4077118173
    {
        public static bool Prefix(
        Pot __instance,
        NetworkConnection conn,
        string seedID,
        float normalizedSeedProgress)
        {
            // Only intercept on a pure client (joiner), not server or host.
            if (!InstanceFinder.IsClient || InstanceFinder.IsServer)
                return true;

            // During replay we want vanilla logic to run unmodified.
            if (DeferredPlantsManager.IsReplaying)
                return true;

            if (!DeferredPlantsManager.IsPotPlantedCustomSeed(seedID))
                return true;

            if (Registry.ItemExists(seedID)) return true;

            DeferredPlantsManager.AddDeferredSeed(__instance, seedID, normalizedSeedProgress);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pot), nameof(Pot.RpcLogic___SetHarvestableActive_Client_338960014))]
    public static class Patch_Pot_RpcLogic___SetHarvestableActive_Client_338960014
    {
        public static bool Prefix(
        Pot __instance,
        NetworkConnection conn,
        int harvestableIndex,
        bool active)
        {
            // Only intercept on a pure client (joiner), not server or host.
            if (!InstanceFinder.IsClient || InstanceFinder.IsServer)
                return true;

            if (DeferredPlantsManager.PendingPotGuids.Contains(__instance.GUID.ToString()))
            {
                DeferredPlantsManager.AddHarvestableUpdate(__instance.GUID.ToString(), harvestableIndex, active);
                return false;
            }

            return true;
        }
    }
}
