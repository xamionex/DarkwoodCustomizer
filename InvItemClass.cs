using HarmonyLib;
using DarkwoodCustomizer;
using System.Collections.Generic;

public class InvItemClassPatch
{
    public static bool RefreshLantern = true;
    public static List<string> ItemStackSizes = new List<string>();

    [HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
    [HarmonyPostfix]
    public static void ItemPatch(InvItemClass __instance)
    {
        if (Plugin.LogItems.Value)
        {
            if (!ItemStackSizes.Contains(__instance.baseClass.name))
            {
                ItemStackSizes.Add(__instance.baseClass.name);
                Plugin.Log.LogInfo($"[ITEM] {__instance.baseClass.name}: {__instance.baseClass.maxAmount}");
            }
        }
        if (Plugin.ChangeStacks.Value)
        {
            if (Plugin.UseGlobalStackSize.Value)
            {
                __instance.baseClass.maxAmount = Plugin.StackResize.Value;
            }
            if (Plugin.CustomStacks.TryGetValue(__instance.baseClass.name, out int value))
            {
                __instance.baseClass.maxAmount = value;
            }
        }

        // add recipe for repairing lantern
        if (__instance.type == "lantern" && Plugin.RepairLantern.Value)
        {
            if (!__instance.baseClass.repairable && RefreshLantern)
            {
                var repairItem = ItemsDatabase.Instance.getItem(Plugin.LanternRepairConfig.Value.ToString(), true);
                var Requirements = new List<CraftingRequirement>
                {
                    new CraftingRequirement
                    {
                        item = repairItem,
                        durabilityAmount = repairItem.hasDurability ? Plugin.LanternDurabilityRepairConfig.Value : 0,
                        amount = repairItem.hasDurability ? 0 : Plugin.LanternAmountRepairConfig.Value
                    }
                };

                __instance.baseClass.repairable = true;
                __instance.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = Requirements;
                __instance.durability = __instance.baseClass.maxDurability;
                RefreshLantern = false;
            }
        }
    }
}