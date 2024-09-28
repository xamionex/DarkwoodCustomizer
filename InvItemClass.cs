using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace DarkwoodCustomizer;

public class InvItemClassPatch
{
    public static bool RefreshLantern = true;
    public static List<string> ItemStackSizes = [];

    [HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
    [HarmonyPostfix]
    public static void ItemPatch(InvItemClass __instance)
    {
        if (Singleton<Dreams>.Instance.dreaming || __instance.baseClass == null)
        {
            return;
        }
        var type = __instance.baseClass.type ?? __instance.type;
        if (Plugin.LogItems.Value)
        {
            if (!ItemStackSizes.Contains(type))
            {
                ItemStackSizes.Add(type);
                Plugin.Log.LogInfo($"[ITEM] {type}: Amount: {__instance.baseClass.maxAmount} Icon: {__instance.baseClass.iconType}");
            }
        }
        if (Plugin.ChangeStacks.Value)
        {
            if (Plugin.UseGlobalStackSize.Value)
            {
                __instance.baseClass.maxAmount = Plugin.StackResize.Value;
            }
            if (Plugin.CustomStacks.TryGetValue(type, out JToken value))
            {
                __instance.baseClass.maxAmount = int.Parse(value.ToString());
            }
        }

        // add recipe for flamethrower
        if (__instance.type == "weapon_flamethrower_homeMade")
        {
            __instance.baseClass.iconType = "weapon_flamethrower_military_01";
            __instance.baseClass.fireMode = InvItem.FireMode.fullAuto;
            __instance.baseClass.hasAmmo = false;
            __instance.baseClass.canBeReloaded = false;
            __instance.baseClass.hasDurability = true;
            __instance.baseClass.maxDurability = 100f;
            __instance.baseClass.ignoreDurabilityInValue = true;
            __instance.baseClass.repairable = true;
            __instance.baseClass.gameObject.AddComponent<RepairRequirements>().requirements =
                [
                    new CraftingRequirement
                    {
                        item = Singleton<ItemsDatabase>.Instance.getItem("gasBottle", true),
                        amount = 1
                    },
                    new CraftingRequirement
                    {
                        item = Singleton<ItemsDatabase>.Instance.getItem("barrelExploding", true),
                        amount = 1
                    }
                ];
            __instance.baseClass.damage = 60;
            __instance.durability = __instance.baseClass.maxDurability;
            __instance.ammo = 999;
            return;
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