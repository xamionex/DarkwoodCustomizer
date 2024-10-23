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
      CustomItems[type]["maxAmount"] = CustomItems[type]["maxAmount"] ?? __instance.baseClass.maxAmount;
      CustomItems[type]["stackable"] = CustomItems[type]["stackable"] ?? __instance.baseClass.stackable;
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
    string logPath = Path.Combine(Paths.ConfigPath, Plugin.PluginGUID, "ItemLog.log");
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
      if (data.ContainsKey("iconType")) CurrentItem.baseClass.iconType = data["iconType"]?.Value<string>() ?? CurrentItem.baseClass.iconType;

      // ranged weapons
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
      if (bool.TryParse(data["aimDontSlow"]?.Value<string>(), out bool aimDontSlow)) CurrentItem.baseClass.aimDontSlow = aimDontSlow;
      if (float.TryParse(data["aimFOV"]?.Value<string>(), out float aimFOV)) CurrentItem.baseClass.aimFOV = aimFOV;
      if (int.TryParse(data["fireRate"]?.Value<string>(), out int fireRate)) CurrentItem.baseClass.fireRate = fireRate;

      if (bool.TryParse(data["hasDurability"]?.Value<string>(), out bool hasDurability)) CurrentItem.baseClass.hasDurability = hasDurability;
      if (int.TryParse(data["maxDurability"]?.Value<string>(), out int maxDurability)) CurrentItem.baseClass.maxDurability = maxDurability;
      if (bool.TryParse(data["ignoreDurabilityInValue"]?.Value<string>(), out bool ignoreDurabilityInValue)) CurrentItem.baseClass.ignoreDurabilityInValue = ignoreDurabilityInValue;
      if (bool.TryParse(data["repairable"]?.Value<string>(), out bool repairable)) CurrentItem.baseClass.repairable = repairable;
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

      if (data.ContainsKey("activateSound")) CurrentItem.baseClass.activateSound = data["activateSound"]?.Value<string>() ?? CurrentItem.baseClass.activateSound;
      if (bool.TryParse(data["addsHotbarSlot"]?.Value<string>(), out bool addsHotbarSlot)) CurrentItem.baseClass.addsHotbarSlot = addsHotbarSlot;
      if (bool.TryParse(data["addsInventorySlot"]?.Value<string>(), out bool addsInventorySlot)) CurrentItem.baseClass.addsInventorySlot = addsInventorySlot;
      if (int.TryParse(data["addSlotAmount"]?.Value<string>(), out int addSlotAmount)) CurrentItem.baseClass.addSlotAmount = addSlotAmount;
      if (bool.TryParse(data["addsPoisonImmunity"]?.Value<string>(), out bool addsPoisonImmunity)) CurrentItem.baseClass.addsPoisonImmunity = addsPoisonImmunity;
      if (int.TryParse(data["aimFinishedFrame"]?.Value<string>(), out int aimFinishedFrame)) CurrentItem.baseClass.aimFinishedFrame = aimFinishedFrame;
      if (data.ContainsKey("aimReturnSound")) CurrentItem.baseClass.aimReturnSound = data["aimReturnSound"]?.Value<string>() ?? CurrentItem.baseClass.aimReturnSound;
      if (data.ContainsKey("aimSound")) CurrentItem.baseClass.aimSound = data["aimSound"]?.Value<string>() ?? CurrentItem.baseClass.aimSound;
      if (data.ContainsKey("aniLibrary")) CurrentItem.baseClass.aniLibrary = data["aniLibrary"]?.Value<string>() ?? CurrentItem.baseClass.aniLibrary;
      if (int.TryParse(data["armorValue"]?.Value<string>(), out int armorValue)) CurrentItem.baseClass.armorValue = armorValue;
      if (data.ContainsKey("attack2Sound")) CurrentItem.baseClass.attack2Sound = data["attack2Sound"]?.Value<string>() ?? CurrentItem.baseClass.attack2Sound;
      if (bool.TryParse(data["attackDoesNotInterrupt"]?.Value<string>(), out bool attackDoesNotInterrupt)) CurrentItem.baseClass.attackDoesNotInterrupt = attackDoesNotInterrupt;
      if (data.ContainsKey("attackSound")) CurrentItem.baseClass.attackSound = data["attackSound"]?.Value<string>() ?? CurrentItem.baseClass.attackSound;
      if (float.TryParse(data["attackSoundRange"]?.Value<string>(), out float attackSoundRange)) CurrentItem.baseClass.attackSoundRange = attackSoundRange;
      if (int.TryParse(data["barricadeDamageDurabilityDrain"]?.Value<string>(), out int barricadeDamageDurabilityDrain)) CurrentItem.baseClass.barricadeDamageDurabilityDrain = barricadeDamageDurabilityDrain;
      if (int.TryParse(data["burstAmount"]?.Value<string>(), out int burstAmount)) CurrentItem.baseClass.burstAmount = burstAmount;
      if (int.TryParse(data["canAttackFrame"]?.Value<string>(), out int canAttackFrame)) CurrentItem.baseClass.canAttackFrame = canAttackFrame;
      if (bool.TryParse(data["canBeAimed"]?.Value<string>(), out bool canBeAimed)) CurrentItem.baseClass.canBeAimed = canBeAimed;
      if (bool.TryParse(data["canBePlaced"]?.Value<string>(), out bool canBePlaced)) CurrentItem.baseClass.canBePlaced = canBePlaced;
      if (bool.TryParse(data["canCutInHalf"]?.Value<string>(), out bool canCutInHalf)) CurrentItem.baseClass.canCutInHalf = canCutInHalf;
      if (bool.TryParse(data["canResumeAim"]?.Value<string>(), out bool canResumeAim)) CurrentItem.baseClass.canResumeAim = canResumeAim;
      if (int.TryParse(data["damageDurabilityDrain"]?.Value<string>(), out int damageDurabilityDrain)) CurrentItem.baseClass.damageDurabilityDrain = damageDurabilityDrain;
      if (data.ContainsKey("deactivateSound")) CurrentItem.baseClass.deactivateSound = data["deactivateSound"]?.Value<string>() ?? CurrentItem.baseClass.deactivateSound;
      if (data.ContainsKey("destroySound")) CurrentItem.baseClass.destroySound = data["destroySound"]?.Value<string>() ?? CurrentItem.baseClass.destroySound;
      if (bool.TryParse(data["dontRemoveOnUse"]?.Value<string>(), out bool dontRemoveOnUse)) CurrentItem.baseClass.dontRemoveOnUse = dontRemoveOnUse;
      if (bool.TryParse(data["dropOnReleaseAim"]?.Value<string>(), out bool dropOnReleaseAim)) CurrentItem.baseClass.dropOnReleaseAim = dropOnReleaseAim;
      if (float.TryParse(data["durabilityDrain"]?.Value<string>(), out float durabilityDrain)) CurrentItem.baseClass.durabilityDrain = durabilityDrain;
      if (float.TryParse(data["durabilityRegeneration"]?.Value<string>(), out float durabilityRegeneration)) CurrentItem.baseClass.durabilityRegeneration = durabilityRegeneration;
      if (data.ContainsKey("emptyClipSound")) CurrentItem.baseClass.emptyClipSound = data["emptyClipSound"]?.Value<string>() ?? CurrentItem.baseClass.emptyClipSound;
      if (bool.TryParse(data["examinable"]?.Value<string>(), out bool examinable)) CurrentItem.baseClass.examinable = examinable;
      if (data.ContainsKey("getSound")) CurrentItem.baseClass.getSound = data["getSound"]?.Value<string>() ?? CurrentItem.baseClass.getSound;
      if (bool.TryParse(data["givesLife"]?.Value<string>(), out bool givesLife)) CurrentItem.baseClass.givesLife = givesLife;
      if (bool.TryParse(data["givesSkillSlot"]?.Value<string>(), out bool givesSkillSlot)) CurrentItem.baseClass.givesSkillSlot = givesSkillSlot;
      if (data.ContainsKey("hideSound")) CurrentItem.baseClass.hideSound = data["hideSound"]?.Value<string>() ?? CurrentItem.baseClass.hideSound;
      if (bool.TryParse(data["isAmmo"]?.Value<string>(), out bool isAmmo)) CurrentItem.baseClass.isAmmo = isAmmo;
      if (bool.TryParse(data["isArmor"]?.Value<string>(), out bool isArmor)) CurrentItem.baseClass.isArmor = isArmor;
      if (bool.TryParse(data["isFirearm"]?.Value<string>(), out bool isFirearm)) CurrentItem.baseClass.isFirearm = isFirearm;
      if (bool.TryParse(data["isFlashlight"]?.Value<string>(), out bool isFlashlight)) CurrentItem.baseClass.isFlashlight = isFlashlight;
      if (bool.TryParse(data["isImportantItem"]?.Value<string>(), out bool isImportantItem)) CurrentItem.baseClass.isImportantItem = isImportantItem;
      if (bool.TryParse(data["isMap"]?.Value<string>(), out bool isMap)) CurrentItem.baseClass.isMap = isMap;
      if (bool.TryParse(data["isMelee"]?.Value<string>(), out bool isMelee)) CurrentItem.baseClass.isMelee = isMelee;
      if (bool.TryParse(data["isNaturalLight"]?.Value<string>(), out bool isNaturalLight)) CurrentItem.baseClass.isNaturalLight = isNaturalLight;
      if (bool.TryParse(data["isRepairKit"]?.Value<string>(), out bool isRepairKit)) CurrentItem.baseClass.isRepairKit = isRepairKit;
      if (bool.TryParse(data["isThrowable"]?.Value<string>(), out bool isThrowable)) CurrentItem.baseClass.isThrowable = isThrowable;
      if (bool.TryParse(data["isWorkbenchUpgrade"]?.Value<string>(), out bool isWorkbenchUpgrade)) CurrentItem.baseClass.isWorkbenchUpgrade = isWorkbenchUpgrade;
      if (float.TryParse(data["maxAim"]?.Value<string>(), out float maxAim)) CurrentItem.baseClass.maxAim = maxAim;
      if (float.TryParse(data["minAim"]?.Value<string>(), out float minAim)) CurrentItem.baseClass.minAim = minAim;
      if (bool.TryParse(data["needsToBeOnHotbar"]?.Value<string>(), out bool needsToBeOnHotbar)) CurrentItem.baseClass.needsToBeOnHotbar = needsToBeOnHotbar;
      if (bool.TryParse(data["nightVision"]?.Value<string>(), out bool nightVision)) CurrentItem.baseClass.nightVision = nightVision;
      if (bool.TryParse(data["noMuzzleFlash"]?.Value<string>(), out bool noMuzzleFlash)) CurrentItem.baseClass.noMuzzleFlash = noMuzzleFlash;
      if (bool.TryParse(data["notUseableWhenAiming"]?.Value<string>(), out bool notUseableWhenAiming)) CurrentItem.baseClass.notUseableWhenAiming = notUseableWhenAiming;
      if (data.ContainsKey("onBrokenText")) CurrentItem.baseClass.onBrokenText = data["onBrokenText"]?.Value<string>() ?? CurrentItem.baseClass.onBrokenText;
      if (bool.TryParse(data["placeOnUse"]?.Value<string>(), out bool placeOnUse)) CurrentItem.baseClass.placeOnUse = placeOnUse;
      if (int.TryParse(data["projectileAmount"]?.Value<string>(), out int projectileAmount)) CurrentItem.baseClass.projectileAmount = projectileAmount;
      if (bool.TryParse(data["protectsFromShadows"]?.Value<string>(), out bool protectsFromShadows)) CurrentItem.baseClass.protectsFromShadows = protectsFromShadows;
      if (float.TryParse(data["recoilAmount"]?.Value<string>(), out float recoilAmount)) CurrentItem.baseClass.recoilAmount = recoilAmount;
      if (bool.TryParse(data["recoverableAfterThrown"]?.Value<string>(), out bool recoverableAfterThrown)) CurrentItem.baseClass.recoverableAfterThrown = recoverableAfterThrown;
      if (bool.TryParse(data["regeneratesWhenInactive"]?.Value<string>(), out bool regeneratesWhenInactive)) CurrentItem.baseClass.regeneratesWhenInactive = regeneratesWhenInactive;
      if (data.ContainsKey("reloadSound")) CurrentItem.baseClass.reloadSound = data["reloadSound"]?.Value<string>() ?? CurrentItem.baseClass.reloadSound;
      if (int.TryParse(data["specialBarricadeDamage"]?.Value<string>(), out int specialBarricadeDamage)) CurrentItem.baseClass.specialBarricadeDamage = specialBarricadeDamage;
      if (int.TryParse(data["specialBarricadeDamageDurabilityDrain"]?.Value<string>(), out int specialBarricadeDamageDurabilityDrain)) CurrentItem.baseClass.specialBarricadeDamageDurabilityDrain = specialBarricadeDamageDurabilityDrain;
      if (int.TryParse(data["specialDamage"]?.Value<string>(), out int specialDamage)) CurrentItem.baseClass.specialDamage = specialDamage;
      if (int.TryParse(data["specialDamageDurabilityDrain"]?.Value<string>(), out int specialDamageDurabilityDrain)) CurrentItem.baseClass.specialDamageDurabilityDrain = specialDamageDurabilityDrain;
      if (bool.TryParse(data["spillsLiquid"]?.Value<string>(), out bool spillsLiquid)) CurrentItem.baseClass.spillsLiquid = spillsLiquid;
      if (bool.TryParse(data["stacksDurability"]?.Value<string>(), out bool stacksDurability)) CurrentItem.baseClass.stacksDurability = stacksDurability;
      if (float.TryParse(data["staminaAttackDrain"]?.Value<string>(), out float staminaAttackDrain)) CurrentItem.baseClass.staminaAttackDrain = staminaAttackDrain;
      if (float.TryParse(data["staminaSpecialAttackDrain"]?.Value<string>(), out float staminaSpecialAttackDrain)) CurrentItem.baseClass.staminaSpecialAttackDrain = staminaSpecialAttackDrain;
      if (bool.TryParse(data["takesDamageOnPlayerHit"]?.Value<string>(), out bool takesDamageOnPlayerHit)) CurrentItem.baseClass.takesDamageOnPlayerHit = takesDamageOnPlayerHit;
      if (float.TryParse(data["zoom"]?.Value<string>(), out float zoom)) CurrentItem.baseClass.zoom = zoom;

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
          CurrentItem.baseClass.gameObject.AddComponent<RepairRequirements>().requirements = list;
        }
      }

      // rotten item (mushrooms)
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