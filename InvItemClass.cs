using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarkwoodCustomizer;

public class InvItemClassPatch
{
    public static List<string> LogItem = [];
    public static string LogStats = "";

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
            if (!LogItem.Contains(type))
            {
                LogItem.Add(type);
                LogStats += $"\n----------------------------------------\n[ITEM] ID [{type}] Stats:\n";
                LogStats += $"{type}.iconType = {__instance.baseClass.iconType}\n";
                LogStats += $"{type}.hasAmmo = {__instance.baseClass.hasAmmo}\n";
                LogStats += $"{type}.canBeReloaded = {__instance.baseClass.canBeReloaded}\n";
                LogStats += $"{type}.ammoReloadType = {__instance.baseClass.ammoReloadType}\n";
                LogStats += $"{type}.ammoType = {__instance.baseClass.ammoType}\n";
                LogStats += $"{type}.hasDurability = {__instance.baseClass.hasDurability}\n";
                LogStats += $"{type}.maxDurability = {__instance.baseClass.maxDurability}\n";
                LogStats += $"{type}.ignoreDurabilityInValue = {__instance.baseClass.ignoreDurabilityInValue}\n";
                LogStats += $"{type}.repairable = {__instance.baseClass.repairable}\n";
                LogStats += $"{type}.repairrequirements = {__instance.baseClass.gameObject.GetComponent<RepairRequirements>()?.requirements?.Count}\n";
                LogStats += $"{type}.flamethrowerdrag = {((GameObject)__instance.baseClass.item)?.GetComponent<Rigidbody>()?.drag}\n";
                LogStats += $"{type}.flamethrowercontactDamage = {((GameObject)__instance.baseClass.item)?.GetComponent<Flame>()?.contactDamage}\n";
                LogStats += $"{type}.damage = {__instance.baseClass.damage}\n";
                LogStats += $"{type}.clipSize = {__instance.baseClass.clipSize}\n";
                LogStats += $"{type}.value = {__instance.baseClass.value}\n";
                LogStats += $"{type}.maxAmount = {__instance.baseClass.maxAmount}\n";
                LogStats += $"{type}.stackable = {__instance.baseClass.stackable}\n----------------------------------------\n";
            }
            string logPath = Path.Combine(Plugin.ConfigPath, "ItemLog.log");
            if (!File.Exists(logPath) || File.ReadAllText(logPath) != LogStats)
            {
                File.WriteAllText(logPath, LogStats);
            }
        }
        if (!Plugin.ItemsModification.Value) return;
        if (Plugin.UseGlobalStackSize.Value)
        {
            __instance.baseClass.maxAmount = Plugin.StackResize.Value;
        }

        if (Plugin.CustomItemsUseDefaults.Value) SetItemValues(__instance, Plugin.DefaultCustomItems);
        SetItemValues(__instance, Plugin.CustomItems);
    }


    private static void SetItemValues(InvItemClass CurrentItem, JObject CustomItems)
    {
        if (CustomItems[CurrentItem.type] != null)
        {
            var data = (JObject)CustomItems[CurrentItem.type];

            if (data.ContainsKey("iconType")) CurrentItem.baseClass.iconType = data["iconType"]?.Value<string>() ?? CurrentItem.baseClass.iconType;
            if (data.ContainsKey("fireMode"))
            {
                switch (data["fireMode"].Value<string>().ToLower())
                {
                    case "semi":
                        CurrentItem.baseClass.fireMode = InvItem.FireMode.semiAuto;
                        break;
                    case "burst":
                        CurrentItem.baseClass.fireMode = InvItem.FireMode.burst;
                        break;
                    case "fullauto":
                        CurrentItem.baseClass.fireMode = InvItem.FireMode.fullAuto;
                        break;
                    case "auto":
                        CurrentItem.baseClass.fireMode = InvItem.FireMode.fullAuto;
                        break;
                    case "single":
                        CurrentItem.baseClass.fireMode = InvItem.FireMode.oneShot;
                        break;
                    default:
                        CurrentItem.baseClass.fireMode = InvItem.FireMode.oneShot;
                        break;
                }
            }
            if (data.ContainsKey("hasAmmo")) CurrentItem.baseClass.hasAmmo = data["hasAmmo"]?.Value<bool>() ?? CurrentItem.baseClass.hasAmmo;
            if (data.ContainsKey("canBeReloaded")) CurrentItem.baseClass.canBeReloaded = data["canBeReloaded"]?.Value<bool>() ?? CurrentItem.baseClass.canBeReloaded;
            if (data.ContainsKey("ammoReloadType"))
            {
                if (data["ammoReloadType"].Value<string>() == "single") CurrentItem.baseClass.ammoReloadType = InvItem.AmmoReloadType.magazine;
                else CurrentItem.baseClass.ammoReloadType = InvItem.AmmoReloadType.magazine;
            }
            if (data.ContainsKey("ammoType")) CurrentItem.baseClass.ammoType = data["ammoType"]?.Value<string>() ?? CurrentItem.baseClass.ammoType;
            if (data.ContainsKey("hasDurability")) CurrentItem.baseClass.hasDurability = data["hasDurability"]?.Value<bool>() ?? CurrentItem.baseClass.hasDurability;
            if (data.ContainsKey("maxDurability")) CurrentItem.baseClass.maxDurability = data["maxDurability"]?.Value<int>() ?? CurrentItem.baseClass.maxDurability;
            if (data.ContainsKey("ignoreDurabilityInValue")) CurrentItem.baseClass.ignoreDurabilityInValue = data["ignoreDurabilityInValue"]?.Value<bool>() ?? false;
            if (data.ContainsKey("repairable")) CurrentItem.baseClass.repairable = data["repairable"]?.Value<bool>() ?? false;
            if (data.ContainsKey("requirements"))
            {
                var requirements = data["requirements"].Value<JObject>();
                if (requirements != null)
                {
                    var list = new List<CraftingRequirement>();
                    foreach (var requirement in requirements.Properties())
                    {
                        var item = ItemsDatabase.Instance.getItem(requirement.Name, true);
                        list.Add(new CraftingRequirement
                        {
                            item = item,
                            durabilityAmount = item.hasDurability ? (float)requirement.Value : 1,
                            amount = item.hasDurability ? 1 : (int)requirement.Value
                        });
                    }
                    CurrentItem.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = list;
                }
            }
            if (data.ContainsKey("flamethrowerdrag")) ((GameObject)CurrentItem.baseClass.item).GetComponent<Rigidbody>().drag = data["flamethrowerdrag"]?.Value<float>() ?? ((GameObject)CurrentItem.baseClass.item).GetComponent<Rigidbody>().drag;
            if (data.ContainsKey("flamethrowercontactDamage")) ((GameObject)CurrentItem.baseClass.item).GetComponent<Flame>().contactDamage = data["flamethrowercontactDamage"]?.Value<int>() ?? ((GameObject)CurrentItem.baseClass.item).GetComponent<Flame>().contactDamage;
            if (data.ContainsKey("damage")) CurrentItem.baseClass.damage = data["damage"]?.Value<int>() ?? CurrentItem.baseClass.damage;
            if (data.ContainsKey("clipSize")) CurrentItem.baseClass.clipSize = data["clipSize"]?.Value<int>() ?? CurrentItem.baseClass.clipSize;
            if (data.ContainsKey("value")) CurrentItem.baseClass.value = data["value"]?.Value<int>() ?? CurrentItem.baseClass.value;
            if (data.ContainsKey("maxAmount")) CurrentItem.baseClass.maxAmount = data["maxAmount"]?.Value<int>() ?? CurrentItem.baseClass.maxAmount;
            if (data.ContainsKey("stackable")) CurrentItem.baseClass.stackable = data["stackable"]?.Value<bool>() ?? CurrentItem.baseClass.stackable;
            if (data.ContainsKey("clipSize")) CurrentItem.ammo = CurrentItem.baseClass.clipSize;
        }
    }
}