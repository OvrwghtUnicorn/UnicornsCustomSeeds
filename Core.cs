using HarmonyLib;
using MelonLoader;
using UnityEngine.Events;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;



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
            LoadManager.Instance.onLoadComplete.AddListener((UnityAction)CustomSeedsManager.Initialize);
            //Il2CppAssetBundle ab = AssetBundleUtils.LoadAssetBundle("customshaders");
            //var assets = ab.AllAssetNames();
            //foreach (string name in assets)
            //{
            //    Utility.Log(name);
            //}
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {

            if (sceneName.ToLower() == "main")
            {
                // Temp
            } else
            {
                CustomSeedsManager.DiscoveredSeeds.Clear();
                CustomSeedsManager.loadedSeeds.Clear();
            }
        }

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