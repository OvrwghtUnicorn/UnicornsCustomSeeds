using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.Persistence.Datas;
using Il2CppScheduleOne.Persistence.ItemLoaders;
using Il2CppScheduleOne.Product;
using Il2CppScheduleOne.UI.MainMenu;
using MelonLoader;
using Newtonsoft.Json;
using System.Reflection;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using Il2CppGeneric = Il2CppSystem.Collections.Generic;

namespace UnicornsCustomSeeds.Patches
{

    [HarmonyPatch(typeof(LoadManager), nameof(LoadManager.ExitToMenu))]
    public static class LoadManager_ExitToMenu_Patch
    {
        public static void Postfix(LoadManager __instance, SaveInfo autoLoadSave, MainMenuPopup.Data mainMenuPopup, bool preventLeaveLobby)
        {
            var seedList = Singleton<Registry>.Instance.Seeds;
            var seedListClean = new Il2CppGeneric.List<SeedDefinition>();
            foreach (var seed in seedList)
            {
                if (seed != null && !seed.ID.Contains("customseeddefinition"))
                {
                    seedListClean.Add(seed);
                }
            }

            Singleton<Registry>.Instance.Seeds = seedListClean;
        }
    }

    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save), new Type[] { typeof(string) })]
    public static class SaveManager_Save_Patch
    {
        public static bool Prefix(SaveManager __instance, string saveFolderPath)
        {
            if (string.IsNullOrEmpty(saveFolderPath))
                return true;

            string filePath = Path.Combine(saveFolderPath, "DiscoveredCustomSeeds.json");
            List<UnicornSeedData> seedsIl2cpp = CustomSeedsManager.DiscoveredSeeds.Values.ToList();
            if (Directory.Exists(saveFolderPath))
            {
                string json = JsonConvert.SerializeObject(seedsIl2cpp, Formatting.Indented);
                File.WriteAllText(filePath, json);
                MelonLogger.Msg("Created default DiscoveredCustomSeeds.json with initial custom seeds.");
            }

            return true;
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
                string saveGameFolder = __instance.LoadedGameFolderPath;
                if (string.IsNullOrEmpty(saveGameFolder))
                    return;

                string filePath = Path.Combine(saveGameFolder, "DiscoveredCustomSeeds.json");
                
                List<UnicornSeedData> seedsIl2cpp = new List<UnicornSeedData>();

                if (File.Exists(filePath))
                {
                    // Load and deserialize directly into an IL2CPP array/list
                    string json = File.ReadAllText(filePath);

                    // Try array first
                    seedsIl2cpp = JsonConvert.DeserializeObject<List<UnicornSeedData>>(json) ?? new List<UnicornSeedData>();

                    MelonLogger.Msg($"Loaded {seedsIl2cpp.Count} custom seeds from DiscoveredCustomSeeds.json");
                }
                
                foreach (UnicornSeedData seedData in seedsIl2cpp)
                {
                    if (seedData != null && !CustomSeedsManager.DiscoveredSeeds.ContainsKey(seedData.weedId))
                    {
                        CustomSeedsManager.DiscoveredSeeds.Add(seedData.weedId, seedData);
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
            if (CustomSeedsManager.DiscoveredSeeds.TryGetValue(id, out var parts))
            {
                SeedDefinition newSeed = CustomSeedsManager.SeedDefinitionLoader(parts);
                if (newSeed != null)
                {
                    MelonLogger.Msg($"[CreateWeed_Patch] Successfully loaded seed: {newSeed.ID}");
                }
            }
        }
    }

}
