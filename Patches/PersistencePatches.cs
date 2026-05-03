using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;
using System.Collections.Generic;
using System;
using System.IO;



#if IL2CPP
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.MainMenu;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using FishNet.Connection;
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Management;
using ScheduleOne.Persistence;
using ScheduleOne.Product;
using ScheduleOne.UI.MainMenu;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.ExitToMenu))]
    public static class LoadManager_ExitToMenu_Patch
    {
        public static void Postfix(LoadManager __instance, SaveInfo autoLoadSave, MainMenuPopup.Data mainMenuPopup, bool preventLeaveLobby)
        {
            var seedList = Singleton<ManagementUtilities>.Instance.Seeds;
            var seedListClean = new GenericCol.List<SeedDefinition>();
            foreach (var seed in seedList)
            {
                if (seed != null && !seed.ID.Contains("customseeddefinition"))
                    seedListClean.Add(seed);
            }
            Singleton<ManagementUtilities>.Instance.Seeds = seedListClean;
        }
    }

    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.StartGame))]
    public static class LoadManager_StartGame_Patch
    {
        public static void Postfix(LoadManager __instance)
        {
            if (__instance == null || string.IsNullOrEmpty(__instance.LoadedGameFolderPath)) return;

            try
            {
                string saveFolder = __instance.LoadedGameFolderPath;

                // ── DiscoveredCustomSeeds.json ────────────────────────────────────
                LoadDiscoveredSeeds(saveFolder);

                // ── UnicornsActiveCooking.json ────────────────────────────────────
                LoadActiveCooking(saveFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"PersistencePatches.StartGame: {ex}");
            }
        }

        private static void LoadDiscoveredSeeds(string saveFolder)
        {
            string filePath = Path.Combine(saveFolder, "DiscoveredCustomSeeds.json");
            List<UnicornSeedData> seeds = new List<UnicornSeedData>();

            if (File.Exists(filePath))
            {
                CustomSeedsManager.FirstLoad = false;
                string json = File.ReadAllText(filePath);

                try
                {
                    JArray jArray = JArray.Parse(json);

                    // Legacy migration: old format used "weedId" instead of "mixId"
                    if (jArray.Count > 0 && jArray[0] is JObject first && first.ContainsKey("weedId"))
                    {
                        CustomSeedsManager.letsMigrate = true;
                        var legacy = JsonConvert.DeserializeObject<List<LegacySeedData>>(json) ?? new List<LegacySeedData>();
                        Utility.Success($"Migrating {legacy.Count} legacy seed records.");

                        foreach (var l in legacy)
                            seeds.Add(new UnicornSeedData { seedId = l.seedId, mixId = l.weedId, drugType = EDrugType.Marijuana, price = l.price });

                        File.WriteAllText(filePath, JsonConvert.SerializeObject(seeds, Formatting.Indented));
                        Utility.Success("Save file migrated to new format.");
                    }
                    else
                    {
                        CustomSeedsManager.letsMigrate = false;
                        seeds = JsonConvert.DeserializeObject<List<UnicornSeedData>>(json) ?? new List<UnicornSeedData>();
                    }

                    Utility.Success($"Loaded {seeds.Count} custom seeds.");
                }
                catch (Exception ex)
                {
                    Utility.PrintException(ex);
                    CustomSeedsManager.FirstLoad = true;
                    seeds = new List<UnicornSeedData>();
                }
            }
            else
            {
                CustomSeedsManager.FirstLoad = true;
            }

            // Dispatch each seed to the correct manager by drug type
            foreach (UnicornSeedData data in seeds)
            {
                if (data == null) continue;

                switch (data.drugType)
                {
                    case EDrugType.Marijuana:
                        if (!CustomSeedsManager.DiscoveredSeeds.ContainsKey(data.mixId))
                            CustomSeedsManager.DiscoveredSeeds.Add(data.mixId, data);
                        break;

                    case EDrugType.Shrooms:
                        if (!CustomShroomsManager.DiscoveredShrooms.ContainsKey(data.mixId))
                            CustomShroomsManager.DiscoveredShrooms.Add(data.mixId, data);
                        break;

                    case EDrugType.Cocaine:
                        if (!CustomCocaSeedsManager.DiscoveredCocaSeeds.ContainsKey(data.mixId))
                            CustomCocaSeedsManager.DiscoveredCocaSeeds.Add(data.mixId, data);
                        break;

                    default:
                        Utility.Error($"PersistencePatches: Unknown EDrugType '{data.drugType}' for seed '{data.seedId}' — skipped.");
                        break;
                }
            }
        }

        private static void LoadActiveCooking(string saveFolder)
        {
            string filePath = Path.Combine(saveFolder, "UnicornsActiveCooking.json");
            if (!File.Exists(filePath)) return;

            try
            {
                string json = File.ReadAllText(filePath);
                var entries = JsonConvert.DeserializeObject<List<ActiveCookingEntry>>(json);
                if (entries == null) return;

                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry?.stationGuid) && !string.IsNullOrEmpty(entry.mixId))
                        ActiveCookingRegistry.Register(entry.stationGuid, entry.mixId);
                }

                Utility.Success($"Loaded {entries.Count} active cooking entries.");
            }
            catch (Exception ex)
            {
                Utility.PrintException(ex);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch: ProductManager.CreateWeed — re-create weed seeds after load
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.CreateWeed))]
    public static class Patch_ProductManager_CreateWeed
    {
        public static void Postfix(
            NetworkConnection conn, string name, string id,
     EDrugType type, List<string> properties, WeedAppearanceSettings appearance)
        {
            if (Registry.ItemExists(id + "_customseeddefinition")) return;

            if (CustomSeedsManager.DiscoveredSeeds.TryGetValue(id, out var parts))
            {
                SeedDefinition newSeed = CustomSeedsManager.SeedDefinitionLoader(parts);
                if (newSeed != null)
                {
                    try { Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed); }
                    catch (Exception ex) { Utility.PrintException(ex); }
                }
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Patch: ProductManager.CreateCocaine — re-create coca seeds after load
    //
    // Fires whenever a cocaine mix is registered (on load for each existing mix).
    // If DiscoveredCocaSeeds has an entry for this mix ID and the seed is not
    // yet in the Registry, run CocaFactory to rebuild the full chain and
    // register it, then add the leaf to cauldron filters.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.CreateCocaine))]
    public static class Patch_ProductManager_CreateCocaine
    {
        public static void Postfix(
         NetworkConnection conn, string name, string id,
  EDrugType type, List<string> properties, CocaineAppearanceSettings appearance)
        {
            if (Registry.ItemExists(id + "_customcocaseed")) return;
            if (!CustomCocaSeedsManager.DiscoveredCocaSeeds.TryGetValue(id, out var _)) return;

            if (CustomCocaSeedsManager.factory == null)
            {
                Utility.Error($"Patch_ProductManager_CreateCocaine: factory is null for '{id}'.");
                return;
            }

            try
            {
                var cocaineDef = Registry.GetItem<ProductDefinition>(id);
                if (cocaineDef == null)
                {
                    Utility.Error($"Patch_ProductManager_CreateCocaine: Could not resolve ProductDefinition '{id}'.");
                    return;
                }

                SeedDefinition newSeed = CustomCocaSeedsManager.factory.CreateCocaSeedDefinition(cocaineDef);
                if (newSeed == null) return;

                Singleton<Registry>.Instance.AddToRegistry(newSeed);

                var customLeaf = Registry.GetItem<QualityItemDefinition>($"{id}_customcocaleaf");
                if (customLeaf != null)
                    CocaFactory.AddLeafToCauldrons(customLeaf);

                Utility.Log($"Patch_ProductManager_CreateCocaine: Reloaded coca seed '{newSeed.ID}'.");
            }
            catch (Exception ex) { Utility.PrintException(ex); }
        }
    }
}
