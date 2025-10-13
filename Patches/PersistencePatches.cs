using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppNewtonsoft.Json;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.Persistence.ItemLoaders;
using Il2CppScheduleOne.Product;
using MelonLoader;
using System.Reflection;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using Il2CppGeneric = Il2CppSystem.Collections.Generic;

namespace UnicornsCustomSeeds.Patches
{

    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.StartGame))]
    public static class LoadManager_StartGame_Patch
    {
        public static void Postfix(LoadManager __instance)
        {
            Utility.Log($"{__instance}");
            if (__instance == null) return;
            Utility.Log($"{__instance.LoadedGameFolderPath}");
            try
            {
                SeedDefinition ogkush = Registry.GetItem<SeedDefinition>("ogkushseed");
                if (ogkush != null)
                {
                    Utility.Log("OG Kush Exists at Runtime");
                }

                string saveGameFolder = __instance.LoadedGameFolderPath;
                if (string.IsNullOrEmpty(saveGameFolder))
                    return;

                string filePath = Path.Combine(saveGameFolder, "DiscoveredCustomSeeds.json");
                Il2CppGeneric.List<string> seedsIl2cpp = null;
                Utility.Log(filePath);
                if (File.Exists(filePath))
                {
                    // Load and deserialize directly into an IL2CPP array/list
                    string json = File.ReadAllText(filePath);

                    // Try array first
                    seedsIl2cpp = JsonConvert.DeserializeObject<Il2CppGeneric.List<string>>(json);

                    // Fallback: some setups prefer List<string> over arrays
                    if (seedsIl2cpp == null)
                    {
                        seedsIl2cpp = new Il2CppGeneric.List<string>();
                    }

                    MelonLogger.Msg($"Loaded {seedsIl2cpp.Count} custom seeds from DiscoveredCustomSeeds.json");
                }
                else
                {
                    // Create default IL2CPP array
                    seedsIl2cpp = new Il2CppGeneric.List<string>();
                    seedsIl2cpp.Add("deathfuel_ogkushseed_customseeddefinition");
                    seedsIl2cpp.Add("superghost_ogkushseed_customseeddefinition");
                    seedsIl2cpp.Add("granddaddygrool_ogkushseed_customseeddefinition");
                    seedsIl2cpp.Add("aspendeath_ogkushseed_customseeddefinition");
                    seedsIl2cpp.Add("imapickle_ogkushseed_customseeddefinition");
                    seedsIl2cpp.Add("miraclecrystal_ogkushseed_customseeddefinition");

                    // Serialize using Il2CppNewtonsoft into JSON and write file
                    string json = JsonConvert.SerializeObject(seedsIl2cpp, Formatting.Indented);
                    Directory.CreateDirectory(saveGameFolder);
                    File.WriteAllText(filePath, json);
                    MelonLogger.Msg("Created default DiscoveredCustomSeeds.json with initial custom seeds.");
                }

                foreach (string seedId in seedsIl2cpp)
                {
                    var parts = CustomSeedsManager.SeedIdSplitter(seedId);
                    if (parts.weedDefId != null && parts.baseSeedId != null)
                    {
                        CustomSeedsManager.loadedSeeds.Add(parts.weedDefId, new SeedComponents { baseSeedId = parts.baseSeedId, seedId = seedId });
                    }
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error handling DiscoveredCustomSeeds.json: {ex}");
            }

        }
    }

    [HarmonyPatch(typeof(ProductManager), nameof(ProductManager.CreateWeed))]
    public static class Patch_ProductManager_CreateWeed
    {
        public static void Postfix(
                NetworkConnection conn,
                string name,
                string id,
                EDrugType type,
                List<string> properties,
                WeedAppearanceSettings appearance)
        {
            if (CustomSeedsManager.loadedSeeds.TryGetValue(id, out var parts))
            {
                Utility.Log($"[CreateWeed_Patch] Called with id={id}, name={name}, type={type}");
                CustomSeedsManager.SeedDefinitionLoader(id, parts.baseSeedId);
            }
        }
    }

}
