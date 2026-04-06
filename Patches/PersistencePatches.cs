using HarmonyLib;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;



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
                    string json = File.ReadAllText(filePath);

                    try
                    {
                        JArray jArray = JArray.Parse(json);

                        // Detect legacy schema: first element has "weedId" property
                        if (jArray.Count > 0 && jArray[0] is JObject firstObj && firstObj.ContainsKey("weedId"))
                        {
                            // Legacy migration path
                            var legacySeeds = JsonConvert.DeserializeObject<List<LegacySeedData>>(json) ?? new List<LegacySeedData>();
                            Utility.Success($"Migrating {legacySeeds.Count} legacy seed records to new format");

                            foreach (var legacy in legacySeeds)
                            {
                                seedsIl2cpp.Add(new UnicornSeedData
                                {
                                    seedId = legacy.seedId,
                                    mixId = legacy.weedId,
                                    drugType = EDrugType.Marijuana,
                                    price = legacy.price,
                                });
                            }

                            // Overwrite save file with new format
                            string migratedJson = JsonConvert.SerializeObject(seedsIl2cpp, Formatting.Indented);
                            File.WriteAllText(filePath, migratedJson);
                            Utility.Success("Save file migrated to new format");
                        }
                        else
                        {
                            // Already new format
                            seedsIl2cpp = JsonConvert.DeserializeObject<List<UnicornSeedData>>(json) ?? new List<UnicornSeedData>();
                        }

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
                    if (seedData != null && !CustomSeedsManager.DiscoveredSeeds.ContainsKey(seedData.mixId))
                    {
                        CustomSeedsManager.DiscoveredSeeds.Add(seedData.mixId, seedData);
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
            if (Registry.ItemExists(id + "_customseeddefinition"))
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
                    catch (Exception ex)
                    {
                        Utility.PrintException(ex);
                    }
                }
            }
        }
    }
}
