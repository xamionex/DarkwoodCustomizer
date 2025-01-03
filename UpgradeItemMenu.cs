using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

public static class UpgradeItemMenuPatch
{
  public static Vector2 VanillaValue = new(0, 0);
  public static Vector2 CraftingPos = new(0, 0);

  [HarmonyPatch(typeof(UpgradeItemMenu), nameof(UpgradeItemMenu.open))]
  [HarmonyPrefix]
  public static void UpgradeMenuOpened(UpgradeItemMenu __instance, InvItemClass invItemClass)
  {
    if (!__instance.gameObject.TryGetComponent<PositionMe>(out var positionMe)) return;
    if (VanillaValue is { x: 0, y: 0 })
    {
      VanillaValue.x = positionMe.offset.x;
      VanillaValue.y = positionMe.offset.y;
    }
    if (Plugin.WorkbenchInventoryModification.Value)
    {
      var extraSlots = Plugin.CraftingRightSlots.Value - 5f;
      var xPosition = 119f + 31f * extraSlots;
      positionMe.offset = new Vector2(CraftingPos.x + xPosition, CraftingPos.y);
    }
    else
    {
      positionMe.offset = new Vector2(VanillaValue.x, VanillaValue.y);
    }
  }
}