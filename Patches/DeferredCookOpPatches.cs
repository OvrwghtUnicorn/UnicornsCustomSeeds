using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using FishNet;
using FishNet.Connection;
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // Holds back an in-progress cook operation until what it references exists.
    //
    // Both receive handlers dereference a lazily-resolved value with no null check, and both
    // run inside FishNet's reader loop, so a null throws there and takes the rest of the
    // packet with it — including the SetReplicationDone that ends the client's loading
    // screen. That is the "never leaves the loading screen" symptom.
    //
    // Patched on RpcLogic rather than the RpcReader, so the operation is already
    // deserialized and the wire position is not this patch's problem. Returning false skips
    // the handler completely rather than letting it half-apply: every consumer guards on a
    // null CurrentCookOperation, while a half-applied one is non-null with a null Recipe and
    // throws again on every minute pass.
    //
    // Covers the Target path (join sync) and the Observers path (a cook started by another
    // player) through the same RpcLogic, since both readers funnel into it.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ChemistryStation),
        nameof(ChemistryStation.RpcLogic___SetCookOperation_1024887225))]
    public static class Patch_ChemistryStation_SetCookOperation
    {
        public static bool Prefix(ChemistryStation __instance, NetworkConnection conn,
                                  ChemistryCookOperation operation)
        {
            try
            {
                if (DeferredCookOpsManager.IsReplaying) return true;
                if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return true;
                if (operation == null) return true;

                if (DeferredCookOpsManager.IsChemistryOpReady(operation)) return true;

                DeferredCookOpsManager.ParkChemistry(__instance, operation);
                return false;
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
                // Letting the original run on an unexpected failure is worse than skipping —
                // it is the throw inside the reader that breaks the join.
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(LabOven),
        nameof(LabOven.RpcLogic___SetCookOperation_2611294368))]
    public static class Patch_LabOven_SetCookOperation
    {
        public static bool Prefix(LabOven __instance, NetworkConnection conn,
                                  OvenCookOperation operation, bool playButtonPress)
        {
            try
            {
                if (DeferredCookOpsManager.IsReplaying) return true;
                if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return true;
                if (operation == null) return true;

                if (DeferredCookOpsManager.IsOvenOpReady(operation)) return true;

                DeferredCookOpsManager.ParkOven(__instance, operation, playButtonPress);
                return false;
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
                return false;
            }
        }
    }
}
