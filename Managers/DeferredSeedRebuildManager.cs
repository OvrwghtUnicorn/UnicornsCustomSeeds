using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.Seeds;
using UnicornsCustomSeeds.TemplateUtils;

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Client-side retry for custom seed payloads that arrive before their base mix.
    ///
    /// The host sends every [NET-JSON] payload immediately from its
    /// ProductManager.OnSpawnServer postfix, but the vanilla product replication that
    /// carries the base mixes is QUEUED through ReplicationQueue. A payload can
    /// therefore land before its WeedDefinition exists, and
    /// CustomSeedsManager.SeedDefinitionLoader has no retry — it logs and drops.
    /// Observed as the host registering 6 custom seeds and the client rebuilding 4.
    ///
    /// Same idea as DeferredPlantsManager, one layer up: that one defers a plant until
    /// its seed definition exists, this defers a seed definition until its mix exists.
    /// Deliberately a separate class rather than an extension of it.
    ///
    /// Keyed by mixId because that is what the drain site knows: the postfix on
    /// RpcLogic___CreateWeed receives the mix id when a real product arrives.
    /// </summary>
    public static class DeferredSeedRebuildManager
    {
        private static readonly Dictionary<string, List<UnicornSeedData>> pending =
            new Dictionary<string, List<UnicornSeedData>>();

        public static int PendingCount
        {
            get
            {
                int n = 0;
                foreach (var kvp in pending) n += kvp.Value.Count;
                return n;
            }
        }

        /// <summary>Hold a payload until its mix shows up.</summary>
        public static void Park(UnicornSeedData data)
        {
            if (data == null || string.IsNullOrEmpty(data.mixId)) return;

            if (!pending.TryGetValue(data.mixId, out var list))
            {
                list = new List<UnicornSeedData>();
                pending[data.mixId] = list;
            }

            // A payload can be re-sent; do not queue it twice.
            foreach (var existing in list)
                if (existing.seedId == data.seedId) return;

            list.Add(data);
            Utility.Log($"[SEEDWAIT] parked '{data.seedId}' — waiting on mix '{data.mixId}' ({PendingCount} pending).");
        }

        /// <summary>The named mix just arrived; build anything that was waiting on it.</summary>
        public static void TryRebuild(string mixId)
        {
            if (string.IsNullOrEmpty(mixId)) return;
            if (!pending.TryGetValue(mixId, out var list)) return;

            pending.Remove(mixId);

            foreach (var data in list)
            {
                try
                {
                    bool built = CustomSeedsManager.RebuildFromPayload(data);
                    if (built)
                        Utility.Log($"[SEEDWAIT] mix '{mixId}' arrived — rebuilt '{data.seedId}'.");
                    else
                        Utility.Error($"[SEEDWAIT] mix '{mixId}' arrived but '{data.seedId}' still failed to build.");
                }
                catch (Exception e)
                {
                    Utility.Error($"[SEEDWAIT] rebuild of '{data.seedId}' threw.");
                    Utility.PrintException(e);
                }
            }
        }

        public static void ClearAll() => pending.Clear();
    }
}
