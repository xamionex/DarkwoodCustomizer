using HarmonyLib;
using DarkwoodCustomizer;
using System.Collections.Generic;

public class InvItemClassPatch
{
    public static bool RefreshLantern = true;
    public static Dictionary<string, int> ItemStackSizes = new Dictionary<string, int>();
    public static bool ItemStackSizesChanged = true;

    [HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
    [HarmonyPostfix]
    public static void ItemPatch(InvItemClass __instance)
    {
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
        if (Plugin.LogItems.Value)
        {
            if (!ItemStackSizes.ContainsKey(__instance.baseClass.name))
            {
                ItemStackSizes.Add(__instance.baseClass.name, __instance.baseClass.maxAmount);
                ItemStackSizesChanged = true;
            }
            if (ItemStackSizesChanged)
            {
                Plugin.LogDivider();
                Plugin.Log.LogInfo($"Since the logging option was enabled I have seen Items:");
                Plugin.LogDivider();
                foreach (var item in ItemStackSizes)
                {
                    Plugin.Log.LogInfo($"{item.Key}: {item.Value}");
                }
                Plugin.LogDivider();
                ItemStackSizesChanged = false;
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