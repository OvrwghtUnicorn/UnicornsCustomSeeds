using System;
using HarmonyLib;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppFishNet.Serializing;
using Il2CppFishNet.Transporting;
using Il2CppScheduleOne;
using Il2CppScheduleOne.Storage;
#elif MONO
using FishNet.Serializing;
using FishNet.Transporting;
using ScheduleOne;
using ScheduleOne.Storage;
#endif

namespace UnicornsCustomSeeds.Patches
{
    // ─────────────────────────────────────────────────────────────────────────
    // DIAGNOSTIC PROBE — client side, observation only.
    //
    // Reports which incoming item IDs a joining client has no definition for.
    // This is the only view into that, and it found the 'deathfuel' payload the
    // host never sent.
    //
    // Nothing here changes behaviour. The prefix returns true, and it rewinds the
    // reader so the original sees an untouched stream. Reading from a Reader
    // ADVANCES it, so that rewind is load-bearing, not tidiness.
    //
    // Two earlier probes lived here and have been removed, having answered their
    // question: a prefix on ItemSerializers.ReadItemInstance fires, while one on
    // ItemInstance.CreateInstanceAndRead never does because IL2CPP inlines it into
    // its only caller. ItemStreamGuard depends on that result for its patch target.
    // ─────────────────────────────────────────────────────────────────────────

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
