using System;
using System.Collections.Generic;
using UnicornsCustomSeeds.TemplateUtils;

#if IL2CPP
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.UI.Stations;
#elif MONO
using ScheduleOne;
using ScheduleOne.DevUtilities;
using ScheduleOne.ObjectScripts;
using ScheduleOne.UI.Stations;
#endif

namespace UnicornsCustomSeeds.Managers
{
    /// <summary>
    /// Client-side retry for in-progress cook operations that arrive before the definition or
    /// recipe they depend on.
    ///
    /// Unlike item slots, a cook operation carries string IDs rather than an ItemInstance and
    /// resolves them lazily. That sounds safe, but both receive handlers dereference the
    /// resolved value immediately and neither null-checks it:
    ///
    ///   ChemistryStation.RpcLogic___SetCookOperation (ChemistryStation.cs:901)
    ///       this.BoilingFlask.SetTemperature(operation.Recipe.CookTemperature);
    ///   LabOven.RpcLogic___SetCookOperation (LabOven.cs:1129)
    ///       CookableModule module = operation.Ingredient.StationItem.GetModule[CookableModule]();
    ///
    /// ChemistryCookOperation.Recipe searches ChemistryStationInterface.Instance.Recipes,
    /// which is where PseudoFactory injects custom recipes — seconds after the station spawns.
    /// OvenCookOperation.Ingredient is a Registry lookup on a custom ID that has not arrived
    /// yet. Either one returns null and the dereference throws INSIDE FishNet's reader loop,
    /// which kills the rest of the packet. Observed as a joining client that never leaves the
    /// loading screen, because the SetReplicationDone that ends loading sits behind the throw
    /// in the stream.
    ///
    /// So the patches skip the handler entirely rather than let it half-apply. Skipping is
    /// safe: every consumer guards on CurrentCookOperation != null, whereas a half-applied
    /// operation is non-null with a null Recipe and throws again on every minute pass.
    ///
    /// Same shape as DeferredSlotsManager, for operations instead of slots.
    /// </summary>
    public static class DeferredCookOpsManager
    {
        /// <summary>Set while replaying, so the patches let the re-issued call through.</summary>
        public static bool IsReplaying { get; private set; }

        private sealed class PendingChemistryOp
        {
            public ChemistryStation Station;
            public ChemistryCookOperation Operation;
            public int Attempts;
        }

        private sealed class PendingOvenOp
        {
            public LabOven Oven;
            public OvenCookOperation Operation;
            public bool PlayButtonPress;
            public int Attempts;
        }

        private static readonly List<PendingChemistryOp> pendingChemistry = new List<PendingChemistryOp>();
        private static readonly List<PendingOvenOp> pendingOven = new List<PendingOvenOp>();

        public static int PendingCount => pendingChemistry.Count + pendingOven.Count;

        // ── Resolution checks ────────────────────────────────────────────────
        //
        // Both are wrapped, because the getters they poke are themselves unguarded:
        // ChemistryCookOperation.Recipe dereferences Singleton<ChemistryStationInterface>
        // .Instance, which may not exist yet on a joining client.

        public static bool IsChemistryOpReady(ChemistryCookOperation operation)
        {
            if (operation == null) return true;
            try
            {
                if (!Singleton<ChemistryStationInterface>.InstanceExists) return false;
                return operation.Recipe != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsOvenOpReady(OvenCookOperation operation)
        {
            if (operation == null) return true;
            try
            {
                // Registry.ItemExists first, rather than going straight to
                // operation.Ingredient: same answer without poking a lazy getter whose
                // StationItem chain is what throws in the first place.
                if (string.IsNullOrEmpty(operation.IngredientID)) return true;
                if (!Registry.ItemExists(operation.IngredientID)) return false;
                return operation.Ingredient != null && operation.Ingredient.StationItem != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ── Parking ──────────────────────────────────────────────────────────

        public static void ParkChemistry(ChemistryStation station, ChemistryCookOperation operation)
        {
            if (station == null || operation == null) return;

            foreach (var existing in pendingChemistry)
            {
                if (existing.Station == station)
                {
                    existing.Operation = operation;
                    return;
                }
            }

            pendingChemistry.Add(new PendingChemistryOp { Station = station, Operation = operation });
            Utility.Log($"[COOKWAIT] parked chemistry cook '{operation.RecipeID}' on " +
                        $"'{station.name}' — recipe not injected yet ({PendingCount} pending).");
        }

        public static void ParkOven(LabOven oven, OvenCookOperation operation, bool playButtonPress)
        {
            if (oven == null || operation == null) return;

            foreach (var existing in pendingOven)
            {
                if (existing.Oven == oven)
                {
                    existing.Operation = operation;
                    return;
                }
            }

            pendingOven.Add(new PendingOvenOp
            {
                Oven = oven,
                Operation = operation,
                PlayButtonPress = playButtonPress
            });
            Utility.Log($"[COOKWAIT] parked oven cook '{operation.IngredientID}' on " +
                        $"'{oven.name}' — ingredient not registered yet ({PendingCount} pending).");
        }

        // ── Replay ───────────────────────────────────────────────────────────

        /// <summary>
        /// Called after any custom definition or recipe is rebuilt. Cheap to call repeatedly.
        /// </summary>
        public static void TryReplayAll()
        {
            for (int i = pendingChemistry.Count - 1; i >= 0; i--)
            {
                var entry = pendingChemistry[i];
                if (entry.Station == null)
                {
                    pendingChemistry.RemoveAt(i);
                    continue;
                }
                if (!IsChemistryOpReady(entry.Operation)) continue;

                pendingChemistry.RemoveAt(i);
                ReplayChemistry(entry.Station, entry.Operation);
            }

            for (int i = pendingOven.Count - 1; i >= 0; i--)
            {
                var entry = pendingOven[i];
                if (entry.Oven == null)
                {
                    pendingOven.RemoveAt(i);
                    continue;
                }
                if (!IsOvenOpReady(entry.Operation)) continue;

                pendingOven.RemoveAt(i);
                ReplayOven(entry.Oven, entry.Operation, entry.PlayButtonPress);
            }
        }

        private static void ReplayChemistry(ChemistryStation station, ChemistryCookOperation operation)
        {
            IsReplaying = true;
            try
            {
                station.RpcLogic___SetCookOperation_1024887225(null, operation);
                Utility.Log($"[COOKWAIT] resumed chemistry cook '{operation.RecipeID}' on " +
                            $"'{station.name}' ({PendingCount} still pending).");
            }
            catch (Exception e)
            {
                Utility.Error($"[COOKWAIT] replay of chemistry cook '{operation.RecipeID}' threw.");
                Utility.PrintException(e);
            }
            finally
            {
                IsReplaying = false;
            }
        }

        private static void ReplayOven(LabOven oven, OvenCookOperation operation, bool playButtonPress)
        {
            IsReplaying = true;
            try
            {
                oven.RpcLogic___SetCookOperation_2611294368(null, operation, playButtonPress);
                Utility.Log($"[COOKWAIT] resumed oven cook '{operation.IngredientID}' on " +
                            $"'{oven.name}' ({PendingCount} still pending).");
            }
            catch (Exception e)
            {
                Utility.Error($"[COOKWAIT] replay of oven cook '{operation.IngredientID}' threw.");
                Utility.PrintException(e);
            }
            finally
            {
                IsReplaying = false;
            }
        }

        // ── Failure handling ─────────────────────────────────────────────────
        //
        // Called from the Harmony finalizers when the vanilla handler threw despite the
        // readiness check passing. ChemistryStation's handler also touches BoilingFlask,
        // Burner and Alarm and dereferences Recipe a second time inside UpdateClock, so
        // predicting every null is not realistic — catching is.
        //
        // The operation is cleared before parking: consumers all guard on null, whereas a
        // half-applied operation is non-null with a null member and throws again every
        // minute pass.

        private const int MaxAttempts = 10;

        public static void HandleChemistryFailure(ChemistryStation station,
                                                 ChemistryCookOperation operation,
                                                 Exception error)
        {
            try
            {
                if (station != null) station.CurrentCookOperation = null;

                int attempts = AttemptsForChemistry(station);
                if (station == null || operation == null || attempts >= MaxAttempts)
                {
                    Utility.Error($"[COOKWAIT] chemistry cook '{operation?.RecipeID}' failed " +
                                  $"{attempts} time(s) and is being dropped; the station will " +
                                  "show no cook in progress.");
                    Utility.PrintException(error);
                    RemoveChemistry(station);
                    return;
                }

                Utility.Log($"[COOKWAIT] chemistry cook '{operation.RecipeID}' on " +
                            $"'{station.name}' threw on attempt {attempts + 1} — parked to retry.");

                // Print the exception on the first failure only. Without it there is no way
                // to see WHICH member was null, and printing it on all ten retries would
                // bury everything else.
                if (attempts == 0) Utility.PrintException(error);
                ParkChemistry(station, operation);
                BumpChemistry(station);
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        public static void HandleOvenFailure(LabOven oven, OvenCookOperation operation,
                                             bool playButtonPress, Exception error)
        {
            try
            {
                if (oven != null) oven.CurrentOperation = null;

                int attempts = AttemptsForOven(oven);
                if (oven == null || operation == null || attempts >= MaxAttempts)
                {
                    Utility.Error($"[COOKWAIT] oven cook '{operation?.IngredientID}' failed " +
                                  $"{attempts} time(s) and is being dropped; the oven will " +
                                  "show no cook in progress.");
                    Utility.PrintException(error);
                    RemoveOven(oven);
                    return;
                }

                Utility.Log($"[COOKWAIT] oven cook '{operation.IngredientID}' on " +
                            $"'{oven.name}' threw on attempt {attempts + 1} — parked to retry.");

                // Print the exception on the first failure only. Without it there is no way
                // to see WHICH member was null, and printing it on all ten retries would
                // bury everything else.
                if (attempts == 0) Utility.PrintException(error);
                ParkOven(oven, operation, playButtonPress);
                BumpOven(oven);
            }
            catch (Exception e)
            {
                Utility.PrintException(e);
            }
        }

        private static int AttemptsForChemistry(ChemistryStation station)
        {
            foreach (var e in pendingChemistry)
                if (e.Station == station) return e.Attempts;
            return 0;
        }

        private static int AttemptsForOven(LabOven oven)
        {
            foreach (var e in pendingOven)
                if (e.Oven == oven) return e.Attempts;
            return 0;
        }

        private static void BumpChemistry(ChemistryStation station)
        {
            foreach (var e in pendingChemistry)
                if (e.Station == station) { e.Attempts++; return; }
        }

        private static void BumpOven(LabOven oven)
        {
            foreach (var e in pendingOven)
                if (e.Oven == oven) { e.Attempts++; return; }
        }

        private static void RemoveChemistry(ChemistryStation station)
        {
            for (int i = pendingChemistry.Count - 1; i >= 0; i--)
                if (pendingChemistry[i].Station == station) pendingChemistry.RemoveAt(i);
        }

        private static void RemoveOven(LabOven oven)
        {
            for (int i = pendingOven.Count - 1; i >= 0; i--)
                if (pendingOven[i].Oven == oven) pendingOven.RemoveAt(i);
        }

        public static void ClearAll()
        {
            pendingChemistry.Clear();
            pendingOven.Clear();
        }
    }
}
