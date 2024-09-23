using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using StackResizer;
using System.Collections.Generic;

[HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
public class InvItemClassPatch
{
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
            __instance.baseClass.repairable = true;
            __instance.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = new List<CraftingRequirement>
            {
                new CraftingRequirement
                {
                    item = Singleton<ItemsDatabase>.Instance.getItem(Plugin.LanternRepairConfig.Value.ToString(), true),
                    amount = 1
                }
            };
            __instance.durability = __instance.baseClass.maxDurability;
        }
    }
}