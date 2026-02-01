using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Seeds;
using Il2CppFishNet;


#if IL2CPP
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
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
                {
                    seedListClean.Add(seed);
                }
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

            // Create the seed factory
            //CustomSeedsManager.SeedFactoryLoader();

            try
            {
                string saveGameFolder = __instance.LoadedGameFolderPath;
                if (string.IsNullOrEmpty(saveGameFolder))
                    return;

                string filePath = Path.Combine(saveGameFolder, "DiscoveredCustomSeeds.json");
                
                List<UnicornSeedData> seedsIl2cpp = new List<UnicornSeedData>();

                if (File.Exists(filePath))
                {
                    CustomSeedsManager.FirstLoad = false;
                    // Load and deserialize directly into an IL2CPP array/list
                    string json = File.ReadAllText(filePath);

                    try
                    {
                        seedsIl2cpp = JsonConvert.DeserializeObject<List<UnicornSeedData>>(json) ?? new List<UnicornSeedData>();
                        Utility.Success($"Successfully loaded {seedsIl2cpp.Count} custom seeds");
                    }
                    catch (Exception ex)
                    {
                        Utility.PrintException(ex);
                        CustomSeedsManager.FirstLoad = true;
                        seedsIl2cpp = new List<UnicornSeedData>();
                    }
                }
                else
                {
                    CustomSeedsManager.FirstLoad = true;
                }

                foreach (UnicornSeedData seedData in seedsIl2cpp)
                {
                    var weedInstance = Singleton<Registry>.Instance._GetItem(seedData.weedId);
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
            if(Registry.ItemExists(id + "_customseeddefinition"))
            {
                return;
            }

            if (CustomSeedsManager.DiscoveredSeeds.TryGetValue(id, out var parts))
            {
                SeedDefinition newSeed = CustomSeedsManager.SeedDefinitionLoader(parts);
                if (newSeed != null)
                {
                    try
                    {
                        Singleton<ManagementUtilities>.Instance.Seeds.Add(newSeed);
                    }
                    catch (Exception ex) {
                        Utility.PrintException(ex);
                    }
                }

                //if (InstanceFinder.IsClient && !InstanceFinder.IsServer)
                //{
                //    DeferredPlantsManager.TrySpawnQueuedPlants();
                //}
            }
        }
    }
}
