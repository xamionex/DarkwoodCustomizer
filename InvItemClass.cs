using BepInEx;
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
        var typeRotten = __instance.baseClass?.rottenItem ?? null;
        var icon = __instance.baseClass?.iconType ?? __instance.baseClass.name;
        var CustomItems = Plugin.CustomItems;
        if (!CustomItems.ContainsKey(type))
        {
            CustomItems[type] = new JObject
            {
                { "iconType", icon },
                { "maxAmount", __instance.baseClass?.maxAmount ?? 0 },
                { "stackable", __instance.baseClass?.stackable ?? false }
            };
            Plugin.SaveItems = true;
        }
        else
        {
            CustomItems[type]["iconType"] = CustomItems[type]["iconType"] ?? icon;
            CustomItems[type]["maxAmount"] = CustomItems[type]["maxAmount"] ?? icon;
            CustomItems[type]["stackable"] = CustomItems[type]["stackable"] ?? icon;
            Plugin.SaveItems = true;
        }
        if (typeRotten != null && CustomItems.ContainsKey(type))
        {
            if (!((JObject)CustomItems[type]).ContainsKey("rottenItem"))
            {
                CustomItems[type]["rottenItem"] = typeRotten.type;
                Plugin.SaveItems = true;
            }
            if (!((JObject)CustomItems[type]).ContainsKey("rottenItemMaxAmount"))
            {
                CustomItems[type]["rottenItemMaxAmount"] = typeRotten.maxAmount;
                Plugin.SaveItems = true;
            }
            if (!((JObject)CustomItems[type]).ContainsKey("rottenItemStackable"))
            {
                CustomItems[type]["rottenItemStackable"] = typeRotten.stackable;
                Plugin.SaveItems = true;
            }
        }
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
            LogStats += $"{type}.stackable = {__instance.baseClass.stackable}\n";
            LogStats += $"{type}.expValue = {__instance.baseClass.expValue}\n";
            LogStats += $"{type}.isExpItem = {__instance.baseClass.isExpItem}\n";
            LogStats += "----------------------------------------\n";
        }
        string logPath = Path.Combine(Paths.ConfigPath, "ItemLog.log");
        if (!File.Exists(logPath) || File.ReadAllText(logPath) != LogStats)
        {
            File.WriteAllText(logPath, LogStats);
        }
        if (!Plugin.ItemsModification.Value) return;
        if (Plugin.CustomItemsUseDefaults.Value) SetItemValues(__instance, (JObject)Plugin.DefaultCustomItems[__instance.type]);
        SetItemValues(__instance, (JObject)Plugin.CustomItems[__instance.type]);
        if (Plugin.UseGlobalStackSize.Value)
        {
            __instance.baseClass.maxAmount = Plugin.StackResize.Value;
        }
    }


    private static void SetItemValues(InvItemClass CurrentItem, JObject data)
    {
        if (data != null)
        {
            if (Plugin.LogItems.Value) Plugin.Log.LogInfo($"Changing Item: {CurrentItem.type}");
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
            if (bool.TryParse(data["hasAmmo"]?.Value<string>(), out bool hasAmmo)) CurrentItem.baseClass.hasAmmo = hasAmmo;
            if (bool.TryParse(data["canBeReloaded"]?.Value<string>(), out bool canBeReloaded)) CurrentItem.baseClass.canBeReloaded = canBeReloaded;
            if (data.ContainsKey("ammoReloadType"))
            {
                if (data["ammoReloadType"].Value<string>() == "single") CurrentItem.baseClass.ammoReloadType = InvItem.AmmoReloadType.magazine;
                else CurrentItem.baseClass.ammoReloadType = InvItem.AmmoReloadType.magazine;
            }
            if (data.ContainsKey("ammoType")) CurrentItem.baseClass.ammoType = data["ammoType"]?.Value<string>() ?? CurrentItem.baseClass.ammoType;
            if (bool.TryParse(data["hasDurability"]?.Value<string>(), out bool hasDurability)) CurrentItem.baseClass.hasDurability = hasDurability;
            if (int.TryParse(data["maxDurability"]?.Value<string>(), out int maxDurability)) CurrentItem.baseClass.maxDurability = maxDurability;
            if (bool.TryParse(data["ignoreDurabilityInValue"]?.Value<string>(), out bool ignoreDurabilityInValue)) CurrentItem.baseClass.ignoreDurabilityInValue = ignoreDurabilityInValue;
            if (bool.TryParse(data["repairable"]?.Value<string>(), out bool repairable)) CurrentItem.baseClass.repairable = repairable;
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
            if (float.TryParse(data["flamethrowerdrag"]?.Value<string>(), out float drag)) ((GameObject)CurrentItem.baseClass.item).GetComponent<Rigidbody>().drag = drag;
            if (int.TryParse(data["flamethrowercontactDamage"]?.Value<string>(), out int contactDamage)) ((GameObject)CurrentItem.baseClass.item).GetComponent<Flame>().contactDamage = contactDamage;
            if (int.TryParse(data["damage"]?.Value<string>(), out int damage)) CurrentItem.baseClass.damage = damage;
            if (int.TryParse(data["clipSize"]?.Value<string>(), out int clipSize)) CurrentItem.baseClass.clipSize = clipSize;
            if (int.TryParse(data["value"]?.Value<string>(), out int value)) CurrentItem.baseClass.value = value;
            if (int.TryParse(data["maxAmount"]?.Value<string>(), out int maxAmount)) CurrentItem.baseClass.maxAmount = maxAmount;
            if (bool.TryParse(data["stackable"]?.Value<string>(), out bool stackable)) CurrentItem.baseClass.stackable = stackable;
            if (int.TryParse(data["clipSize"]?.Value<string>(), out int clipSizeParsed)) CurrentItem.ammo = clipSizeParsed;
            if (int.TryParse(data["ExpValue"]?.Value<string>(), out int expValue)) CurrentItem.baseClass.expValue = expValue;
            if (bool.TryParse(data["IsExpItem"]?.Value<string>(), out bool isExpItem)) CurrentItem.baseClass.isExpItem = isExpItem;
            if (data.ContainsKey("rottenItem"))
            {
                InvItem RottenItem = ItemsDatabase.Instance.getItem(data["rottenItem"].Value<string>(), true);
                if (RottenItem != null) CurrentItem.baseClass.rottenItem = RottenItem;
            }
            if (int.TryParse(data["rottenItemMaxAmount"]?.Value<string>(), out int rottenItemMaxAmount)) CurrentItem.baseClass.rottenItem.maxAmount = rottenItemMaxAmount;
            if (bool.TryParse(data["rottenItemStackable"]?.Value<string>(), out bool rottenItemStackable)) CurrentItem.baseClass.rottenItem.stackable = rottenItemStackable;
            if (int.TryParse(data["rottenItemValue"]?.Value<string>(), out int rottenItemValue)) CurrentItem.baseClass.rottenItem.value = rottenItemValue;
            if (int.TryParse(data["rottenItemExpValue"]?.Value<string>(), out int rottenItemExpValue)) CurrentItem.baseClass.rottenItem.expValue = rottenItemExpValue;
            if (bool.TryParse(data["rottenItemIsExpItem"]?.Value<string>(), out bool rottenItemIsExpItem)) CurrentItem.baseClass.rottenItem.isExpItem = rottenItemIsExpItem;
        }
    }
}