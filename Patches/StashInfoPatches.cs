using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnicornsCustomSeeds.Managers;

#if IL2CPP
using Il2CppScheduleOne.UI;
#elif MONO
using ScheduleOne.UI;
#endif

namespace UnicornsCustomSeeds.Patches
{
    /// <summary>
    /// Every StorageMenu.Open overload. Selected by name rather than by signature: the
    /// callback parameter is System.Action on MONO but Il2CppSystem.Action on IL2CPP, so a
    /// typed [HarmonyPatch] can't name the same overload in both builds. Refresh runs
    /// once per overload in the call chain, which is harmless. It also hides the text when
    /// a non-stash container opens, since StorageMenu is shared by every container.
    /// </summary>
    [HarmonyPatch]
    public class StorageMenu_Open_Patch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            return AccessTools.GetDeclaredMethods(typeof(StorageMenu)).Where(m => m.Name == "Open");
        }

        static void Postfix()
        {
            StashInfoUI.Refresh();
        }
    }

    [HarmonyPatch(typeof(StorageMenu), "CloseMenu")]
    public class StorageMenu_CloseMenu_Patch
    {
        static void Postfix()
        {
            StashInfoUI.Hide();
        }
    }
}
