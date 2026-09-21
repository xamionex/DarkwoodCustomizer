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
    if (!Plugin.DefensesModification.Value) return;

    // Match on the Trigger's own isBearTrap/isChainTrap flags (via the shared
    // DefensesPatch.IsTrapType helper, which also excludes mushrooms/other world items that reuse the isBearTrap flag) instead of the GameObject name.
    // Item.getRealName() (used for the on-screen tooltip/cursor text) calls Item.refreshName() every time it runs, which - once the trap is a dropped item - renames the GameObject to the loot's display name (e.g. "Scrap Metal").
    // That happens automatically the moment the player looks at a triggered trap, so a name == "bearTrap"/"chainTrap" check silently stops matching for that trap from then on, regardless of anything else.
    // The Trigger flags never change.
    var trigger = __instance.GetComponent<Trigger>();
    if (!trigger || !DefensesPatch.IsTrapType(trigger)) return;

    if (Plugin.BearTrapRecovery.Value && trigger.isBearTrap)
    {
      __instance.invItemAmount = 3;
      if (!Plugin.BearTrapRecoverySwitch.Value)
      {
        __instance.invItem = ItemsDatabase.Instance.getItem("beartrap");
        __instance.invItemAmount = 1;
      }
    }
    if (Plugin.ChainTrapRecovery.Value && trigger.isChainTrap)
    {
      __instance.invItemAmount = 2;
      if (!Plugin.ChainTrapRecoverySwitch.Value)
      {
        // The chain trap's item type is "chainTrap" (camelCase), unlike the all lowercase "beartrap".
        // ItemsDatabase lookups are case sensitive, so a lowercase "chaintrap" resolves to null and the trap would silently never be handed to the player.
        __instance.invItem = ItemsDatabase.Instance.getItem("chainTrap");
        __instance.invItemAmount = 1;
      }
    }
  }

  // Picking up a trap that's already sprung but hasn't recharged yet goes through Item.getDroppedItem() instead of Item.disarm() (see Item.activate(): isDroppedItem routes there directly).
  // getDroppedItem() hands the player whatever sits in the trap's own Inventory slot 0 - vanilla's native "1x scrap metal" loot - without ever calling disarm(), so the patch above never gets a chance to apply the Recover Items settings.
  // This mirrors that logic onto the slot content instead, so a freshly-triggered, not-yet-recharged trap respects the same settings vanilla-style pickup would after a recharge.
  [HarmonyPatch(typeof(Item), nameof(Item.getDroppedItem))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  public static bool PickingUpFreshlyTriggeredTrap(Item __instance)
  {
    if (!Plugin.DefensesModification.Value) return true;

    var trigger = __instance.GetComponent<Trigger>();
    if (!trigger || !DefensesPatch.IsTrapType(trigger)) return true;

    var inventory = __instance.GetComponent<Inventory>();
    if (!inventory || inventory.slots.Count == 0) return true;
    var slot = inventory.slots[0];
    if (InvItemClass.isNull(slot.invItem)) return true;

    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] getDroppedItem() prefix reached for trap '{__instance.name}' (isBearTrap={trigger.isBearTrap}, isChainTrap={trigger.isChainTrap}, native loot slot has '{slot.invItem.type}' x{slot.invItem.amount})");

    // Giving back a full pristine trap (Recovery Switch off) swaps the slot for a different item type entirely.
    // Testing showed that specific case leaves the trap stuck on the ground even though the trap item was already added to the player - Item.getDroppedItem()'s own transfer call reports failure despite the transfer having actually happened - while bumping the amount of the loot that's already there (Recovery Switch on) works fine.
    // So for the "give a pristine trap" case we skip getDroppedItem() entirely and do the transfer and cleanup ourselves with Player.Inventory.addItemTypeToPlayer(), the same call Item.disarm() itself already uses successfully for the armed-trap-disarm case.
    if (Plugin.BearTrapRecovery.Value && trigger.isBearTrap && !Plugin.BearTrapRecoverySwitch.Value)
    {
      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogInfo($"[Defenses] Picking up freshly-triggered BearTrap '{__instance.name}': giving a pristine trap back instead of the native loot");
      return !GivePristineTrapAndRemove(__instance, "beartrap");
    }
    if (Plugin.ChainTrapRecovery.Value && trigger.isChainTrap && !Plugin.ChainTrapRecoverySwitch.Value)
    {
      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogInfo($"[Defenses] Picking up freshly-triggered ChainTrap '{__instance.name}': giving a pristine trap back instead of the native loot");
      return !GivePristineTrapAndRemove(__instance, "chainTrap");
    }

    if (Plugin.BearTrapRecovery.Value && trigger.isBearTrap)
      slot.invItem.amount = 3;
    if (Plugin.ChainTrapRecovery.Value && trigger.isChainTrap)
      slot.invItem.amount = 2;

    return true;
  }

  // Returns true if the trap item was successfully added to the player (and the trap's GameObject was destroyed to match), false if there was no room - in which case nothing changed and the trap should be left alone, same as vanilla would.
  private static bool GivePristineTrapAndRemove(Item item, string trapType)
  {
    var added = Player.Instance.Inventory.addItemTypeToPlayer(trapType, 1);
    if (InvItemClass.isNull(added))
    {
      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogInfo($"[Defenses] addItemTypeToPlayer('{trapType}') returned nothing - assuming no free inventory slot, leaving the trap in place");
      return false;
    }
    Player.Instance.refreshRecipes();
    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] addItemTypeToPlayer('{trapType}') succeeded, destroying trap GameObject '{item.name}' (instance id {item.GetInstanceID()})");
    UnityEngine.Object.Destroy(item.gameObject);
    Player.Instance.deselectObject();
    return true;
  }

  // The name shown next to the cursor for a triggered trap comes from getRealName(), which builds it out of the loot sitting in the trap's own inventory.
  // That loot is always the native single scrap metal, so without this the tooltip keeps promising scrap metal even when the Recover Items settings hand over a whole trap or a bigger pile of scrap instead.
  [HarmonyPatch(typeof(Item), nameof(Item.getRealName))]
  [HarmonyPostfix]
  // ReSharper disable InconsistentNaming
  public static void TrapRealName(Item __instance, ref string __result)
  // ReSharper restore InconsistentNaming
  {
    if (!Plugin.DefensesModification.Value) return;

    var trigger = __instance.GetComponent<Trigger>();
    if (!trigger || !DefensesPatch.IsTrapType(trigger)) return;

    var reward = DefensesPatch.GetTrapRewardName(trigger);
    if (reward != null) __result = reward;
  }
}
