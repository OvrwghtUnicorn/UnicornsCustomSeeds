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
    // Both receive handlers dereference lazily-resolved values with no null checks, and both
    // run inside FishNet's reader loop — so a null throws there and takes the rest of the
    // packet with it, including the SetReplicationDone that ends the client's loading screen.
    //
    // TWO LAYERS, because predicting readiness turned out not to be enough:
    //
    //   1. Prefix — park when the obvious dependency is missing (recipe not injected,
    //      ingredient not registered). Cheap, and handles the common case.
    //
    //   2. Finalizer — catch anything the prefix failed to predict. ChemistryStation's
    //      handler also touches BoilingFlask, Burner and Alarm, and UpdateClock dereferences
    //      Recipe a second time, so a null this mod did not anticipate still reached the
    //      reader loop even with the prefix in place. The finalizer suppresses the exception,
    //      clears the half-applied operation, and parks it for retry.
    //
    // Clearing the operation on failure matters: every consumer guards on a null
    // CurrentCookOperation, while a half-applied one is non-null with a null member and
    // throws again on every minute pass.
    //
    // Patched on RpcLogic rather than the RpcReader, so the operation is already deserialized
    // and the wire position is not this patch's problem. Both the Target path (join sync) and
    // the Observers path (a cook started by another player) funnel through here.
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
                Utility.Log($"[COOKDIAG] Is not replaying");
                if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return true;
                Utility.Log($"[COOKDIAG] It is the server");
                if (operation == null) return true;
                Utility.Log($"[COOKDIAG] Operation is not null");

                if (DeferredCookOpsManager.IsChemistryOpReady(operation)) return true;

                DeferredCookOpsManager.ParkChemistry(__instance, operation);
                return false;
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
                // Skipping on an unexpected failure beats letting the original run — it is
                // the throw inside the reader that breaks the join.
                return false;
            }
        }

        public static Exception Finalizer(Exception __exception, ChemistryStation __instance,
                                         ChemistryCookOperation operation)
        {
            if (__exception == null) return null;

            // HOST: the vanilla save loader calls SetCookOperation(null, operation) directly
            // (ChemistryStationLoader.cs:52), long before this mod injects its custom recipes
            // in InitMod on onLoadComplete. Vanilla gets away with it because vanilla recipes
            // are present from the start.
            //
            // Park it here too, but do NOT clear the operation: clearing it is what previously
            // made OnSpawnServer skip the send and left a joining client with no cook.
            // Core.InitMod drains the queue once the recipes are in.
            bool onHost = !InstanceFinder.IsClient || InstanceFinder.IsServer;

            DeferredCookOpsManager.HandleChemistryFailure(
                __instance, operation, __exception, clearOperation: !onHost);

            // Returning null suppresses it, so it never reaches FishNet's reader loop.
            return null;
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

        public static Exception Finalizer(Exception __exception, LabOven __instance,
                                         OvenCookOperation operation, bool playButtonPress)
        {
            if (__exception == null) return null;

            // HOST: see the remarks in the chemistry finalizer. LabOvenLoader.cs:52 does the
            // same thing during save load.
            bool onHost = !InstanceFinder.IsClient || InstanceFinder.IsServer;

            DeferredCookOpsManager.HandleOvenFailure(
                __instance, operation, playButtonPress, __exception, clearOperation: !onHost);

            return null;
        }
    }
}
