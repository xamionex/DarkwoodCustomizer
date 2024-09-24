using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using MonoMod.Utils;
using Newtonsoft.Json;
using StackResizer;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
public class InvItemClassPatch
{
    public static bool ConfigReloaded = true;
    public static void Postfix(InvItemClass __instance)
    {
        if (__instance.baseClass.stackable)
        {
            __instance.baseClass.maxAmount = Plugin.StackResize.Value;
        }

        if (Plugin.LogItems.Value)
        {
            Plugin.Log.LogInfo($"I see an item: {__instance.baseClass.name} with the stack size of {__instance.baseClass.maxAmount}!");
        }

        // add recipe for repairing lantern
        if (__instance.type == "lantern")
        {
            if (!__instance.baseClass.repairable && ConfigReloaded)
            {
                var RepairItem = Singleton<ItemsDatabase>.Instance.getItem(Plugin.LanternRepairConfig.Value.ToString(), true);
                List<CraftingRequirement> Requirements;
                if (RepairItem.hasDurability) {
                    Requirements = new List<CraftingRequirement>
                    {
                        new CraftingRequirement
                        {
                            item = Singleton<ItemsDatabase>.Instance.getItem(Plugin.LanternRepairConfig.Value.ToString(), true),
                            durabilityAmount = Plugin.LanternDurabilityRepairConfig.Value
                        }
                    };
                } else {
                    Requirements = new List<CraftingRequirement>
                    {
                        new CraftingRequirement
                        {
                            item = Singleton<ItemsDatabase>.Instance.getItem(Plugin.LanternRepairConfig.Value.ToString(), true),
                            amount = Plugin.LanternAmountRepairConfig.Value
                        }
                    };
                }
                __instance.baseClass.repairable = true;
                __instance.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = Requirements;
                __instance.durability = __instance.baseClass.maxDurability;
                ConfigReloaded = false;
            }
        }
    }
}