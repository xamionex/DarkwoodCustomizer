using BepInEx;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class InvItemClassPatch
{
  public static List<string> LogItem = [];
  public static string LogStats = "";

  [HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
  [HarmonyPostfix]
  public static void ItemPatch(InvItemClass __instance)
  {
    if (!Plugin.ItemsModification.Value) return;
    if (Singleton<Dreams>.Instance.dreaming || __instance.baseClass == null) return;
    var type = __instance.baseClass.type ?? __instance.type;
    var typeRotten = __instance.baseClass?.rottenItem ?? null;
    var icon = __instance.baseClass?.iconType ?? __instance.baseClass.name;
    var customItems = Plugin.CustomItems;
    if (!customItems.ContainsKey(type))
    {
      customItems[type] = new JObject
            {
                { "iconType", icon },
                { "maxAmount", __instance.baseClass?.maxAmount ?? 0 },
                { "stackable", __instance.baseClass?.stackable ?? false }
            };
    }
    else
    {
      customItems[type]!["iconType"] ??= icon;
      customItems[type]!["maxAmount"] ??= __instance.baseClass.maxAmount;
      customItems[type]!["stackable"] ??= __instance.baseClass.stackable;
    }

    Plugin.SaveItems = true;
    if (typeRotten != null && customItems.ContainsKey(type))
    {
      if (!((JObject)customItems[type]).ContainsKey("rottenItem"))
      {
        customItems[type]["rottenItem"] = typeRotten.type;
        Plugin.SaveItems = true;
      }
      if (!((JObject)customItems[type]).ContainsKey("rottenItemMaxAmount"))
      {
        customItems[type]["rottenItemMaxAmount"] = typeRotten.maxAmount;
        Plugin.SaveItems = true;
      }
      if (!((JObject)customItems[type]).ContainsKey("rottenItemStackable"))
      {
        customItems[type]["rottenItemStackable"] = typeRotten.stackable;
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
    var logPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "ItemLog.log");
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
  private static void SetItemValues(InvItemClass currentItem, JObject data)
  {
    if (data != null)
    {
      if (data.ContainsKey("iconType")) currentItem.baseClass.iconType = data["iconType"]?.Value<string>() ?? currentItem.baseClass.iconType;

      // ranged weapons
      if (data.ContainsKey("fireMode"))
      {
        switch (data["fireMode"].Value<string>().ToLower())
        {
          case "semi":
            currentItem.baseClass.fireMode = InvItem.FireMode.semiAuto;
            break;
          case "burst":
            currentItem.baseClass.fireMode = InvItem.FireMode.burst;
            break;
          case "fullauto":
            currentItem.baseClass.fireMode = InvItem.FireMode.fullAuto;
            break;
          case "auto":
            currentItem.baseClass.fireMode = InvItem.FireMode.fullAuto;
            break;
          case "single":
            currentItem.baseClass.fireMode = InvItem.FireMode.oneShot;
            break;
          default:
            currentItem.baseClass.fireMode = InvItem.FireMode.oneShot;
            break;
        }
      }
      if (bool.TryParse(data["hasAmmo"]?.Value<string>(), out var hasAmmo)) currentItem.baseClass.hasAmmo = hasAmmo;
      if (bool.TryParse(data["canBeReloaded"]?.Value<string>(), out var canBeReloaded)) currentItem.baseClass.canBeReloaded = canBeReloaded;
      if (data.ContainsKey("ammoReloadType"))
      {
        if (data["ammoReloadType"].Value<string>() == "single") currentItem.baseClass.ammoReloadType = InvItem.AmmoReloadType.magazine;
        else currentItem.baseClass.ammoReloadType = InvItem.AmmoReloadType.magazine;
      }
      if (data.ContainsKey("ammoType")) currentItem.baseClass.ammoType = data["ammoType"]?.Value<string>() ?? currentItem.baseClass.ammoType;
      if (bool.TryParse(data["aimDontSlow"]?.Value<string>(), out var aimDontSlow)) currentItem.baseClass.aimDontSlow = aimDontSlow;
      if (float.TryParse(data["aimFOV"]?.Value<string>(), out var aimFOV)) currentItem.baseClass.aimFOV = aimFOV;
      if (int.TryParse(data["fireRate"]?.Value<string>(), out var fireRate)) currentItem.baseClass.fireRate = fireRate;

      if (bool.TryParse(data["hasDurability"]?.Value<string>(), out var hasDurability)) currentItem.baseClass.hasDurability = hasDurability;
      if (int.TryParse(data["maxDurability"]?.Value<string>(), out var maxDurability)) currentItem.baseClass.maxDurability = maxDurability;
      if (bool.TryParse(data["ignoreDurabilityInValue"]?.Value<string>(), out var ignoreDurabilityInValue)) currentItem.baseClass.ignoreDurabilityInValue = ignoreDurabilityInValue;
      if (bool.TryParse(data["repairable"]?.Value<string>(), out var repairable)) currentItem.baseClass.repairable = repairable;
      if (float.TryParse(data["flamethrowerdrag"]?.Value<string>(), out var drag)) ((GameObject)currentItem.baseClass.item).GetComponent<Rigidbody>().drag = drag;
      if (int.TryParse(data["flamethrowercontactDamage"]?.Value<string>(), out var contactDamage)) ((GameObject)currentItem.baseClass.item).GetComponent<Flame>().contactDamage = contactDamage;
      if (int.TryParse(data["damage"]?.Value<string>(), out var damage)) currentItem.baseClass.damage = damage;
      if (int.TryParse(data["clipSize"]?.Value<string>(), out var clipSize)) currentItem.baseClass.clipSize = clipSize;
      if (int.TryParse(data["value"]?.Value<string>(), out var value)) currentItem.baseClass.value = value;
      if (int.TryParse(data["maxAmount"]?.Value<string>(), out var maxAmount)) currentItem.baseClass.maxAmount = maxAmount;
      if (bool.TryParse(data["stackable"]?.Value<string>(), out var stackable)) currentItem.baseClass.stackable = stackable;
      if (int.TryParse(data["clipSize"]?.Value<string>(), out var clipSizeParsed)) currentItem.ammo = clipSizeParsed;
      if (int.TryParse(data["ExpValue"]?.Value<string>(), out var expValue)) currentItem.baseClass.expValue = expValue;
      if (bool.TryParse(data["IsExpItem"]?.Value<string>(), out var isExpItem)) currentItem.baseClass.isExpItem = isExpItem;

      if (data.ContainsKey("activateSound")) currentItem.baseClass.activateSound = data["activateSound"]?.Value<string>() ?? currentItem.baseClass.activateSound;
      if (bool.TryParse(data["addsHotbarSlot"]?.Value<string>(), out var addsHotbarSlot)) currentItem.baseClass.addsHotbarSlot = addsHotbarSlot;
      if (bool.TryParse(data["addsInventorySlot"]?.Value<string>(), out var addsInventorySlot)) currentItem.baseClass.addsInventorySlot = addsInventorySlot;
      if (int.TryParse(data["addSlotAmount"]?.Value<string>(), out var addSlotAmount)) currentItem.baseClass.addSlotAmount = addSlotAmount;
      if (bool.TryParse(data["addsPoisonImmunity"]?.Value<string>(), out var addsPoisonImmunity)) currentItem.baseClass.addsPoisonImmunity = addsPoisonImmunity;
      if (int.TryParse(data["aimFinishedFrame"]?.Value<string>(), out var aimFinishedFrame)) currentItem.baseClass.aimFinishedFrame = aimFinishedFrame;
      if (data.ContainsKey("aimReturnSound")) currentItem.baseClass.aimReturnSound = data["aimReturnSound"]?.Value<string>() ?? currentItem.baseClass.aimReturnSound;
      if (data.ContainsKey("aimSound")) currentItem.baseClass.aimSound = data["aimSound"]?.Value<string>() ?? currentItem.baseClass.aimSound;
      if (data.ContainsKey("aniLibrary")) currentItem.baseClass.aniLibrary = data["aniLibrary"]?.Value<string>() ?? currentItem.baseClass.aniLibrary;
      if (int.TryParse(data["armorValue"]?.Value<string>(), out var armorValue)) currentItem.baseClass.armorValue = armorValue;
      if (data.ContainsKey("attack2Sound")) currentItem.baseClass.attack2Sound = data["attack2Sound"]?.Value<string>() ?? currentItem.baseClass.attack2Sound;
      if (bool.TryParse(data["attackDoesNotInterrupt"]?.Value<string>(), out var attackDoesNotInterrupt)) currentItem.baseClass.attackDoesNotInterrupt = attackDoesNotInterrupt;
      if (data.ContainsKey("attackSound")) currentItem.baseClass.attackSound = data["attackSound"]?.Value<string>() ?? currentItem.baseClass.attackSound;
      if (float.TryParse(data["attackSoundRange"]?.Value<string>(), out var attackSoundRange)) currentItem.baseClass.attackSoundRange = attackSoundRange;
      if (int.TryParse(data["barricadeDamageDurabilityDrain"]?.Value<string>(), out var barricadeDamageDurabilityDrain)) currentItem.baseClass.barricadeDamageDurabilityDrain = barricadeDamageDurabilityDrain;
      if (int.TryParse(data["burstAmount"]?.Value<string>(), out var burstAmount)) currentItem.baseClass.burstAmount = burstAmount;
      if (int.TryParse(data["canAttackFrame"]?.Value<string>(), out var canAttackFrame)) currentItem.baseClass.canAttackFrame = canAttackFrame;
      if (bool.TryParse(data["canBeAimed"]?.Value<string>(), out var canBeAimed)) currentItem.baseClass.canBeAimed = canBeAimed;
      if (bool.TryParse(data["canBePlaced"]?.Value<string>(), out var canBePlaced)) currentItem.baseClass.canBePlaced = canBePlaced;
      if (bool.TryParse(data["canCutInHalf"]?.Value<string>(), out var canCutInHalf)) currentItem.baseClass.canCutInHalf = canCutInHalf;
      if (bool.TryParse(data["canResumeAim"]?.Value<string>(), out var canResumeAim)) currentItem.baseClass.canResumeAim = canResumeAim;
      if (int.TryParse(data["damageDurabilityDrain"]?.Value<string>(), out var damageDurabilityDrain)) currentItem.baseClass.damageDurabilityDrain = damageDurabilityDrain;
      if (data.ContainsKey("deactivateSound")) currentItem.baseClass.deactivateSound = data["deactivateSound"]?.Value<string>() ?? currentItem.baseClass.deactivateSound;
      if (data.ContainsKey("destroySound")) currentItem.baseClass.destroySound = data["destroySound"]?.Value<string>() ?? currentItem.baseClass.destroySound;
      if (bool.TryParse(data["dontRemoveOnUse"]?.Value<string>(), out var dontRemoveOnUse)) currentItem.baseClass.dontRemoveOnUse = dontRemoveOnUse;
      if (bool.TryParse(data["dropOnReleaseAim"]?.Value<string>(), out var dropOnReleaseAim)) currentItem.baseClass.dropOnReleaseAim = dropOnReleaseAim;
      if (float.TryParse(data["durabilityDrain"]?.Value<string>(), out var durabilityDrain)) currentItem.baseClass.durabilityDrain = durabilityDrain;
      if (float.TryParse(data["durabilityRegeneration"]?.Value<string>(), out var durabilityRegeneration)) currentItem.baseClass.durabilityRegeneration = durabilityRegeneration;
      if (data.ContainsKey("emptyClipSound")) currentItem.baseClass.emptyClipSound = data["emptyClipSound"]?.Value<string>() ?? currentItem.baseClass.emptyClipSound;
      if (bool.TryParse(data["examinable"]?.Value<string>(), out var examinable)) currentItem.baseClass.examinable = examinable;
      if (data.ContainsKey("getSound")) currentItem.baseClass.getSound = data["getSound"]?.Value<string>() ?? currentItem.baseClass.getSound;
      if (bool.TryParse(data["givesLife"]?.Value<string>(), out var givesLife)) currentItem.baseClass.givesLife = givesLife;
      if (bool.TryParse(data["givesSkillSlot"]?.Value<string>(), out var givesSkillSlot)) currentItem.baseClass.givesSkillSlot = givesSkillSlot;
      if (data.ContainsKey("hideSound")) currentItem.baseClass.hideSound = data["hideSound"]?.Value<string>() ?? currentItem.baseClass.hideSound;
      if (bool.TryParse(data["isAmmo"]?.Value<string>(), out var isAmmo)) currentItem.baseClass.isAmmo = isAmmo;
      if (bool.TryParse(data["isArmor"]?.Value<string>(), out var isArmor)) currentItem.baseClass.isArmor = isArmor;
      if (bool.TryParse(data["isFirearm"]?.Value<string>(), out var isFirearm)) currentItem.baseClass.isFirearm = isFirearm;
      if (bool.TryParse(data["isFlashlight"]?.Value<string>(), out var isFlashlight)) currentItem.baseClass.isFlashlight = isFlashlight;
      if (bool.TryParse(data["isImportantItem"]?.Value<string>(), out var isImportantItem)) currentItem.baseClass.isImportantItem = isImportantItem;
      if (bool.TryParse(data["isMap"]?.Value<string>(), out var isMap)) currentItem.baseClass.isMap = isMap;
      if (bool.TryParse(data["isMelee"]?.Value<string>(), out var isMelee)) currentItem.baseClass.isMelee = isMelee;
      if (bool.TryParse(data["isNaturalLight"]?.Value<string>(), out var isNaturalLight)) currentItem.baseClass.isNaturalLight = isNaturalLight;
      if (bool.TryParse(data["isRepairKit"]?.Value<string>(), out var isRepairKit)) currentItem.baseClass.isRepairKit = isRepairKit;
      if (bool.TryParse(data["isThrowable"]?.Value<string>(), out var isThrowable)) currentItem.baseClass.isThrowable = isThrowable;
      if (bool.TryParse(data["isWorkbenchUpgrade"]?.Value<string>(), out var isWorkbenchUpgrade)) currentItem.baseClass.isWorkbenchUpgrade = isWorkbenchUpgrade;
      if (float.TryParse(data["maxAim"]?.Value<string>(), out var maxAim)) currentItem.baseClass.maxAim = maxAim;
      if (float.TryParse(data["minAim"]?.Value<string>(), out var minAim)) currentItem.baseClass.minAim = minAim;
      if (bool.TryParse(data["needsToBeOnHotbar"]?.Value<string>(), out var needsToBeOnHotbar)) currentItem.baseClass.needsToBeOnHotbar = needsToBeOnHotbar;
      if (bool.TryParse(data["nightVision"]?.Value<string>(), out var nightVision)) currentItem.baseClass.nightVision = nightVision;
      if (bool.TryParse(data["noMuzzleFlash"]?.Value<string>(), out var noMuzzleFlash)) currentItem.baseClass.noMuzzleFlash = noMuzzleFlash;
      if (bool.TryParse(data["notUseableWhenAiming"]?.Value<string>(), out var notUseableWhenAiming)) currentItem.baseClass.notUseableWhenAiming = notUseableWhenAiming;
      if (data.ContainsKey("onBrokenText")) currentItem.baseClass.onBrokenText = data["onBrokenText"]?.Value<string>() ?? currentItem.baseClass.onBrokenText;
      if (bool.TryParse(data["placeOnUse"]?.Value<string>(), out var placeOnUse)) currentItem.baseClass.placeOnUse = placeOnUse;
      if (int.TryParse(data["projectileAmount"]?.Value<string>(), out var projectileAmount)) currentItem.baseClass.projectileAmount = projectileAmount;
      if (bool.TryParse(data["protectsFromShadows"]?.Value<string>(), out var protectsFromShadows)) currentItem.baseClass.protectsFromShadows = protectsFromShadows;
      if (float.TryParse(data["recoilAmount"]?.Value<string>(), out var recoilAmount)) currentItem.baseClass.recoilAmount = recoilAmount;
      if (bool.TryParse(data["recoverableAfterThrown"]?.Value<string>(), out var recoverableAfterThrown)) currentItem.baseClass.recoverableAfterThrown = recoverableAfterThrown;
      if (bool.TryParse(data["regeneratesWhenInactive"]?.Value<string>(), out var regeneratesWhenInactive)) currentItem.baseClass.regeneratesWhenInactive = regeneratesWhenInactive;
      if (data.ContainsKey("reloadSound")) currentItem.baseClass.reloadSound = data["reloadSound"]?.Value<string>() ?? currentItem.baseClass.reloadSound;
      if (int.TryParse(data["specialBarricadeDamage"]?.Value<string>(), out var specialBarricadeDamage)) currentItem.baseClass.specialBarricadeDamage = specialBarricadeDamage;
      if (int.TryParse(data["specialBarricadeDamageDurabilityDrain"]?.Value<string>(), out var specialBarricadeDamageDurabilityDrain)) currentItem.baseClass.specialBarricadeDamageDurabilityDrain = specialBarricadeDamageDurabilityDrain;
      if (int.TryParse(data["specialDamage"]?.Value<string>(), out var specialDamage)) currentItem.baseClass.specialDamage = specialDamage;
      if (int.TryParse(data["specialDamageDurabilityDrain"]?.Value<string>(), out var specialDamageDurabilityDrain)) currentItem.baseClass.specialDamageDurabilityDrain = specialDamageDurabilityDrain;
      if (bool.TryParse(data["spillsLiquid"]?.Value<string>(), out var spillsLiquid)) currentItem.baseClass.spillsLiquid = spillsLiquid;
      if (bool.TryParse(data["stacksDurability"]?.Value<string>(), out var stacksDurability)) currentItem.baseClass.stacksDurability = stacksDurability;
      if (float.TryParse(data["staminaAttackDrain"]?.Value<string>(), out var staminaAttackDrain)) currentItem.baseClass.staminaAttackDrain = staminaAttackDrain;
      if (float.TryParse(data["staminaSpecialAttackDrain"]?.Value<string>(), out var staminaSpecialAttackDrain)) currentItem.baseClass.staminaSpecialAttackDrain = staminaSpecialAttackDrain;
      if (bool.TryParse(data["takesDamageOnPlayerHit"]?.Value<string>(), out var takesDamageOnPlayerHit)) currentItem.baseClass.takesDamageOnPlayerHit = takesDamageOnPlayerHit;
      if (float.TryParse(data["zoom"]?.Value<string>(), out var zoom)) currentItem.baseClass.zoom = zoom;

      // repair requirements
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
          currentItem.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = list;
        }
      }

      // rotten item (mushrooms)
      if (data.ContainsKey("rottenItem"))
      {
        var rottenItem = ItemsDatabase.Instance.getItem(data["rottenItem"].Value<string>(), true);
        if (rottenItem != null) currentItem.baseClass.rottenItem = rottenItem;
      }
      if (int.TryParse(data["rottenItemMaxAmount"]?.Value<string>(), out var rottenItemMaxAmount)) currentItem.baseClass.rottenItem.maxAmount = rottenItemMaxAmount;
      if (bool.TryParse(data["rottenItemStackable"]?.Value<string>(), out var rottenItemStackable)) currentItem.baseClass.rottenItem.stackable = rottenItemStackable;
      if (int.TryParse(data["rottenItemValue"]?.Value<string>(), out var rottenItemValue)) currentItem.baseClass.rottenItem.value = rottenItemValue;
      if (int.TryParse(data["rottenItemExpValue"]?.Value<string>(), out var rottenItemExpValue)) currentItem.baseClass.rottenItem.expValue = rottenItemExpValue;
      if (bool.TryParse(data["rottenItemIsExpItem"]?.Value<string>(), out var rottenItemIsExpItem)) currentItem.baseClass.rottenItem.isExpItem = rottenItemIsExpItem;
    }
  }
}