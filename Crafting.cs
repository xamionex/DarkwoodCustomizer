using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace DarkwoodCustomizer;

internal static class CraftingPatch
{
    private static readonly Dictionary<CraftingRecipes, CraftingRecipes.Recipe> PendingUpgradeRecipe = new();

    // Prevents the game from consuming items when crafting
    [HarmonyPatch(typeof(CraftingRecipes.Recipe), nameof(CraftingRecipes.Recipe.removeIngredients))]
    [HarmonyPrefix]
    private static bool Prefix_RemoveIngredients()
    {
        return !Plugin.FreeCrafting.Value;
    }

    [HarmonyPatch(typeof(CraftingRequirement), "refresh")]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void Postfix_CraftingRequirement_Refresh(CraftingRequirement __instance)
    {
        if (Plugin.FreeCrafting.Value)
        {
            // Force the requirement to always be met
            __instance.met = true;
        }
    }

    // Tracks workbench upgrade crafts so the postfix can ensure the item is consumed.
    [HarmonyPatch(typeof(CraftingRecipes), nameof(CraftingRecipes.doCraft))]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static void Prefix_DoCraft(CraftingRecipes __instance, CraftingRecipes.Recipe __0)
    {
        var invItem = __instance.GetComponent<InvItem>();
        if (invItem != null && invItem.isWorkbenchUpgrade)
        {
            PendingUpgradeRecipe[__instance] = __0;
        }
    }

    [HarmonyPatch(typeof(CraftingRecipes), nameof(CraftingRecipes.doCraft))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    private static void Postfix_DoCraft(CraftingRecipes __instance)
    {
        if (!PendingUpgradeRecipe.TryGetValue(__instance, out var recipe)) return;
        PendingUpgradeRecipe.Remove(__instance);

        var itemType = __instance.GetComponent<InvItem>()?.type;
        if (string.IsNullOrEmpty(itemType)) return;

        var amount = recipe.produceAmount;
        var remaining = amount;

        // Remove the upgrade item from player inventory, hotbar, and workbench storage.
        // This handles cases where the vanilla removeItem() failed because the item was stackable and merged into an existing stack.
        foreach (var slot in Player.Instance.Inventory.slots)
        {
            if (remaining <= 0) break;
            if (InvItemClass.isNull(slot.invItem) || slot.invItem.type != itemType) continue;
            if (slot.invItem.amount <= remaining)
            {
                remaining -= slot.invItem.amount;
                slot.removeItem();
            }
            else
            {
                slot.invItem.amount -= remaining;
                slot.invItem.refresh();
                remaining = 0;
            }
        }
        foreach (var slot in Player.Instance.Hotbar.slots.TakeWhile(slot => remaining > 0).Where(slot => !InvItemClass.isNull(slot.invItem) && slot.invItem.type == itemType))
        {
            if (slot.invItem.amount <= remaining)
            {
                remaining -= slot.invItem.amount;
                slot.removeItem();
            }
            else
            {
                slot.invItem.amount -= remaining;
                slot.invItem.refresh();
                remaining = 0;
            }
        }
        if (Player.Instance.openedItemInventory2 != null)
        {
            foreach (var slot in Player.Instance.openedItemInventory2.slots.TakeWhile(slot => remaining > 0).Where(slot => !InvItemClass.isNull(slot.invItem) && slot.invItem.type == itemType))
            {
                if (slot.invItem.amount <= remaining)
                {
                    remaining -= slot.invItem.amount;
                    slot.removeItem();
                }
                else
                {
                    slot.invItem.amount -= remaining;
                    slot.invItem.refresh();
                    remaining = 0;
                }
            }
        }

        if (remaining > 0)
        {
            Plugin.Log.LogWarning($"Could not fully remove workbench upgrade item {itemType} from player inventory. {remaining} remaining.");
        }
    }
}
