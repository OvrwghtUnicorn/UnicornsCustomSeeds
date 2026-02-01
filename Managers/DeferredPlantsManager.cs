using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ObjectScripts;
using MelonLoader;
using System.Reflection;

namespace UnicornsCustomSeeds.Managers
{
    public static class DeferredPlantsManager
    {
        private struct Entry
        {
            public Pot Pot;
            public NetworkConnection Conn;
            public string SeedId;
            public float Progress;
        }

        private static readonly List<Entry> _entries = new List<Entry>();
        public static Dictionary<string,List<(Pot pot, string SeedId, float Progress)>> seedsToLoad = new Dictionary<string, List<(Pot pot, string SeedId, float Progress)>>();

        public static bool IsReplaying = false;

        private static MethodInfo _rpcLogicMethod;

        private static MethodInfo RpcLogicMethod
        {
            get
            {
                if (_rpcLogicMethod == null)
                {
                    _rpcLogicMethod = AccessTools.Method(
                        typeof(Pot),
                        "RpcLogic___PlantSeed_Client_4077118173");

                    if (_rpcLogicMethod == null)
                    {
                        MelonLogger.Warning(
                            "[CustomSeeds] Could not find RpcLogic___PlantSeed_Client_4077118173 via reflection.");
                    }
                }

                return _rpcLogicMethod;
            }
        }

        public static void Enqueue(
            Pot pot,
            NetworkConnection conn,
            string seedId,
            float progress)
        {
            Entry e;
            e.Pot = pot;
            e.Conn = conn;
            e.SeedId = seedId;
            e.Progress = progress;
            _entries.Add(e);
        }

        public static void TrySpawnQueuedPlants(string seedId)
        {
            Utility.Log($"{seedId} : {seedsToLoad[seedId].Count}");
            if (seedsToLoad.ContainsKey(seedId))
            {
                var potsToQueue = seedsToLoad[seedId];
                foreach ( var potTuple in potsToQueue)
                {
                    potTuple.pot.PlantSeed_Client(null,potTuple.SeedId,potTuple.Progress);
                }
            }
        }
    }
}
