using System;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet;
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.StationFramework;
using Il2CppScheduleOne.Storage;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using FishNet;
using FishNet.Serializing;
using FishNet.Transporting;
using ScheduleOne.ItemFramework;
using ScheduleOne.NPCs;
using ScheduleOne.ObjectScripts;
using ScheduleOne.StationFramework;
using ScheduleOne.Storage;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // Recovers container slots that arrived before their item definition.
    //
    // Verified wire order, identical in all four containers (e.g. ChemistryStation.cs:1254):
    //
    //     int itemSlotIndex = PooledReader0.ReadInt32(AutoPackType.Packed);
    //     ItemInstance instance = PooledReader0.ReadItemInstance();
    //
    // All four share the generated suffix _2652194801, so one helper covers them.
    //
    // Only the Target variant is patched. That is the join-sync path:
    // SendItemSlotDataToClient passes a non-null connection, which routes to
    // RpcWriter___Target_*. The Observers variant is ordinary gameplay, where the client
    // already has every definition, and is left alone.
    //
    // Split across prefix and postfix because the destination and the item are known in
    // different places. The prefix can read the slot index off the wire but the item has not
    // been deserialized yet; ItemStreamGuard runs nested inside the original and knows the
    // item but not where it was going. So: prefix records the slot, postfix checks whether
    // the guard skipped anything and pairs the two up.
    //
    // Reading from a Reader ADVANCES it, so the prefix rewinds. That rewind is load-bearing
    // — without it the original reads from the wrong offset and corrupts every field after.
    // ─────────────────────────────────────────────────────────────────────────
    internal static class SlotReaderGuard
    {
        private static int pendingSlotIndex = -1;

        public static void Before(PooledReader reader)
        {
            pendingSlotIndex = -1;
            ItemStreamGuard.ClearLastSkipped();

            if (!InstanceFinder.IsClient || InstanceFinder.IsServer) return;
            if (reader == null) return;

            int start = reader.Position;
            try
            {
                pendingSlotIndex = reader.ReadInt32(AutoPackType.Packed);
            }
            catch (Exception)
            {
                pendingSlotIndex = -1;
            }
            finally
            {
                // Must happen on every path, or the original reads from the wrong offset.
                reader.Position = start;
            }
        }

        public static void After(string containerName, GenericCol.List<ItemSlot> slots)
        {
            try
            {
                if (pendingSlotIndex < 0) return;

                string skipped = ItemStreamGuard.LastSkippedId;
                if (string.IsNullOrEmpty(skipped)) return;

                DeferredSlotsManager.Park(
                    containerName,
                    slots,
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

    [HarmonyPatch(typeof(ChemistryStation),
        nameof(ChemistryStation.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_ChemistryStation_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(ChemistryStation __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    [HarmonyPatch(typeof(LabOven),
        nameof(LabOven.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_LabOven_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(LabOven __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    [HarmonyPatch(typeof(MixingStation),
        nameof(MixingStation.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_MixingStation_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(MixingStation __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    [HarmonyPatch(typeof(StorageEntity),
        nameof(StorageEntity.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_StorageEntity_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(StorageEntity __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    [HarmonyPatch(typeof(Cauldron),
        nameof(Cauldron.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_Cauldron_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(Cauldron __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Three more IItemSlotOwner types that can plausibly hold a custom drug item.
    //
    // Added after a join log showed two [ITEMGUARD] fires with no matching park —
    // 'kryptonitesucks_customliquidmeth' and 'bananafruit_customcocainebase' reached a
    // container not yet covered. Twelve types share this same generated reader; these are
    // the ones that can actually hold these items. A chemist carries ingredients between
    // stations (NPCInventory), and the drying rack and mushroom spawn station both take
    // custom inputs.
    //
    // Deliberately NOT covered: BrickPress, PackagingStation and PlayerClothing, which never
    // hold these intermediates.
    // ─────────────────────────────────────────────────────────────────────────

    [HarmonyPatch(typeof(NPCInventory),
        nameof(NPCInventory.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_NPCInventory_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(NPCInventory __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    [HarmonyPatch(typeof(DryingRack),
        nameof(DryingRack.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_DryingRack_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(DryingRack __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }

    [HarmonyPatch(typeof(MushroomSpawnStation),
        nameof(MushroomSpawnStation.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Patch_MushroomSpawnStation_TargetSlotReader
    {
        public static void Prefix(PooledReader PooledReader0) => SlotReaderGuard.Before(PooledReader0);
        public static void Postfix(MushroomSpawnStation __instance) =>
            SlotReaderGuard.After(__instance.name, __instance.ItemSlots);
    }
}
