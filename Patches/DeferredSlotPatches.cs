using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using FishNet;
using FishNet.Serializing;
using FishNet.Transporting;
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // Recovers chemistry station slots that arrived before their item definition.
    //
    // Verified wire order (ChemistryStation.cs:1254):
    //
    //     int itemSlotIndex = PooledReader0.ReadInt32(AutoPackType.Packed);
    //     ItemInstance instance = PooledReader0.ReadItemInstance();
    //
    // The Target variant is the join-sync path — SendItemSlotDataToClient passes a non-null
    // connection, which routes to RpcWriter___Target_*. The Observers variant is ordinary
    // gameplay and is left alone.
    //
    // Split across prefix and postfix because the destination and the item are known in
    // different places. The prefix can read the slot index off the wire but the item has
    // not been deserialized yet; ItemStreamGuard runs nested inside the original and knows
    // the item but not where it was going. So: prefix records the slot, postfix checks
    // whether the guard skipped anything and pairs the two up.
    //
    // Reading from a Reader ADVANCES it, so the prefix rewinds. That rewind is
    // load-bearing — without it the original reads from the wrong offset.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(ChemistryStation),
        nameof(ChemistryStation.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_ChemistryStation_TargetSlotReader
    {
        private static int pendingSlotIndex = -1;

        public static void Prefix(PooledReader PooledReader0)
        {
            pendingSlotIndex = -1;
            ItemStreamGuard.ClearLastSkipped();

            if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return;

            int start = PooledReader0.Position;
            try
            {
                pendingSlotIndex = PooledReader0.ReadInt32(AutoPackType.Packed);
            }
            catch (Exception)
            {
                pendingSlotIndex = -1;
            }
            finally
            {
                PooledReader0.Position = start;
            }
        }

        public static void Postfix(ChemistryStation __instance)
        {
            try
            {
                if (pendingSlotIndex < 0) return;

                string skipped = ItemStreamGuard.LastSkippedId;
                if (string.IsNullOrEmpty(skipped)) return;

                DeferredSlotsManager.Park(
                    __instance,
                    pendingSlotIndex,
                    skipped,
                    ItemStreamGuard.LastSkippedQuantity,
                    ItemStreamGuard.LastSkippedStandIn);
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
            finally
            {
                pendingSlotIndex = -1;
                ItemStreamGuard.ClearLastSkipped();
            }
        }
    }
}
