using System;
using HarmonyLib;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Storage;
#elif MONO
using FishNet.Serializing;
using FishNet.Transporting;
using ScheduleOne;
using ScheduleOne.ItemFramework;
using ScheduleOne.Storage;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // TEMPORARY PROBES — client side, observation only.
    //
    // Question: on a joining client, can we extract an incoming item's ID, its
    // quantity, and the ItemSlot it was destined for?
    //
    // Nothing here changes behaviour. Every prefix returns true, and the probe
    // that reads from the stream rewinds it so the original sees an untouched
    // reader.
    //
    // Reading from a Reader ADVANCES it. A prefix that reads and then returns
    // true makes the original re-read from the wrong offset and corrupt every
    // field after it, so the rewind is load-bearing, not tidiness.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Confirms a patch here is live. Reads nothing, so it cannot disturb the stream.</summary>
    [HarmonyPatch(typeof(ItemSerializers), nameof(ItemSerializers.ReadItemInstance))]
    public static class Probe_ReadItemInstance
    {
        public static int Calls;

        public static bool Prefix(Reader reader)
        {
            if (Calls++ == 0)
                Utility.Log("[PROBE] ItemSerializers.ReadItemInstance prefix FIRED — patchable.");
            return true;
        }
    }

    /// <summary>
    /// Tests whether CreateInstanceAndRead survives IL2CPP inlining. If ReadItemInstance
    /// logs and this never does, it was inlined into its only caller. Reads nothing.
    /// </summary>
    [HarmonyPatch(typeof(ItemInstance), nameof(ItemInstance.CreateInstanceAndRead))]
    public static class Probe_CreateInstanceAndRead
    {
        public static int Calls;

        public static bool Prefix(Reader reader)
        {
            if (Calls++ == 0)
                Utility.Log("[PROBE] ItemInstance.CreateInstanceAndRead prefix FIRED — NOT inlined.");
            return true;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verified wire order (StorageEntity.cs:666 Observers, :710 Target):
    //
    //     int itemSlotIndex = PooledReader0.ReadInt32(AutoPackType.Packed);
    //     ItemInstance instance = PooledReader0.ReadItemInstance();
    //
    // No NetworkConnection on the wire for these two — only the Server variant
    // reads one. ItemInstance.Write emits ID (string) then Quantity (ushort), so
    // slot + id + quantity are all reachable from here.
    // ─────────────────────────────────────────────────────────────────────────
    public static class StorageProbe
    {
        private static int reads;
        private static int misses;

        public static void Inspect(string source, StorageEntity entity, PooledReader reader)
        {
            int start = reader.Position;
            try
            {
                int slot = reader.ReadInt32(AutoPackType.Packed);
                string id = reader.ReadString();
                int qty = reader.ReadUInt16();

                reads++;

                // Only the misses matter now — a known item needs no attention, and logging
                // every slot buried the signal under ~100 lines last run.
                if (!string.IsNullOrEmpty(id) && !Registry.ItemExists(id))
                {
                    misses++;
                    Utility.Error($"[PROBE] MISS {source} '{(entity != null ? entity.name : "<null>")}' " +
                                  $"slot={slot} id='{id}' qty={qty} — not in Registry, slot will stay empty " +
                                  $"({misses} miss(es) of {reads} reads).");
                }
            }
            catch (Exception e)
            {
                Utility.Error($"[PROBE] {source} inspect failed.");
                Utility.PrintException(e);
            }
            finally
            {
                // Must happen on every path, or the original reads from the wrong offset.
                reader.Position = start;
            }
        }
    }

    [HarmonyPatch(typeof(StorageEntity), nameof(StorageEntity.RpcReader___Target_SetStoredInstance_Internal_2652194801))]
    public static class Probe_StorageEntity_TargetReader
    {
        public static bool Prefix(StorageEntity __instance, PooledReader PooledReader0, Channel channel)
        {
            StorageProbe.Inspect("Target", __instance, PooledReader0);
            return true;
        }
    }

    [HarmonyPatch(typeof(StorageEntity), nameof(StorageEntity.RpcReader___Observers_SetStoredInstance_Internal_2652194801))]
    public static class Probe_StorageEntity_ObserversReader
    {
        public static bool Prefix(StorageEntity __instance, PooledReader PooledReader0, Channel channel)
        {
            StorageProbe.Inspect("Observers", __instance, PooledReader0);
            return true;
        }
    }
}
