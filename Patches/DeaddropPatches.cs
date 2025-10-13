using MelonLoader;
using HarmonyLib;
using Il2CppFishNet.Connection;
using Il2CppScheduleOne.Product;
using System;
using Il2CppScheduleOne.Economy;
using Il2CppScheduleOne.ItemFramework;

namespace UnicornsCustomSeeds.Patches
{
    [HarmonyPatch(typeof(SupplierStash), nameof(SupplierStash.UpdateDeadDrop))]
    public static class Patch_SupplierStash_UpdateDeadDrop
    {
        public static void Postfix(SupplierStash __instance)
        {
            if (__instance != null) {

                var slots = __instance.Storage.ItemSlots;

                foreach( ItemSlot slot in slots)
                {
                    if( slot != null && slot.ItemInstance != null)
                    {
                        Utility.Log($"Slot Item: {slot.ItemInstance.Name}");
                    }
                }
            }
        }
    }
}
