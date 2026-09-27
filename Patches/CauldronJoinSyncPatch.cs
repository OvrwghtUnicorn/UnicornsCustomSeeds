using System;
using HarmonyLib;
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
    // Cauldron.OnSpawnServer (Cauldron.cs:291) sends the joining client its cook timer and
    // its configuration — and nothing else:
    //
    //     base.OnSpawnServer(connection);
    //     if (this.RemainingCookTime > 0)
    //         this.StartCookOperation(connection, this.RemainingCookTime, this.InputQuality);
    //     this.SendConfigurationToClient(connection);
    //
    // It never sends the item slots. Every other container does: StorageEntity enqueues them
    // through ReplicationQueue, while ChemistryStation, LabOven and MixingStation call
    // SendItemSlotDataToClient inline. Cauldron does neither, even though it implements
    // IItemSlotOwner and has the full SetStoredInstance RPC machinery.
    //
    // This is a vanilla bug, not a mod one. Confirmed on a real join: a cauldron holding
    // coca leaves and gasoline shows full on the host and empty on the client, and the
    // GASOLINE is a vanilla item — so custom definitions are not involved.
    //
    // Ordering does not matter here. If this send lands before a custom item's definition,
    // DeferredSlotPatches parks the slot and DeferredSlotsManager restores it once the
    // definition arrives. So this just adds the missing send, inline, with no queueing.
    //
    // Sends via Cauldron's own public SetStoredInstance / SetSlotLocked rather than
    // IItemSlotOwner.SendItemSlotDataToClient. That method is a C# default interface
    // implementation, which is not reliably reachable through Il2CppInterop, and the loop it
    // runs is short enough to mirror directly.
    // ─────────────────────────────────────────────────────────────────────────
    [HarmonyPatch(typeof(Cauldron), nameof(Cauldron.OnSpawnServer))]
    public static class Patch_Cauldron_OnSpawnServer
    {
        public static void Postfix(Cauldron __instance, NetworkConnection connection)
        {
            try
            {
                if (connection == null) return;
                if (!InstanceFinder.IsServer) return;
                if (connection.IsHost) return;
                if (__instance.ItemSlots == null) return;

                int sent = 0;
                for (int i = 0; i < __instance.ItemSlots.Count; i++)
                {
                    var slot = __instance.ItemSlots[i];
                    if (slot == null) continue;

                    if (slot.ItemInstance != null)
                    {
                        __instance.SetStoredInstance(connection, i, slot.ItemInstance);
                        sent++;
                    }

                    // Cauldrons lock their slots while cooking; without this the client
                    // could drop items into a slot the host considers locked.
                    if (slot.IsLocked && slot.ActiveLock != null)
                    {
                        __instance.SetSlotLocked(connection, i, true,
                            slot.ActiveLock.LockOwner, slot.ActiveLock.LockReason);
                    }
                }

                if (sent > 0)
                {
                    Utility.Log($"[CAULDRONSYNC] sent {sent} slot(s) of '{__instance.name}' " +
                                $"to client {connection.ClientId}.");
                }
            }
            catch (Exception e)
            {
                Utility.Error($"[CAULDRONSYNC] slot send failed for '{__instance.name}'.");
                Utility.PrintException(e);
            }
        }
    }
}
