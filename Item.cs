using HarmonyLib;
using DarkwoodCustomizer;

public static class ItemPatch
{
    [HarmonyPatch(typeof(Item), nameof(Item.disarm))]
    [HarmonyPrefix]
    public static void BearTrapDisarm(Item __instance)
    {
        if (Plugin.BearTrapRecovery.Value)
        {
            var bearTrap = ItemsDatabase.Instance.getItem("beartrap", true);
            if (bearTrap != null)
            {
                __instance.invItem = bearTrap;
                __instance.invItemAmount = 1;
            }
        }
    }
}