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
                if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return true;
                if (operation == null) return true;

                bool ready = DeferredCookOpsManager.IsChemistryOpReady(operation);

                // DIAGNOSTIC: a join showed the host cooking while the client showed nothing,
                // with no line from this patch at all. This says whether the handler is even
                // reached, and what the readiness check decided.
                Utility.Log($"[COOKDIAG] chemistry handler reached for '{operation.RecipeID}' " +
                            $"on '{__instance.name}' — ready={ready}.");

                if (ready) return true;

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

            // HOST: do not touch it. This whole park/replay mechanism is client-side —
            // TryReplayAll is only driven from the client's definition-rebuild paths, so a
            // host-side park never replays. Worse, clearing CurrentCookOperation here left
            // the host with no operation to send, so ChemistryStation.OnSpawnServer's
            // "if (CurrentCookOperation != null)" skipped the send entirely and a joining
            // client saw no cook at all. Returning the exception restores exactly the
            // pre-patch behaviour: it surfaces in the log, and the operation stays set so it
            // still replicates.
            if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return __exception;

            DeferredCookOpsManager.HandleChemistryFailure(__instance, operation, __exception);

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

            // HOST: do not touch it. This whole park/replay mechanism is client-side —
            // TryReplayAll is only driven from the client's definition-rebuild paths, so a
            // host-side park never replays. Worse, clearing CurrentCookOperation here left
            // the host with no operation to send, so ChemistryStation.OnSpawnServer's
            // "if (CurrentCookOperation != null)" skipped the send entirely and a joining
            // client saw no cook at all. Returning the exception restores exactly the
            // pre-patch behaviour: it surfaces in the log, and the operation stays set so it
            // still replicates.
            if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return __exception;

            DeferredCookOpsManager.HandleOvenFailure(__instance, operation, playButtonPress, __exception);

            return null;
        }
    }
    // ─────────────────────────────────────────────────────────────────────────
    // DIAGNOSTIC — temporary. Remove once the chemistry cook question is settled.
    //
    // The readers run one level above RpcLogic and fire even when RpcLogic is skipped, e.g.
    // the generated reader's own "if (!base.IsClientInitialized) return;" which drops the
    // operation silently. Together with [COOKDIAG] in the RpcLogic prefix this gives a clean
    // decision tree for a cook that never appears on the client:
    //
    //   neither line      → the host never sent it
    //   reader only       → arrived but was dropped before the handler
    //   reader + handler  → the handler ran, so the problem is downstream of this mod
    //
    // Patched by string name, not nameof: these readers are private.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(ChemistryStation), "RpcReader___Target_SetCookOperation_1024887225")]
    public static class Diag_ChemistryStation_TargetCookReader
    {
        public static void Prefix(ChemistryStation __instance) =>
            Utility.Log($"[COOKDIAG] chemistry cook packet arrived (Target) for '{__instance.name}'.");
    }

    [HarmonyPatch(typeof(ChemistryStation), "RpcReader___Observers_SetCookOperation_1024887225")]
    public static class Diag_ChemistryStation_ObserversCookReader
    {
        public static void Prefix(ChemistryStation __instance) =>
            Utility.Log($"[COOKDIAG] chemistry cook packet arrived (Observers) for '{__instance.name}'.");
    }
}
