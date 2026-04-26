using System;
using System.Reflection;
using HarmonyLib;
using UnicornsCustomSeeds.TemplateUtils;
using UnityEngine;

#if IL2CPP
using Il2CppScheduleOne.ObjectScripts;
using GameConsole = Il2CppScheduleOne.Console;
using GenericCol = Il2CppSystem.Collections.Generic;
#elif MONO
using ScheduleOne.ObjectScripts;
using GameConsole = ScheduleOne.Console;
using GenericCol = System.Collections.Generic;
#endif

namespace UnicornsCustomSeeds.Patches
{
    /// <summary>
    /// Injects a "finishcooking" developer console command that instantly completes
    /// all active Cauldron and LabOven cook operations in the scene.
    ///
    /// The game's Console.AddCommand is private so we inject via a Harmony Postfix
    /// on Console.Awake, mirroring the game's own pattern for GrowPlants etc.
    /// </summary>
    [HarmonyPatch(typeof(GameConsole), "Awake")]
    public static class Patch_Console_AddFinishCookingCommand
    {
        static void Postfix(GameConsole __instance)
        {
          try
 {
           MethodInfo addCmd = typeof(GameConsole).GetMethod(
          "AddCommand",
           BindingFlags.NonPublic | BindingFlags.Instance);
         if (addCmd == null)
    {
   Utility.Error("FinishCookingCommand: Could not find Console.AddCommand via reflection.");
         return;
         }
                addCmd.Invoke(__instance, new object[] { new FinishCookingCommand() });
     Utility.Log("FinishCookingCommand: 'finishcooking' command registered.");
            }
  catch (Exception e) { Utility.PrintException(e); }
    }
    }

    public class FinishCookingCommand : GameConsole.ConsoleCommand
    {
        public override string CommandWord => "finishcooking";
        public override string CommandDescription => "Instantly completes all active Cauldron and LabOven cook operations.";
        public override string ExampleUsage => "finishcooking";

        public override void Execute(GenericCol.List<string> args)
        {
    int cauldronCount = 0;
            int ovenCount = 0;

       // ?? Cauldrons ????????????????????????????????????????????????????????????
   // RpcLogic___FinishCookOperation_2166136261 handles output placement (server-only),
        // fillable reset, and onCookEnd event. Set RemainingCookTime = 0 first so
            // the periodic OnTimePass tick doesn't try to trigger it a second time.
        foreach (Cauldron cauldron in GameObject.FindObjectsOfType<Cauldron>())
        {
            try
       {
              if (cauldron.RemainingCookTime > 0)
    {
  cauldron.RemainingCookTime = 0;
// Call the public [ObserversRpc] entry point so it passes through
         // Patch_Cauldron_FinishCookOperation before broadcasting to clients.
        cauldron.FinishCookOperation();
          cauldronCount++;
           }
      }
        catch (Exception e) { Utility.Error($"FinishCookingCommand: cauldron '{cauldron?.name}': {e.Message}"); }
        }

       // ?? Lab Ovens ?????????????????????????????????????????????????????????????
            // LabOven has no auto-harvest RPC — advancing CookProgress to completion
    // makes IsReadyForHarvest() return true so the player can collect normally.
   // UpdateOvenAppearance() and UpdateLiquid() are private; DingSound is public.
  foreach (LabOven oven in GameObject.FindObjectsOfType<LabOven>())
            {
        try
       {
         if (oven.CurrentOperation == null || oven.CurrentOperation.IsComplete()) continue;

  int remaining = oven.CurrentOperation.GetCookDuration() - oven.CurrentOperation.CookProgress;
         oven.CurrentOperation.UpdateCookProgress(remaining + 1);

         // Play ding (public field)
      oven.DingSound?.Play();

// UpdateOvenAppearance + UpdateLiquid are private — call via reflection
         var t = oven.GetType();
        t.GetMethod("UpdateOvenAppearance", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
      ?.Invoke(oven, null);
           t.GetMethod("UpdateLiquid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
         ?.Invoke(oven, null);

     ovenCount++;
         }
           catch (Exception e) { Utility.Error($"FinishCookingCommand: oven '{oven?.name}': {e.Message}"); }
  }

            GameConsole.Log($"finishcooking: completed {cauldronCount} cauldron(s) and {ovenCount} lab oven(s).", null);
        }
    }
}
