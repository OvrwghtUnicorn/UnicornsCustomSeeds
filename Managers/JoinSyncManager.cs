using System;
using System.Collections.Generic;
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

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Host-side join-sync ordering for containers the game replicates too early.
    ///
    /// The game already has an ordering hook for this: IItemSlotOwner.SendItemSlotDataToClient
    /// waits on ProductManager.HasSentProductDataToConnection / onProductDataSentToConnection
    /// before writing item slots, so a joining client always has the product definitions
    /// before it has to resolve an item ID. That hook is useless to this mod, because
    /// ProductManager fires onProductDataSentToConnection at the END of its own
    /// OnSpawnServer — which is BEFORE our Harmony postfix sends the [NET-JSON] payloads
    /// that define the custom items. Anything relying on the vanilla hook flushes one step
    /// too early and the client NREs inside ItemInstance.CreateInstanceAndRead, which has
    /// no null check on Registry.GetItem.
    ///
    /// So this is the same pattern, one step later: a per-connection "custom definitions
    /// have been sent" flag, plus a parking list for containers that spawned before that
    /// happened. NetworkingPatches drains it at the end of the ProductManager postfix.
    ///
    /// Deliberately NOT built on ReplicationQueue.Enqueue: that takes an
    /// Action&lt;NetworkConnection&gt;, which means marshalling a managed delegate into
    /// Il2CppSystem.Action on the IL2CPP target. Draining by hand avoids that entirely and
    /// the payloads here are a handful of slots, well under the queue's rate limit.
    /// </summary>
    public static class JoinSyncManager
    {
        // ClientIds the mod has finished sending custom definitions to.
        private static readonly HashSet<int> customDataSentTo = new HashSet<int>();

        // Cauldrons that spawned before their connection got custom data, keyed by ClientId.
        private static readonly Dictionary<int, List<PendingCauldron>> pending =
            new Dictionary<int, List<PendingCauldron>>();

        private sealed class PendingCauldron
        {
            public Cauldron Station;
            public NetworkConnection Connection;
        }

        public static int PendingCount
        {
            get
            {
                int n = 0;
                foreach (var kvp in pending) n += kvp.Value.Count;
                return n;
            }
        }

        /// <summary>
        /// Called from the Cauldron.OnSpawnServer postfix. Sends immediately if the custom
        /// definitions already went out to this connection, otherwise parks until they do.
        /// </summary>
        public static void RegisterCauldron(Cauldron station, NetworkConnection connection)
        {
            if (station == null || connection == null) return;

            if (customDataSentTo.Contains(connection.ClientId))
            {
                SendCauldronSlots(station, connection);
                return;
            }

            if (!pending.TryGetValue(connection.ClientId, out var list))
            {
                list = new List<PendingCauldron>();
                pending[connection.ClientId] = list;
            }

            foreach (var existing in list)
                if (existing.Station == station) return;

            list.Add(new PendingCauldron { Station = station, Connection = connection });
            Utility.Log($"[JOINSYNC] parked cauldron '{station.GUID}' for client {connection.ClientId} ({PendingCount} pending).");
        }

        /// <summary>
        /// Called at the very end of the ProductManager.OnSpawnServer postfix, once every
        /// [NET-JSON] payload for this connection has been written.
        /// </summary>
        public static void OnCustomDataSent(NetworkConnection connection)
        {
            if (connection == null) return;

            customDataSentTo.Add(connection.ClientId);

            if (!pending.TryGetValue(connection.ClientId, out var list)) return;
            pending.Remove(connection.ClientId);

            foreach (var entry in list)
            {
                if (entry.Station == null || entry.Connection == null) continue;
                SendCauldronSlots(entry.Station, entry.Connection);
            }

            Utility.Log($"[JOINSYNC] flushed {list.Count} cauldron(s) to client {connection.ClientId}.");
        }

        /// <summary>
        /// Replicates a cauldron's slot contents to one joining client.
        ///
        /// Cauldron implements IItemSlotOwner and has the full SetStoredInstance /
        /// SetSlotLocked RPC machinery, but its OnSpawnServer never calls
        /// SendItemSlotDataToClient — it only sends the cook operation and the config.
        /// That is a vanilla bug: a client that joins mid-session sees an empty cauldron
        /// even when the host has coca leaves and gasoline loaded. This mirrors what
        /// IItemSlotOwner's own send loop does.
        /// </summary>
        private static void SendCauldronSlots(Cauldron station, NetworkConnection connection)
        {
            try
            {
                if (station.ItemSlots == null) return;

                int sent = 0;
                for (int i = 0; i < station.ItemSlots.Count; i++)
                {
                    var slot = station.ItemSlots[i];
                    if (slot == null) continue;

                    if (slot.ItemInstance != null)
                    {
                        station.SetStoredInstance(connection, i, slot.ItemInstance);
                        sent++;
                    }

                    if (slot.IsLocked && slot.ActiveLock != null)
                    {
                        station.SetSlotLocked(connection, i, true,
                            slot.ActiveLock.LockOwner, slot.ActiveLock.LockReason);
                    }
                }

                if (sent > 0)
                    Utility.Log($"[JOINSYNC] cauldron '{station.GUID}' sent {sent} slot(s) to client {connection.ClientId}.");
            }
            catch (Exception e)
            {
                Utility.Error($"[JOINSYNC] cauldron slot send failed for client {connection.ClientId}.");
                Utility.PrintException(e);
            }
        }

        public static void ClearAll()
        {
            customDataSentTo.Clear();
            pending.Clear();
        }
    }
}
