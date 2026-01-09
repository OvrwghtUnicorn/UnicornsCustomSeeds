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

                string json = JsonConvert.SerializeObject(seedsIl2cpp, Formatting.Indented);
                File.WriteAllText(filePath, json);
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
                CustomSeedsManager.factory = new SeedFactory(baseSeed);
            }
            // When returning to the main scene clear all data structures to prevent overlap with other saves
            if (sceneName.ToLower() != "main")
            {
                CustomSeedsManager.ClearAll();
            }
        }
    }
}