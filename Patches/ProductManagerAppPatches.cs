using Il2CppFishNet;
using Il2CppFishNet.Connection;
using HarmonyLib;
using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnicornsCustomSeeds.Managers;
using UnicornsCustomSeeds.SeedQuests;
using Il2CppScheduleOne.UI.Phone.ProductManagerApp;
using Il2CppScheduleOne.Product;
using UnityEngine;
using UnityEngine.UI;

namespace UnicornsCustomSeeds.Patches
{
    public class ProductManagerAppPatches
    {
        [HarmonyPatch(typeof(ProductManagerApp))]
        public static class ProductManagerApp_Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ProductManagerApp.Start))]
            public static bool StartPatch(ProductManagerApp __instance)
            {

                if (__instance != null && __instance.EntryPrefab != null && __instance.EntryPrefab.transform.Find("SeedIndicator") == null)
                {
                    var parent = new GameObject("Temp");
                    parent.SetActive(false);
                    var newPrefab = UnityEngine.Object.Instantiate<GameObject>(__instance.EntryPrefab, parent.transform);
                    ProductEntry entry = __instance.EntryPrefab.GetComponent<ProductEntry>();
                    var favouriteButton = UnityEngine.Object.Instantiate<GameObject>(entry.FavouriteButton.gameObject, newPrefab.transform);
                    Button temp = favouriteButton.GetComponent<Button>();
                    GameObject.Destroy(temp);
                    favouriteButton.name = "SeedIndicator";
                    RectTransform indicatorRect = favouriteButton.GetComponent<RectTransform>();
                    if (indicatorRect != null)
                    {
                        indicatorRect.anchoredPosition = new Vector2(15, -15);
                        indicatorRect.anchorMax = new Vector2(0, 1);
                        indicatorRect.anchorMin = new Vector2(0, 1);
                    }
                    try { 
                        Utility.Log($"Seed icon is loaded? {SeedVisualsManager.seedIcon != null}");
                        Image labelImage = favouriteButton.transform.GetChild(0).GetComponent<Image>();
                        if (labelImage != null && SeedVisualsManager.seedIcon != null)
                        {
                            labelImage.sprite = SeedVisualsManager.seedIcon;
                            labelImage.color = Color.white;
                        }
                    } catch (Exception e)
                    {
                        Utility.PrintException(e);
                    }


                    __instance.EntryPrefab = newPrefab;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(ProductEntry), nameof(ProductEntry.Initialize))]
        public static class ProductEntry_Initialize_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(ProductEntry __instance, ProductDefinition definition)
            {
                if (__instance != null && definition != null)
                {
                    Transform seedIndicator = __instance.transform.Find("SeedIndicator");
                    if (seedIndicator != null)
                    {
                        if (CustomSeedsManager.DiscoveredSeeds.ContainsKey(definition.ID))
                        {
                            seedIndicator.gameObject.SetActive(true);
                        }
                        else
                        {
                            seedIndicator.gameObject.SetActive(false);
                        }
                    }
                }
            }
        }
    }
}
