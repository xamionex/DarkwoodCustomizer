using HarmonyLib;

namespace DarkwoodCustomizer;

public static class ItemPatch
{
  [HarmonyPatch(typeof(Item), nameof(Item.disarm))]
  [HarmonyPrefix]
  public static void PickingUpGroundItem(Item __instance)
  {
    // Since this includes mushrooms and more dont exit if it isnt beartrap, for future code if needed
    if (!Plugin.ItemsModification.Value) return;
    if (Plugin.BearTrapRecovery.Value && __instance.name == "bearTrap")
    {
      __instance.invItemAmount = 3;
      if (!Plugin.BearTrapRecoverySwitch.Value)
      {
        __instance.invItem = ItemsDatabase.Instance.getItem("beartrap", true);
        __instance.invItemAmount = 1;
      }
    }
    if (Plugin.ChainTrapRecovery.Value && __instance.name == "chainTrap")
    {
      __instance.invItemAmount = 2;
      if (!Plugin.ChainTrapRecoverySwitch.Value)
      {
        __instance.invItem = ItemsDatabase.Instance.getItem("chaintrap", true);
        __instance.invItemAmount = 1;
      }
    }
  }
}