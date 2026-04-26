using BepInEx;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class InvItemClassPatch
{
  private static readonly List<string> LOGItem = [];
  private static string _logStats = "";

  [HarmonyPatch(typeof(InvItemClass), nameof(InvItemClass.assignClass))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void ItemPatch(InvItemClass __instance)
  {
    if (!Plugin.ItemsModification.Value) return;
    if (Singleton<Dreams>.Instance.dreaming || __instance.baseClass == null) return;
    var type = __instance.baseClass.type ?? __instance.type;
    var typeRotten = __instance.baseClass?.rottenItem;
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

    if (!LOGItem.Contains(type))
    {
      LOGItem.Add(type);
      _logStats += $"\n----------------------------------------\n[ITEM] ID [{type}] Stats:\n";
      _logStats += $"{type}.iconType = {__instance.baseClass.iconType}\n";
      _logStats += $"{type}.hasAmmo = {__instance.baseClass.hasAmmo}\n";
      _logStats += $"{type}.canBeReloaded = {__instance.baseClass.canBeReloaded}\n";
      _logStats += $"{type}.ammoReloadType = {__instance.baseClass.ammoReloadType}\n";
      _logStats += $"{type}.ammoType = {__instance.baseClass.ammoType}\n";
      _logStats += $"{type}.hasDurability = {__instance.baseClass.hasDurability}\n";
      _logStats += $"{type}.maxDurability = {__instance.baseClass.maxDurability}\n";
      _logStats += $"{type}.ignoreDurabilityInValue = {__instance.baseClass.ignoreDurabilityInValue}\n";
      _logStats += $"{type}.repairable = {__instance.baseClass.repairable}\n";
      _logStats +=
        $"{type}.repairrequirements = {__instance.baseClass.gameObject.GetComponent<RepairRequirements>()?.requirements?.Count}\n";
      _logStats +=
        $"{type}.flamethrowerdrag = {((GameObject)__instance.baseClass.item)?.GetComponent<Rigidbody>()?.drag}\n";
      _logStats +=
        $"{type}.flamethrowercontactDamage = {((GameObject)__instance.baseClass.item)?.GetComponent<Flame>()?.contactDamage}\n";
      _logStats += $"{type}.damage = {__instance.baseClass.damage}\n";
      _logStats += $"{type}.clipSize = {__instance.baseClass.clipSize}\n";
      _logStats += $"{type}.value = {__instance.baseClass.value}\n";
      _logStats += $"{type}.maxAmount = {__instance.baseClass.maxAmount}\n";
      _logStats += $"{type}.stackable = {__instance.baseClass.stackable}\n";
      _logStats += $"{type}.expValue = {__instance.baseClass.expValue}\n";
      _logStats += $"{type}.isExpItem = {__instance.baseClass.isExpItem}\n";
      _logStats += "----------------------------------------\n";
    }

    var logPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "ItemLog.log");
    if (!File.Exists(logPath) || File.ReadAllText(logPath) != _logStats)
    {
      File.WriteAllText(logPath, _logStats);
    }

    if (!Plugin.ItemsModification.Value) return;
    if (Plugin.CustomItemsUseDefaults.Value)
      SetItemValues(__instance, (JObject)Plugin.DefaultCustomItems[__instance.type]);
    SetItemValues(__instance, (JObject)Plugin.CustomItems[__instance.type]);
    if (Plugin.UseGlobalStackSize.Value)
    {
      __instance.baseClass.maxAmount = Plugin.StackResize.Value;
    }

    if (Plugin.UseGlobalMaxDurability.Value && __instance.baseClass.hasDurability)
    {
      __instance.baseClass.maxDurability = Plugin.MaxDurability.Value;
    }
  }

  private static void SetItemValues(InvItemClass currentItem, JObject data)
  {
    if (data == null) return;
    
    if (data.ContainsKey("iconType"))
      currentItem.baseClass.iconType = data["iconType"]?.Value<string>() ?? currentItem.baseClass.iconType;

    // ranged weapons
    if (data.ContainsKey("fireMode"))
    {
      currentItem.baseClass.fireMode = data["fireMode"].Value<string>().ToLower() switch
      {
        "semi" => InvItem.FireMode.semiAuto,
        "burst" => InvItem.FireMode.burst,
        "fullauto" => InvItem.FireMode.fullAuto,
        "auto" => InvItem.FireMode.fullAuto,
        _ => InvItem.FireMode.oneShot
      };
    }

    if (data["hasAmmo"] != null && bool.TryParse(data["hasAmmo"]?.Value<string>(), out var hasAmmo)) currentItem.baseClass.hasAmmo = hasAmmo;
    if (data["canBeReloaded"] != null && bool.TryParse(data["canBeReloaded"]?.Value<string>(), out var canBeReloaded))
      currentItem.baseClass.canBeReloaded = canBeReloaded;
    if (data.ContainsKey("ammoReloadType"))
    {
      currentItem.baseClass.ammoReloadType = data["ammoReloadType"].Value<string>() == "single" ? InvItem.AmmoReloadType.single : InvItem.AmmoReloadType.magazine;
    }

    if (data.ContainsKey("ammoType"))
      currentItem.baseClass.ammoType = data["ammoType"]?.Value<string>() ?? currentItem.baseClass.ammoType;
    if (data["aimDontSlow"] != null && bool.TryParse(data["aimDontSlow"]?.Value<string>(), out var aimDontSlow))
      currentItem.baseClass.aimDontSlow = aimDontSlow;
    if (data["aimFOV"] != null && float.TryParse(data["aimFOV"]?.Value<string>(), out var aimFOV)) currentItem.baseClass.aimFOV = aimFOV;
    if (data["fireRate"] != null && int.TryParse(data["fireRate"]?.Value<string>(), out var fireRate)) currentItem.baseClass.fireRate = fireRate;

    if (data["hasDurability"] != null && bool.TryParse(data["hasDurability"]?.Value<string>(), out var hasDurability))
      currentItem.baseClass.hasDurability = hasDurability;
    if (data["maxDurability"] != null && int.TryParse(data["maxDurability"]?.Value<string>(), out var maxDurability))
      currentItem.baseClass.maxDurability = maxDurability;
    if (data["ignoreDurabilityInValue"] != null && bool.TryParse(data["ignoreDurabilityInValue"]?.Value<string>(), out var ignoreDurabilityInValue))
      currentItem.baseClass.ignoreDurabilityInValue = ignoreDurabilityInValue;
    if (data["repairable"] != null && bool.TryParse(data["repairable"]?.Value<string>(), out var repairable))
      currentItem.baseClass.repairable = repairable;
    if (data["flamethrowerdrag"] != null && float.TryParse(data["flamethrowerdrag"]?.Value<string>(), out var drag))
      ((GameObject)currentItem.baseClass.item).GetComponent<Rigidbody>().drag = drag;
    if (data["flamethrowercontactDamage"] != null && int.TryParse(data["flamethrowercontactDamage"]?.Value<string>(), out var contactDamage))
      ((GameObject)currentItem.baseClass.item).GetComponent<Flame>().contactDamage = contactDamage;
    if (data["damage"] != null && int.TryParse(data["damage"]?.Value<string>(), out var damage)) currentItem.baseClass.damage = damage;
    if (data["clipSize"] != null && int.TryParse(data["clipSize"]?.Value<string>(), out var clipSize)) currentItem.baseClass.clipSize = clipSize;
    if (data["value"] != null && int.TryParse(data["value"]?.Value<string>(), out var value)) currentItem.baseClass.value = value;
    if (data["maxAmount"] != null && int.TryParse(data["maxAmount"]?.Value<string>(), out var maxAmount))
      currentItem.baseClass.maxAmount = maxAmount;
    if (data["stackable"] != null && bool.TryParse(data["stackable"]?.Value<string>(), out var stackable))
      currentItem.baseClass.stackable = stackable;
    if (data["clipSize"] != null && int.TryParse(data["clipSize"]?.Value<string>(), out var clipSizeParsed)) currentItem.ammo = clipSizeParsed;
    if (data["ExpValue"] != null && int.TryParse(data["ExpValue"]?.Value<string>(), out var expValue)) currentItem.baseClass.expValue = expValue;
    if (data["IsExpItem"] != null && bool.TryParse(data["IsExpItem"]?.Value<string>(), out var isExpItem))
      currentItem.baseClass.isExpItem = isExpItem;

    if (data.ContainsKey("activateSound"))
      currentItem.baseClass.activateSound =
        data["activateSound"]?.Value<string>() ?? currentItem.baseClass.activateSound;
    if (data["addsHotbarSlot"] != null && bool.TryParse(data["addsHotbarSlot"]?.Value<string>(), out var addsHotbarSlot))
      currentItem.baseClass.addsHotbarSlot = addsHotbarSlot;
    if (data["addsInventorySlot"] != null && bool.TryParse(data["addsInventorySlot"]?.Value<string>(), out var addsInventorySlot))
      currentItem.baseClass.addsInventorySlot = addsInventorySlot;
    if (data["addSlotAmount"] != null && int.TryParse(data["addSlotAmount"]?.Value<string>(), out var addSlotAmount))
      currentItem.baseClass.addSlotAmount = addSlotAmount;
    if (data["addsPoisonImmunity"] != null && bool.TryParse(data["addsPoisonImmunity"]?.Value<string>(), out var addsPoisonImmunity))
      currentItem.baseClass.addsPoisonImmunity = addsPoisonImmunity;
    if (data["aimFinishedFrame"] != null && int.TryParse(data["aimFinishedFrame"]?.Value<string>(), out var aimFinishedFrame))
      currentItem.baseClass.aimFinishedFrame = aimFinishedFrame;
    if (data.ContainsKey("aimReturnSound"))
      currentItem.baseClass.aimReturnSound =
        data["aimReturnSound"]?.Value<string>() ?? currentItem.baseClass.aimReturnSound;
    if (data.ContainsKey("aimSound"))
      currentItem.baseClass.aimSound = data["aimSound"]?.Value<string>() ?? currentItem.baseClass.aimSound;
    if (data.ContainsKey("aniLibrary"))
      currentItem.baseClass.aniLibrary = data["aniLibrary"]?.Value<string>() ?? currentItem.baseClass.aniLibrary;
    if (data["armorValue"] != null && int.TryParse(data["armorValue"]?.Value<string>(), out var armorValue))
      currentItem.baseClass.armorValue = armorValue;
    if (data.ContainsKey("attack2Sound"))
      currentItem.baseClass.attack2Sound =
        data["attack2Sound"]?.Value<string>() ?? currentItem.baseClass.attack2Sound;
    if (data["attackDoesNotInterrupt"] != null && bool.TryParse(data["attackDoesNotInterrupt"]?.Value<string>(), out var attackDoesNotInterrupt))
      currentItem.baseClass.attackDoesNotInterrupt = attackDoesNotInterrupt;
    if (data.ContainsKey("attackSound"))
      currentItem.baseClass.attackSound = data["attackSound"]?.Value<string>() ?? currentItem.baseClass.attackSound;
    if (data["attackSoundRange"] != null && float.TryParse(data["attackSoundRange"]?.Value<string>(), out var attackSoundRange))
      currentItem.baseClass.attackSoundRange = attackSoundRange;
    if (data["barricadeDamageDurabilityDrain"] != null && int.TryParse(data["barricadeDamageDurabilityDrain"]?.Value<string>(), out var barricadeDamageDurabilityDrain))
      currentItem.baseClass.barricadeDamageDurabilityDrain = barricadeDamageDurabilityDrain;
    if (data["burstAmount"] != null && int.TryParse(data["burstAmount"]?.Value<string>(), out var burstAmount))
      currentItem.baseClass.burstAmount = burstAmount;
    if (data["canAttackFrame"] != null && int.TryParse(data["canAttackFrame"]?.Value<string>(), out var canAttackFrame))
      currentItem.baseClass.canAttackFrame = canAttackFrame;
    if (data["canBeAimed"] != null && bool.TryParse(data["canBeAimed"]?.Value<string>(), out var canBeAimed))
      currentItem.baseClass.canBeAimed = canBeAimed;
    if (data["canBePlaced"] != null && bool.TryParse(data["canBePlaced"]?.Value<string>(), out var canBePlaced))
      currentItem.baseClass.canBePlaced = canBePlaced;
    if (data["canCutInHalf"] != null && bool.TryParse(data["canCutInHalf"]?.Value<string>(), out var canCutInHalf))
      currentItem.baseClass.canCutInHalf = canCutInHalf;
    if (data["canResumeAim"] != null && bool.TryParse(data["canResumeAim"]?.Value<string>(), out var canResumeAim))
      currentItem.baseClass.canResumeAim = canResumeAim;
    if (data["damageDurabilityDrain"] != null && int.TryParse(data["damageDurabilityDrain"]?.Value<string>(), out var damageDurabilityDrain))
      currentItem.baseClass.damageDurabilityDrain = damageDurabilityDrain;
    if (data.ContainsKey("deactivateSound"))
      currentItem.baseClass.deactivateSound =
        data["deactivateSound"]?.Value<string>() ?? currentItem.baseClass.deactivateSound;
    if (data.ContainsKey("destroySound"))
      currentItem.baseClass.destroySound =
        data["destroySound"]?.Value<string>() ?? currentItem.baseClass.destroySound;
    if (data["dontRemoveOnUse"] != null && bool.TryParse(data["dontRemoveOnUse"]?.Value<string>(), out var dontRemoveOnUse))
      currentItem.baseClass.dontRemoveOnUse = dontRemoveOnUse;
    if (data["dropOnReleaseAim"] != null && bool.TryParse(data["dropOnReleaseAim"]?.Value<string>(), out var dropOnReleaseAim))
      currentItem.baseClass.dropOnReleaseAim = dropOnReleaseAim;
    if (data["durabilityDrain"] != null && float.TryParse(data["durabilityDrain"]?.Value<string>(), out var durabilityDrain))
      currentItem.baseClass.durabilityDrain = durabilityDrain;
    if (data["durabilityRegeneration"] != null && float.TryParse(data["durabilityRegeneration"]?.Value<string>(), out var durabilityRegeneration))
      currentItem.baseClass.durabilityRegeneration = durabilityRegeneration;
    if (data.ContainsKey("emptyClipSound"))
      currentItem.baseClass.emptyClipSound =
        data["emptyClipSound"]?.Value<string>() ?? currentItem.baseClass.emptyClipSound;
    if (data["examinable"] != null && bool.TryParse(data["examinable"]?.Value<string>(), out var examinable))
      currentItem.baseClass.examinable = examinable;
    if (data.ContainsKey("getSound"))
      currentItem.baseClass.getSound = data["getSound"]?.Value<string>() ?? currentItem.baseClass.getSound;
    if (data["givesLife"] != null && bool.TryParse(data["givesLife"]?.Value<string>(), out var givesLife))
      currentItem.baseClass.givesLife = givesLife;
    if (data["givesSkillSlot"] != null && bool.TryParse(data["givesSkillSlot"]?.Value<string>(), out var givesSkillSlot))
      currentItem.baseClass.givesSkillSlot = givesSkillSlot;
    if (data.ContainsKey("hideSound"))
      currentItem.baseClass.hideSound = data["hideSound"]?.Value<string>() ?? currentItem.baseClass.hideSound;
    if (data["isAmmo"] != null && bool.TryParse(data["isAmmo"]?.Value<string>(), out var isAmmo)) currentItem.baseClass.isAmmo = isAmmo;
    if (data["isArmor"] != null && bool.TryParse(data["isArmor"]?.Value<string>(), out var isArmor)) currentItem.baseClass.isArmor = isArmor;
    if (data["isFirearm"] != null && bool.TryParse(data["isFirearm"]?.Value<string>(), out var isFirearm))
      currentItem.baseClass.isFirearm = isFirearm;
    if (data["isFlashlight"] != null && bool.TryParse(data["isFlashlight"]?.Value<string>(), out var isFlashlight))
      currentItem.baseClass.isFlashlight = isFlashlight;
    if (data["isImportantItem"] != null && bool.TryParse(data["isImportantItem"]?.Value<string>(), out var isImportantItem))
      currentItem.baseClass.isImportantItem = isImportantItem;
    if (data["isMap"] != null && bool.TryParse(data["isMap"]?.Value<string>(), out var isMap)) currentItem.baseClass.isMap = isMap;
    if (data["isMelee"] != null && bool.TryParse(data["isMelee"]?.Value<string>(), out var isMelee)) currentItem.baseClass.isMelee = isMelee;
    if (data["isNaturalLight"] != null && bool.TryParse(data["isNaturalLight"]?.Value<string>(), out var isNaturalLight))
      currentItem.baseClass.isNaturalLight = isNaturalLight;
    if (data["isRepairKit"] != null && bool.TryParse(data["isRepairKit"]?.Value<string>(), out var isRepairKit))
      currentItem.baseClass.isRepairKit = isRepairKit;
    if (data["isThrowable"] != null && bool.TryParse(data["isThrowable"]?.Value<string>(), out var isThrowable))
      currentItem.baseClass.isThrowable = isThrowable;
    if (data["isWorkbenchUpgrade"] != null && bool.TryParse(data["isWorkbenchUpgrade"]?.Value<string>(), out var isWorkbenchUpgrade))
      currentItem.baseClass.isWorkbenchUpgrade = isWorkbenchUpgrade;
    if (data["maxAim"] != null && float.TryParse(data["maxAim"]?.Value<string>(), out var maxAim)) currentItem.baseClass.maxAim = maxAim;
    if (data["minAim"] != null && float.TryParse(data["minAim"]?.Value<string>(), out var minAim)) currentItem.baseClass.minAim = minAim;
    if (data["needsToBeOnHotbar"] != null && bool.TryParse(data["needsToBeOnHotbar"]?.Value<string>(), out var needsToBeOnHotbar))
      currentItem.baseClass.needsToBeOnHotbar = needsToBeOnHotbar;
    if (data["nightVision"] != null && bool.TryParse(data["nightVision"]?.Value<string>(), out var nightVision))
      currentItem.baseClass.nightVision = nightVision;
    if (data["noMuzzleFlash"] != null && bool.TryParse(data["noMuzzleFlash"]?.Value<string>(), out var noMuzzleFlash))
      currentItem.baseClass.noMuzzleFlash = noMuzzleFlash;
    if (data["notUseableWhenAiming"] != null && bool.TryParse(data["notUseableWhenAiming"]?.Value<string>(), out var notUseableWhenAiming))
      currentItem.baseClass.notUseableWhenAiming = notUseableWhenAiming;
    if (data.ContainsKey("onBrokenText"))
      currentItem.baseClass.onBrokenText =
        data["onBrokenText"]?.Value<string>() ?? currentItem.baseClass.onBrokenText;
    if (data["placeOnUse"] != null && bool.TryParse(data["placeOnUse"]?.Value<string>(), out var placeOnUse))
      currentItem.baseClass.placeOnUse = placeOnUse;
    if (data["projectileAmount"] != null && int.TryParse(data["projectileAmount"]?.Value<string>(), out var projectileAmount))
      currentItem.baseClass.projectileAmount = projectileAmount;
    if (data["protectsFromShadows"] != null && bool.TryParse(data["protectsFromShadows"]?.Value<string>(), out var protectsFromShadows))
      currentItem.baseClass.protectsFromShadows = protectsFromShadows;
    if (data["recoilAmount"] != null && float.TryParse(data["recoilAmount"]?.Value<string>(), out var recoilAmount))
      currentItem.baseClass.recoilAmount = recoilAmount;
    if (data["recoverableAfterThrown"] != null && bool.TryParse(data["recoverableAfterThrown"]?.Value<string>(), out var recoverableAfterThrown))
      currentItem.baseClass.recoverableAfterThrown = recoverableAfterThrown;
    if (data["regeneratesWhenInactive"] != null && bool.TryParse(data["regeneratesWhenInactive"]?.Value<string>(), out var regeneratesWhenInactive))
      currentItem.baseClass.regeneratesWhenInactive = regeneratesWhenInactive;
    if (data.ContainsKey("reloadSound"))
      currentItem.baseClass.reloadSound = data["reloadSound"]?.Value<string>() ?? currentItem.baseClass.reloadSound;
    if (data["specialBarricadeDamage"] != null && int.TryParse(data["specialBarricadeDamage"]?.Value<string>(), out var specialBarricadeDamage))
      currentItem.baseClass.specialBarricadeDamage = specialBarricadeDamage;
    if (data["specialBarricadeDamageDurabilityDrain"] != null && int.TryParse(data["specialBarricadeDamageDurabilityDrain"]?.Value<string>(),
          out var specialBarricadeDamageDurabilityDrain))
      currentItem.baseClass.specialBarricadeDamageDurabilityDrain = specialBarricadeDamageDurabilityDrain;
    if (data["specialDamage"] != null && int.TryParse(data["specialDamage"]?.Value<string>(), out var specialDamage))
      currentItem.baseClass.specialDamage = specialDamage;
    if (data["specialDamageDurabilityDrain"] != null && int.TryParse(data["specialDamageDurabilityDrain"]?.Value<string>(), out var specialDamageDurabilityDrain))
      currentItem.baseClass.specialDamageDurabilityDrain = specialDamageDurabilityDrain;
    if (data["spillsLiquid"] != null && bool.TryParse(data["spillsLiquid"]?.Value<string>(), out var spillsLiquid))
      currentItem.baseClass.spillsLiquid = spillsLiquid;
    if (data["stacksDurability"] != null && bool.TryParse(data["stacksDurability"]?.Value<string>(), out var stacksDurability))
      currentItem.baseClass.stacksDurability = stacksDurability;
    if (data["staminaAttackDrain"] != null && float.TryParse(data["staminaAttackDrain"]?.Value<string>(), out var staminaAttackDrain))
      currentItem.baseClass.staminaAttackDrain = staminaAttackDrain;
    if (data["staminaSpecialAttackDrain"] != null && float.TryParse(data["staminaSpecialAttackDrain"]?.Value<string>(), out var staminaSpecialAttackDrain))
      currentItem.baseClass.staminaSpecialAttackDrain = staminaSpecialAttackDrain;
    if (data["takesDamageOnPlayerHit"] != null && bool.TryParse(data["takesDamageOnPlayerHit"]?.Value<string>(), out var takesDamageOnPlayerHit))
      currentItem.baseClass.takesDamageOnPlayerHit = takesDamageOnPlayerHit;
    if (data["zoom"] != null && float.TryParse(data["zoom"]?.Value<string>(), out var zoom)) currentItem.baseClass.zoom = zoom;

    // repair requirements
    if (data.ContainsKey("requirements"))
    {
      var requirements = data["requirements"].Value<JObject>();
      if (requirements != null)
      {
        var list = (from requirement in requirements.Properties() let item = ItemsDatabase.Instance.getItem(requirement.Name) select new CraftingRequirement { item = item, durabilityAmount = item.hasDurability ? (float)requirement.Value : 1, amount = item.hasDurability ? 1 : (int)requirement.Value }).ToList();
        currentItem.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = list;
      }
    }

    // rotten item (mushrooms)
    if (data.ContainsKey("rottenItem"))
    {
      var rottenItem = ItemsDatabase.Instance.getItem(data["rottenItem"].Value<string>());
      if (rottenItem != null) currentItem.baseClass.rottenItem = rottenItem;
    }

    if (data["rottenItemMaxAmount"] != null && int.TryParse(data["rottenItemMaxAmount"]?.Value<string>(), out var rottenItemMaxAmount))
      currentItem.baseClass.rottenItem.maxAmount = rottenItemMaxAmount;
    if (data["rottenItemStackable"] != null && bool.TryParse(data["rottenItemStackable"]?.Value<string>(), out var rottenItemStackable))
      currentItem.baseClass.rottenItem.stackable = rottenItemStackable;
    if (data["rottenItemValue"] != null && int.TryParse(data["rottenItemValue"]?.Value<string>(), out var rottenItemValue))
      currentItem.baseClass.rottenItem.value = rottenItemValue;
    if (data["rottenItemExpValue"] != null && int.TryParse(data["rottenItemExpValue"]?.Value<string>(), out var rottenItemExpValue))
      currentItem.baseClass.rottenItem.expValue = rottenItemExpValue;
    if (data["rottenItemIsExpItem"] != null && bool.TryParse(data["rottenItemIsExpItem"]?.Value<string>(), out var rottenItemIsExpItem))
      currentItem.baseClass.rottenItem.isExpItem = rottenItemIsExpItem;
  }

  [HarmonyPatch(typeof(InvItemClass), "drainDurability")]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static bool PreventDurabilityDrain(InvItemClass __instance)
  {
    if (!Plugin.ItemsModification.Value) return true;

    var type = __instance.baseClass?.type ?? __instance.type;

    var infinite = Plugin.CustomItems[type] is JObject j && j["InfiniteDurability"]?.Value<bool>() == true ||
                   Plugin.DefaultCustomItems[type] is JObject j2 && j2["InfiniteDurability"]?.Value<bool>() == true;

    return !infinite; // skip original if infinite
  }
}