#if IL2CPP
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Managers
{
    public static class DeferredPlantsManager
    {
        public struct DeferredSeedData
        {
            public Pot Pot;
            public string SeedId;
            public float Progress;
        }

        public struct HarvestableUpdateData
        {
            public int Index;
            public bool Active;
        }
        public static bool IsReplaying = false;

        public static HashSet<string> PendingPotGuids = new HashSet<string>();
        public static Dictionary<string, List<DeferredSeedData>> seedsToLoad = new Dictionary<string, List<DeferredSeedData>>();
        public static Dictionary<string, List<HarvestableUpdateData>> DeferredHarvestables = new Dictionary<string, List<HarvestableUpdateData>>();

        public static void AddDeferredSeed(Pot pot, string seedId, float progress)
        {
            if (seedsToLoad.ContainsKey(seedId))
            {
                seedsToLoad[seedId].Add(new DeferredSeedData { Pot = pot, SeedId = seedId, Progress = progress });
            }
            else
            {
                List<DeferredSeedData> seedList = new List<DeferredSeedData>();
                seedList.Add(new DeferredSeedData { Pot = pot, SeedId = seedId, Progress = progress });
                seedsToLoad.Add(seedId, seedList);
            }
            PendingPotGuids.Add(pot.GUID.ToString());
        }

        public static void AddHarvestableUpdate(string potGuid, int index, bool active)
        {
            if (!DeferredHarvestables.ContainsKey(potGuid))
            {
                DeferredHarvestables[potGuid] = new List<HarvestableUpdateData>();
            }
            DeferredHarvestables[potGuid].Add(new HarvestableUpdateData { Index = index, Active = active });
        }

        public static void TrySpawnQueuedPlants(string seedId)
        {
            Utility.Log($"{seedId} : {seedsToLoad[seedId].Count}");
            if (seedsToLoad.ContainsKey(seedId))
            {
                var potsToQueue = seedsToLoad[seedId];
                foreach ( var data in potsToQueue)
                {
                    data.Pot.PlantSeed_Client(null, data.SeedId, data.Progress);

                    string potGuid = data.Pot.GUID.ToString();
                    if (DeferredHarvestables.ContainsKey(potGuid))
                    {
                        foreach (var update in DeferredHarvestables[potGuid])
                        {
                            data.Pot.Plant.SetHarvestableActive(update.Index, update.Active);
                        }
                        DeferredHarvestables.Remove(potGuid);
                    }
                    PendingPotGuids.Remove(potGuid);
                }
            }
        }
    }
}
