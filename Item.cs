using HarmonyLib;

namespace DarkwoodCustomizer;

public static class ItemPatch
{
  [HarmonyPatch(typeof(Item), nameof(Item.disarm))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  public static void PickingUpGroundItem(Item __instance)
  {
    // Since this includes mushrooms and more dont exit if it isnt beartrap, for future code if needed
    if (!Plugin.ItemsModification.Value) return;
    if (Plugin.BearTrapRecovery.Value && __instance.name == "bearTrap")
    {
      __instance.invItemAmount = 3;
      if (!Plugin.BearTrapRecoverySwitch.Value)
      {
        __instance.invItem = ItemsDatabase.Instance.getItem("beartrap");
        __instance.invItemAmount = 1;
      }
    }
    if (Plugin.ChainTrapRecovery.Value && __instance.name == "chainTrap")
    {
      __instance.invItemAmount = 2;
      if (!Plugin.ChainTrapRecoverySwitch.Value)
      {
        __instance.invItem = ItemsDatabase.Instance.getItem("chaintrap");
        __instance.invItemAmount = 1;
      }
    }
  }
}