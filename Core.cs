using HarmonyLib;
using MelonLoader;
using UnityEngine.Events;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using Il2CppScheduleOne.Equipping;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.UI.Management;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne;
using UnicornsCustomSeeds.Seeds;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;


#if IL2CPP
using S1PlayerTasks = Il2CppScheduleOne.PlayerTasks;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.Persistence;
using Il2CppScheduleOne.PlayerScripts;
#elif MONO
using S1PlayerTasks = ScheduleOne.PlayerTasks;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.Persistence;
using ScheduleOne.PlayerScripts;
#endif

[assembly: MelonInfo(typeof(UnicornsCustomSeeds.Core), UnicornsCustomSeeds.BuildInfo.Name, UnicornsCustomSeeds.BuildInfo.Version, UnicornsCustomSeeds.BuildInfo.Author, UnicornsCustomSeeds.BuildInfo.DownloadLink)]
[assembly: MelonColor(255, 191, 0, 255)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UnicornsCustomSeeds
{
    public static class BuildInfo
    {
        public const string Name = "Unicorns Custom Seeds";
        public const string Description = "Your good buddy Unicorn can help you make these seeds";
        public const string Author = "OverweightUnicorn";
        public const string Company = "UnicornsCanMod";
        public const string Version = "1.0.0";
        public const string DownloadLink = null;
    }
		
    public class Core : MelonMod {

        public override void OnLateInitializeMelon() {
            StashManager.InitializeConfig();
            LoadManager.Instance.onLoadComplete.AddListener((UnityAction)InitMod);
            SaveManager.Instance.onSaveComplete.AddListener((UnityAction)SaveData);
        }

        public void SaveData()
        {
            try
            {
                string saveFolder = Singleton<LoadManager>.Instance.LoadedGameFolderPath;
                if (string.IsNullOrEmpty(saveFolder) || !Directory.Exists(saveFolder))
                {
                    return;
                }

                string filePath = Path.Combine(saveFolder, "DiscoveredCustomSeeds.json");
                List<UnicornSeedData> seedsIl2cpp = CustomSeedsManager.DiscoveredSeeds.Values.ToList();
                foreach (var seed in seedsIl2cpp)
                {
                    Utility.Log($"{seed.seedId}");
                }
                Utility.Log($"Save Location: {filePath}");
                string json = JsonConvert.SerializeObject(seedsIl2cpp, Formatting.Indented);
                File.WriteAllText(filePath, json);
                MelonLogger.Msg("Saved to DiscoveredCustomSeeds.json with custom seeds.");
            }
            catch (Exception e) { 
                Utility.PrintException(e);
            }
        }

        public void InitMod()
        {
            CustomSeedsManager.Initialize();
            StashManager.GetAlbertsStash();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
            if (CustomSeedsManager.factory == null && baseSeed != null)
            {
                Utility.Log("Initializing Factory");
                CustomSeedsManager.factory = new SeedFactory(baseSeed);
            }
            // When returning to the main scene clear all data structures to prevent overlap with other saves
            if (sceneName.ToLower() != "main")
            {
                CustomSeedsManager.ClearAll();
            }
        }

        //[HarmonyPatch(typeof(ItemFieldUI), nameof(ItemFieldUI.Clicked))]
        //public static class ItemFieldUI_Clicked_Patch
        //{

        //    public static bool Prefix(ItemFieldUI __instance)
        //    {
        //        if (__instance != null)
        //        {
        //            if (__instance.name == "Seed")
        //            {
        //                var options = __instance.Fields[0].Options;
        //                var currentOptionIds = new System.Collections.Generic.HashSet<string>();

        //                foreach (var option in options)
        //                {
        //                    if (option != null)
        //                    {
        //                        currentOptionIds.Add(option.ID);
        //                    }
        //                }

        //                foreach (var unicornSeedData in CustomSeedsManager.DiscoveredSeeds.Values)
        //                {
        //                    if (!currentOptionIds.Contains(unicornSeedData.seedId))
        //                    {
        //                        SeedDefinition newSeed = Registry.GetItem<SeedDefinition>(unicornSeedData.seedId);
        //                        if (newSeed != null)
        //                        {
        //                            options.Add(newSeed);
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //        return true;
        //    }

        //}

        [HarmonyPatch(typeof(S1PlayerTasks.SowSeedTask), nameof(S1PlayerTasks.SowSeedTask.OnSeedReachedDestination))]
        public static class SowSeedTask_OnSeedReachedDestination_Patch {

            public static bool Prefix(S1PlayerTasks.SowSeedTask __instance) {
                Utility.Log($"{__instance.definition.ID} Seed has reached destination");
                Utility.Log($"Player has this many seeds {PlayerSingleton<PlayerInventory>.Instance.GetAmountOfItem(__instance.definition.ID)}");
                FunctionalSeed functionalSeed = __instance.seed;
                if (functionalSeed != null) {
                    Utility.Log($"{functionalSeed.gameObject.name} Exists");
                }
                return true;
            }

        }

        [HarmonyPatch(typeof(S1PlayerTasks.SowSeedTask), nameof(S1PlayerTasks.SowSeedTask.Success))]
        public static class SowSeedTask_Success_Patch {

            public static bool Prefix(S1PlayerTasks.SowSeedTask __instance) {
                Utility.Log($"{__instance.definition.ID} has been planted");
                return true;
            }

            public static void Postfix(S1PlayerTasks.SowSeedTask __instance) {
                MelonLogger.Msg("Product is being consumed");
                __instance.pot.Plant.NormalizedGrowthProgress = 1;
                int length = __instance.pot.Plant.GrowthStages.Length;
                for (int i = 0; i < __instance.pot.Plant.GrowthStages.Length; i++) {
                    if (i >= length - 1) {
                        __instance.pot.Plant.GrowthStages[i].gameObject.SetActive(true);
                    } else {
                        __instance.pot.Plant.GrowthStages[i].gameObject.SetActive(false);
                    }

                }
                __instance.pot.Plant.GrowthDone();
            }
        }
    }
}