using HarmonyLib;
using MelonLoader;
using UnityEngine.Events;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;
using UnicornsCustomSeeds.SupplierStashes;
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
            LoadManager.Instance.onLoadComplete.AddListener((UnityAction)InitMod);
            SaveManager.Instance.onSaveComplete.AddListener((UnityAction)SaveData);
            //Il2CppAssetBundle ab = AssetBundleUtils.LoadAssetBundle("customshaders");
            //var assets = ab.AllAssetNames();
            //foreach (string name in assets)
            //{
            //    Utility.Log(name);
            //}
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

            //GameObject temp = GameObject.Find("Player_Local");
            //if(temp != null)
            //{
            //    var mgmtCanvas = temp.transform.Find("CameraContainer/Camera/OverlayCamera/ManagementClipboard/Clipboard/ManagementCanvas");
            //    if (mgmtCanvas != null) {
            //        Utility.Success("Canvas GO");
            //        ManagementInterface managementInterface = mgmtCanvas.GetComponent<ManagementInterface>();
            //        if (managementInterface != null)
            //        {
            //            Utility.Success("Found Screen");
            //            foreach (var panel in managementInterface.ConfigPanelPrefabs)
            //            {
            //                Utility.Success(panel.Panel.name);
            //                if (panel.Panel.TryCast<PotConfigPanel>() is PotConfigPanel potSelector)
            //                {
            //                    ItemFieldUI seedUI = potSelector.SeedUI;
            //                    if (seedUI != null)
            //                    {
            //                        Utility.Success("Found SeedUI");
            //                        if(seedUI.Fields.Count > 0)
            //                        {
            //                            Utility.Success("Fields Are Good");
            //                            foreach (ItemDefinition itemDefinition in seedUI.Fields[0].Options)
            //                            {
            //                                Utility.Log(itemDefinition.Name);
            //                            }

                                        
            //                            foreach (var seed in CustomSeedsManager.DiscoveredSeeds)
            //                            {
            //                                SeedDefinition customDef = Registry.GetItem<SeedDefinition>(seed.seedId);
            //                                if (customDef != null)
            //                                {
            //                                    seedUI.Fields[0].Options.Add(customDef);
            //                                }

            //                            }
            //                        } else
            //                        {
            //                            Utility.Success("Fields Are not Good");
            //                        }
            //                    }
            //                }
            //            }
            //        }
            //    }
            //    /*
            //     ManagementInterface temp = Singleton<ManagementInterface>.Instance;
            //if (temp != null) {
            //    Utility.Success("Found Screen");
            //    foreach(var panel in temp.ConfigPanelPrefabs)
            //    {
            //        Utility.Success(panel.Panel.name);
            //        if(panel.Panel.TryCast<PotConfigPanel>() is PotConfigPanel potSelector){
            //            ItemFieldUI seedUI = potSelector.SeedUI;
            //            if (seedUI != null) {
            //                Utility.Success("Found SeedUI");
            //                foreach (ItemDefinition itemDefinition in seedUI.Fields[0].Options)
            //                {
            //                    Utility.Log(itemDefinition.Name);
            //                }

            //                foreach(var seed in CustomSeedsManager.DiscoveredSeeds)
            //                {
            //                    SeedDefinition customDef = Registry.GetItem<SeedDefinition>(seed.seedId);
            //                    if (customDef != null)
            //                    {
            //                        seedUI.Fields[0].Options.Add(customDef);
            //                    }
                                
            //                }
            //            }
            //        }
            //    }
            //}
            //     */

            //}
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {

            if (sceneName.ToLower() == "main")
            {
                // Temp
            } else
            {
                CustomSeedsManager.ClearAll();
            }
        }

        //[HarmonyPatch(typeof(Pot))]
        //[HarmonyPatch(nameof(Pot.InitializeGridItem))]
        //public static class Pot_InitializeGridItem_Patch
        //{
        //    // Prefix runs BEFORE the game’s PotConfiguration.InitializeGridItem
        //    public static bool Prefix(
        //        Pot __instance,
        //        ItemInstance instance,
        //        Grid grid,
        //        Vector2 originCoordinate,
        //        int rotation,
        //        string GUID
        //    )
        //    {
        //        // Example: ensure Seed.Options contains your custom seeds
        //        if (__instance == null)
        //            return true;

        //        Utility.Error("Initialize POTTTTTT");

        //        return true;
        //    }
        //}

        [HarmonyPatch(typeof(ItemFieldUI), nameof(ItemFieldUI.Clicked))]
        public static class ItemFieldUI_Clicked_Patch
        {

            public static bool Prefix(ItemFieldUI __instance)
            {
                if (__instance != null)
                {
                    if (__instance.Fields.Count > 0)
                    {
                        var field = __instance.Fields[0].Options;
                        if (field[field.Count - 1].TryCast<SeedDefinition>() is  SeedDefinition seed)
                        {
                            if (CustomSeedsManager.SeedQueue.Count > 0) {
                                while (CustomSeedsManager.SeedQueue.TryDequeue(out SeedDefinition newSeed)) {
                                    __instance.Fields[0].Options.Add(newSeed);
                                }
                            }
                        }
                    }
                }
                return true;
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