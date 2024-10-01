using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

public static class ItemPopupPatch
{
    [HarmonyPatch(typeof(ItemPopup), nameof(ItemPopup.show), [typeof(string), typeof(Vector2), typeof(string)])]
    [HarmonyPrefix]
    public static void UpgradeMenu(ItemPopup __instance, string _name, Vector2 _pos, string descText)
    {
        // TODO: Fix upgrade menu
    }
}