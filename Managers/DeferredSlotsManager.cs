using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.ItemFramework;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using ScheduleOne;
using ScheduleOne.ItemFramework;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Client-side retry for container slots whose item definition had not replicated yet.
    ///
    /// ChemistryStation, LabOven and MixingStation call SendItemSlotDataToClient inline from
    /// OnSpawnServer, gated only on the vanilla ProductManager.onProductDataSentToConnection
    /// event. That event fires at the END of ProductManager.OnSpawnServer, which is BEFORE
    /// this mod's Harmony postfix sends the payloads defining the custom items. Measured on
    /// a real join, slot data lands ~3 seconds ahead of the definitions — the client also
    /// spends seconds rebuilding them — so it is not a race that can be won by being faster.
    ///
    /// StorageEntity is included for completeness. It enqueues its send through
    /// ReplicationQueue, which should already order it after the definitions, but it costs
    /// nothing to cover and the [SLOTWAIT] log will say if that assumption is ever wrong.
    ///
    /// ItemStreamGuard keeps the early read from throwing, but the slot ends up empty. This
    /// puts the contents back once the definition is registered. Host state was never wrong:
    /// confirmed by save/reload, where the host still shows the item and only the client
    /// does not.
    ///
    /// Same shape as DeferredPlantsManager and DeferredSeedRebuildManager, one layer over:
    /// one defers a plant until its seed exists, another a seed until its mix exists. This
    /// defers a slot's contents until its item exists.
    ///
    /// Holds the container's ItemSlots list directly rather than the container or an
    /// IItemSlotOwner reference. Four unrelated types own these slots and they share only
    /// that interface, so keeping the list avoids relying on Il2CppInterop's interface
    /// casting for something this simple.
    /// </summary>
    public static class DeferredSlotsManager
    {
        private sealed class PendingSlot
        {
            public string ContainerName;
            public GenericCol.List<ItemSlot> Slots;
            public int SlotIndex;
            public string ItemId;
            public int Quantity;
            public ItemInstance StandIn;   // carries quality; null if the guard fell back
        }

        private static readonly List<PendingSlot> pending = new List<PendingSlot>();

        public static int PendingCount => pending.Count;

        /// <summary>Hold a slot's contents until its item definition shows up.</summary>
        public static void Park(string containerName, GenericCol.List<ItemSlot> slots,
                                int slotIndex, string itemId, int quantity, ItemInstance standIn)
        {
            if (slots == null || string.IsNullOrEmpty(itemId)) return;
            if (slotIndex < 0 || slotIndex >= slots.Count) return;

            foreach (var existing in pending)
                if (ReferenceEquals(existing.Slots, slots) && existing.SlotIndex == slotIndex)
                    return;

            pending.Add(new PendingSlot
            {
                ContainerName = containerName ?? "<unnamed>",
                Slots = slots,
                SlotIndex = slotIndex,
                ItemId = itemId,
                Quantity = quantity,
                StandIn = standIn
            });

            Utility.Log($"[SLOTWAIT] parked '{itemId}' x{quantity} for " +
                        $"'{containerName}' slot {slotIndex} ({pending.Count} pending).");
        }

        /// <summary>
        /// Called after any custom definition is rebuilt. Cheap to call repeatedly — the
        /// pending list holds at most a handful of entries and each one just checks the
        /// Registry.
        /// </summary>
        public static void TryReplayAll()
        {
            if (pending.Count == 0) return;

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var entry = pending[i];

                if (entry.Slots == null)
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

                // The stand-in was the same subclass as the real item, so it read the real
                // quality off the wire. Carry it over or a high-quality pseudo comes back
                // as Standard.
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

                if (entry.SlotIndex < 0 || entry.SlotIndex >= entry.Slots.Count)
                {
                    Utility.Error($"[SLOTWAIT] slot {entry.SlotIndex} out of range on " +
                                  $"'{entry.ContainerName}' — '{entry.ItemId}' dropped.");
                    return;
                }

                // _internal: true so this stays a local correction. The client must not
                // echo it back to the host, whose copy was right all along.
                entry.Slots[entry.SlotIndex].SetStoredItem(real, true);

                Utility.Log($"[SLOTWAIT] restored '{entry.ItemId}' x{entry.Quantity} to " +
                            $"'{entry.ContainerName}' slot {entry.SlotIndex} " +
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
