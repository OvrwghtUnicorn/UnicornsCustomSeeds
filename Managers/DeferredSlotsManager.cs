using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.ObjectScripts;
#elif MONO
using ScheduleOne;
using ScheduleOne.ItemFramework;
using ScheduleOne.ObjectScripts;
#endif

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Client-side retry for station slots whose item definition had not replicated yet.
    ///
    /// ChemistryStation.OnSpawnServer calls SendItemSlotDataToClient inline, gated only on
    /// the vanilla ProductManager.onProductDataSentToConnection event. That event fires at
    /// the END of ProductManager.OnSpawnServer, which is BEFORE this mod's Harmony postfix
    /// sends the payloads that define the custom items. Measured on a real join, the slot
    /// data arrives roughly 3 seconds ahead of the definitions — the client also spends
    /// seconds rebuilding them — so it is not a race we can win by being faster.
    ///
    /// ItemStreamGuard keeps that early read from throwing, but the slot ends up empty.
    /// This puts the item back once its definition exists. Host state was never wrong:
    /// confirmed by save/reload, where the host still shows the item and only the client
    /// does not.
    ///
    /// Same shape as DeferredPlantsManager and DeferredSeedRebuildManager, one layer over:
    /// that one defers a plant until its seed exists, and the other a seed until its mix
    /// exists. This defers a slot's contents until its item exists.
    /// </summary>
    public static class DeferredSlotsManager
    {
        private sealed class PendingSlot
        {
            public ChemistryStation Station;
            public int SlotIndex;
            public string ItemId;
            public int Quantity;
            public ItemInstance StandIn;   // carries quality; null if the guard fell back
        }

        private static readonly List<PendingSlot> pending = new List<PendingSlot>();

        public static int PendingCount => pending.Count;

        /// <summary>Hold a slot's contents until its item definition shows up.</summary>
        public static void Park(ChemistryStation station, int slotIndex, string itemId,
                               int quantity, ItemInstance standIn)
        {
            if (station == null || string.IsNullOrEmpty(itemId)) return;

            foreach (var existing in pending)
                if (existing.Station == station && existing.SlotIndex == slotIndex)
                    return;

            pending.Add(new PendingSlot
            {
                Station = station,
                SlotIndex = slotIndex,
                ItemId = itemId,
                Quantity = quantity,
                StandIn = standIn
            });

            Utility.Log($"[SLOTWAIT] parked '{itemId}' x{quantity} for " +
                        $"'{station.name}' slot {slotIndex} ({pending.Count} pending).");
        }

        /// <summary>
        /// Called after any custom definition is rebuilt. Cheap to call repeatedly — the
        /// pending list holds at most a handful of entries, and each one just checks the
        /// Registry.
        /// </summary>
        public static void TryReplayAll()
        {
            if (pending.Count == 0) return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var entry = pending[i];

                if (entry.Station == null)
                {
                    pending.RemoveAt(i);
                    continue;
                }

                if (!Registry.ItemExists(entry.ItemId)) continue;

                pending.RemoveAt(i);
                Replay(entry);
            }
        }

        private static void Replay(PendingSlot entry)
        {
            try
            {
                var def = Registry.GetItem(entry.ItemId);
                if (def == null) return;

                ItemInstance real = def.GetDefaultInstance(1);
                if (real == null) return;

                real.SetQuantity(entry.Quantity > 0 ? entry.Quantity : 1);

                // The stand-in was the same subclass as the real item, so it read the
                // real quality off the wire. Carry it over or a high-quality pseudo comes
                // back as Standard.
                if (entry.StandIn != null)
                {
#if IL2CPP
                    var realQ = real.TryCast<QualityItemInstance>();
                    var standQ = entry.StandIn.TryCast<QualityItemInstance>();
#elif MONO
                    var realQ = real as QualityItemInstance;
                    var standQ = entry.StandIn as QualityItemInstance;
#endif
                    if (realQ != null && standQ != null)
                        realQ.Quality = standQ.Quality;
                }

                if (entry.Station.ItemSlots == null ||
                    entry.SlotIndex < 0 || entry.SlotIndex >= entry.Station.ItemSlots.Count)
                {
                    Utility.Error($"[SLOTWAIT] slot {entry.SlotIndex} out of range on " +
                                  $"'{entry.Station.name}' — '{entry.ItemId}' dropped.");
                    return;
                }

                entry.Station.ItemSlots[entry.SlotIndex].SetStoredItem(real, true);

                Utility.Log($"[SLOTWAIT] restored '{entry.ItemId}' x{entry.Quantity} to " +
                            $"'{entry.Station.name}' slot {entry.SlotIndex} " +
                            $"({pending.Count} still pending).");
            }
            catch (Exception e)
            {
                Utility.Error($"[SLOTWAIT] replay of '{entry.ItemId}' threw.");
                Utility.PrintException(e);
            }
        }

        public static void ClearAll() => pending.Clear();
    }
}
