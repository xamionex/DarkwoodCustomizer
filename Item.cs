using HarmonyLib;

namespace DarkwoodCustomizer;

public static class ItemPatch
{
  [HarmonyPatch(typeof(Item), nameof(Item.disarm))]
  [HarmonyPrefix]
  public static void PickingUpGroundItem(Item instance)
  {
    // Since this includes mushrooms and more dont exit if it isnt beartrap, for future code if needed
    if (!Plugin.ItemsModification.Value) return;
    if (Plugin.BearTrapRecovery.Value && instance.name == "bearTrap")
    {
      instance.invItemAmount = 3;
      if (!Plugin.BearTrapRecoverySwitch.Value)
      {
        instance.invItem = ItemsDatabase.Instance.getItem("beartrap", true);
        instance.invItemAmount = 1;
      }
    }
    if (Plugin.ChainTrapRecovery.Value && instance.name == "chainTrap")
    {
      instance.invItemAmount = 2;
      if (!Plugin.ChainTrapRecoverySwitch.Value)
      {
        instance.invItem = ItemsDatabase.Instance.getItem("chaintrap", true);
        instance.invItemAmount = 1;
      }
    }
  }
}