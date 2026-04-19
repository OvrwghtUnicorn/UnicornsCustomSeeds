using MelonLoader;
using UnityEngine.Events;
using UnicornsCustomSeeds.Seeds;
using Newtonsoft.Json;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.Patches;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Persistence;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.Growing;
using ScheduleOne.ItemFramework;
using ScheduleOne.Persistence;
#endif

[assembly: MelonInfo(typeof(UnicornsCustomSeeds.Core), UnicornsCustomSeeds.BuildInfo.Name, UnicornsCustomSeeds.BuildInfo.Version, UnicornsCustomSeeds.BuildInfo.Author, UnicornsCustomSeeds.BuildInfo.DownloadLink)]
[assembly: MelonColor(255, 191, 0, 255)]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace UnicornsCustomSeeds
{
    public static class BuildInfo
    {
        public const string Name = "Unicorns Custom Seeds";
        public const string Description = "Your good buddy Unicorn can help you synthesize seeds";
        public const string Author = "OverweightUnicorn";
        public const string Company = "UnicornsCanMod";
        public const string Version = "1.1.1";
        public const string DownloadLink = null;
    }

    public class Core : MelonMod
    {

        public override void OnInitializeMelon()
        {
            AssetBundleUtils.Initialize(this);
        }

        public override void OnLateInitializeMelon()
        {
            StashManager.InitializeConfig();
            SeedVisualsManager.LoadSeedMaterial();
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
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        public void InitMod()
        {
            CustomSeedsManager.Initialize();
            CustomShroomsManager.Initialize();
            StashManager.GetAlbertsStash();

        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            var baseSeed = Registry.GetItem<SeedDefinition>("ogkushseed");
            if (CustomSeedsManager.factory == null && baseSeed != null)
            {
                CustomSeedsManager.factory = new SeedFactory(baseSeed);
            }

            var baseSyringe = Registry.GetItem<SporeSyringeDefinition>(CustomShroomsManager.BASE_SYRINGE_ID);
            if (CustomShroomsManager.factory == null && baseSyringe != null)
            {
                CustomShroomsManager.factory = new SyringeFactory(baseSyringe);
            }

            StashManager.GetAlbertsStash();

            // When returning to the main scene clear all data structures to prevent overlap with other saves
            if (sceneName.ToLower() != "main")
            {
                CustomSeedsManager.ClearAll();
                CustomShroomsManager.ClearAll();
                ProductManagerAppPatches.ClearPendingIndicators();
            }
            else
            {
                // Reload assets when entering main scene to prevent garbage collection issues
                if (SeedVisualsManager.seedIcon == null || SeedVisualsManager.baseSeedSprite == null)
                {
                    SeedVisualsManager.LoadSeedMaterial();
                }
            }
        }
    }
}