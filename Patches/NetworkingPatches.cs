using HarmonyLib;
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.Product;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Seeds;

namespace UnicornsCustomSeeds.Patches
{
    namespace UnicornsCustomSeeds.Patches
    {
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

                var props = new Il2CppSystem.Collections.Generic.List<string>();
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
                Il2CppSystem.Collections.Generic.List<string> properties,
                WeedAppearanceSettings appearance)
            {
                if (id != "ogkushseed")
                    return;

                if (name == null)
                    return;

                if (!name.StartsWith("[NET-JSON]"))
                    return;

                string serializedString = name.Replace("[NET-JSON]", "");
                try
                {
                    UnicornSeedData seedData = JsonConvert.DeserializeObject<UnicornSeedData>(serializedString);
                    if (Registry.ItemExists(seedData.seedId))
                    {
                        return;
                    }

                    if (!CustomSeedsManager.DiscoveredSeeds.ContainsKey(seedData.weedId))
                    {
                        CustomSeedsManager.DiscoveredSeeds.Add(seedData.weedId, seedData);
                    }

                    SeedDefinition newSeed = CustomSeedsManager.SeedDefinitionLoader(seedData);
                    if (newSeed != null)
                    {
                        try
                        {
                            Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed);
                        }
                        catch (Exception ex)
                        {
                            Utility.PrintException(ex);
                        }
                    }

                    Utility.Log($"Is Client? {InstanceFinder.IsClient}");
                    if (InstanceFinder.IsClient)
                    {
                        DeferredPlantsManager.TrySpawnQueuedPlants(newSeed.ID);
                    }

                }
                catch (Exception ex) { 
                    Utility.PrintException(ex);
                }
            }
        }

        [HarmonyPatch(typeof(Pot),nameof(Pot.RpcLogic___PlantSeed_Client_4077118173))]
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

                if (string.IsNullOrEmpty(seedID))
                    return true;

                if (!seedID.Contains("customseeddefinition"))
                    return true;

                if(Registry.ItemExists(seedID)) return true;

                // Seed is missing on this client → defer this spawn.
                Utility.Log($"[CustomSeeds] Deferring RPC plant spawn for pot {__instance.GUID} and seedId=" + seedID);
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
                    Utility.Log($"[CustomSeeds] Deferring SetHarvestableActive for pot {__instance.GUID}, index {harvestableIndex}, active {active}");
                    DeferredPlantsManager.AddHarvestableUpdate(__instance.GUID.ToString(), harvestableIndex, active);
                    return false;
                }

                return true;
            }
        }
    }
}
