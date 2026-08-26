using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class PlayerPatch
{
  public static bool RefreshPlayer = true;
  
  [HarmonyPatch(typeof(Player), "fireWeapon")]
  [HarmonyPostfix]
  private static void PlayerFiresWeapon()
  {
    if (!Plugin.ItemsModification.Value) return;
    JObject data;
    if (Plugin.CustomItems.TryGetValue(Player.Instance.currentItem.type, out var item))
    {
      data = (JObject)item;
    }
    else
    {
      data = (JObject)Plugin.DefaultCustomItems[Player.Instance.currentItem.type];
    }

    if (data == null) return;
    var isInfiniteAmmo = (bool)(data["InfiniteAmmo"] ?? false);
    var isInfiniteDurability = (bool)(data["InfiniteDurability"] ?? false);
    var shouldDrain = DrainWeapon(Player.Instance.currentItem, data);
    switch (shouldDrain)
    {
      case (true, true):
        Player.Instance.currentItem.ammo -= 1;
        Player.Instance.currentItem.drainDurability(1f);
        break;
      case (true, false):
        Player.Instance.currentItem.drainDurability(1f);
        break;
      case (false, true):
        Player.Instance.currentItem.ammo -= 1;
        break;
      case (false, false):
        break;
    }
    if (Player.Instance.currentItem.ammo > Player.Instance.currentItem.baseClass.clipSize) Player.Instance.currentItem.ammo = Player.Instance.currentItem.baseClass.clipSize;
    if (Player.Instance.currentItem.durability > Player.Instance.currentItem.baseClass.maxDurability) Player.Instance.currentItem.durability = Player.Instance.currentItem.baseClass.maxDurability;
    if (isInfiniteAmmo) Player.Instance.currentItem.ammo = Player.Instance.currentItem.baseClass.clipSize;
    if (isInfiniteDurability) Player.Instance.currentItem.durability = Player.Instance.currentItem.baseClass.maxDurability;
  }

  // ReSharper disable once UnusedParameter.Local
  private static (bool, bool) DrainWeapon(InvItemClass weapon, JObject data)
  {
    if (data == null) return (false, false);
    var drainDurability = (bool)(data["drainDurabilityOnShot"] ?? false);
    var drainAmmo = (bool)(data["drainAmmoOnShot"] ?? false);
    return (drainDurability, drainAmmo);
  }

  [HarmonyPatch(typeof(Player), nameof(Player.registerMe))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void PlayerRegistered(Player __instance)
  {
    RefreshPlayer = true;
    __instance.maxHealth = Plugin.PlayerMaxHealth.Value;
  }

  [HarmonyPatch(typeof(Player), nameof(Player.Update))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void PlayerUpdate(Player __instance)
  {
    if (Plugin.Cheats.Value && Plugin.CheatsGiveItem)
    {
      Plugin.Log.LogInfo($"Giving item {Plugin.CheatsGiveItemName.Value} x{Plugin.CheatsGiveItemAmount.Value}");
      var givenItem = __instance.Inventory.addItemTypeToPlayer(Plugin.CheatsGiveItemName.Value, Plugin.CheatsGiveItemAmount.Value, true);
      if (givenItem == null)
        Plugin.Log.LogWarning($"Failed to give item {Plugin.CheatsGiveItemName.Value} x{Plugin.CheatsGiveItemAmount.Value}: invalid item ID or no room (and dropIfNoRoom failed)");
      else
        Plugin.Log.LogInfo($"Successfully gave item {Plugin.CheatsGiveItemName.Value} x{Plugin.CheatsGiveItemAmount.Value}");
      Plugin.CheatsGiveItem = false;
    }

    if (Plugin.Cheats.Value && Plugin.CheatsSpawnCharacter.Value)
    {
      Plugin.CheatsSpawnCharacter.Value = false;
      if (string.IsNullOrEmpty(Plugin.CheatsSpawnCharacterName.Value))
      {
        Plugin.Log.LogWarning("Spawn Character was toggled but no character name was set");
      }
      else if (Singleton<CharacterSpawner>.Instance == null)
      {
        Plugin.Log.LogWarning("Spawn Character was toggled but the character spawner is not available");
      }
      else
      {
        Plugin.Log.LogInfo($"Spawning character {Plugin.CheatsSpawnCharacterName.Value}");
        var character = Singleton<CharacterSpawner>.Instance.spawnCharacterAround(__instance.gameObject, Vector3.zero, 300f, Plugin.CheatsSpawnCharacterName.Value, false);
        if (character == null)
          Plugin.Log.LogWarning($"Failed to spawn character {Plugin.CheatsSpawnCharacterName.Value}: no free spot found or invalid type");
        else
          Plugin.Log.LogInfo($"Successfully spawned character {Plugin.CheatsSpawnCharacterName.Value}");
      }
    }

    if (Plugin.CharacterEffectsModification.Value)
    {
      if (Plugin.EffectManagerApplyEffect.Value || Plugin.EffectManagerRemoveEffect.Value || Plugin.EffectManagerRemoveAllEffects.Value)
      {
        if (__instance.effects == null)
        {
          Plugin.Log.LogWarning("Effect manager was toggled but the player has no effects component");
          Plugin.EffectManagerApplyEffect.Value = false;
          Plugin.EffectManagerRemoveEffect.Value = false;
          Plugin.EffectManagerRemoveAllEffects.Value = false;
        }
      }
      if (Plugin.EffectManagerApplyEffect.Value)
      {
        Plugin.EffectManagerApplyEffect.Value = false;
        if (!Enum.TryParse(Plugin.EffectManagerEffectType.Value, true, out CharacterEffectType effectType))
        {
          Plugin.Log.LogWarning($"Apply Effect was toggled but '{Plugin.EffectManagerEffectType.Value}' is not a valid effect type");
        }
        else
        {
          __instance.effects.deleteThisTypeOfEffect(effectType);
          __instance.effects.activate(effectType, Plugin.EffectManagerEffectDuration.Value, Plugin.EffectManagerEffectModifier.Value, Plugin.EffectManagerEffectInterval.Value, 0f);
          Plugin.Log.LogInfo($"Applied effect {effectType} to player with duration {Plugin.EffectManagerEffectDuration.Value}, modifier {Plugin.EffectManagerEffectModifier.Value}, interval {Plugin.EffectManagerEffectInterval.Value}");
        }
      }
      if (Plugin.EffectManagerRemoveEffect.Value)
      {
        Plugin.EffectManagerRemoveEffect.Value = false;
        if (!Enum.TryParse(Plugin.EffectManagerEffectType.Value, true, out CharacterEffectType effectType))
        {
          Plugin.Log.LogWarning($"Remove Effect was toggled but '{Plugin.EffectManagerEffectType.Value}' is not a valid effect type");
        }
        else
        {
          __instance.effects.deleteThisTypeOfEffect(effectType);
          Plugin.Log.LogInfo($"Removed effect {effectType} from player");
        }
      }
      if (Plugin.EffectManagerRemoveAllEffects.Value)
      {
        Plugin.EffectManagerRemoveAllEffects.Value = false;
        __instance.effects.removeAllEffects();
        Plugin.Log.LogInfo("Removed all effects from player");
      }
    }

    if (Plugin.PlayerModification.Value)
    {
      if (Plugin.PlayerInfiniteStamina.Value)
      {
        __instance.stamina = __instance.maxStamina;
        if (Plugin.PlayerInfiniteStaminaEffect.Value)
        {
          __instance.flashStaminaBar();
        }
      }

      if (Plugin.PlayerGodmode.Value)
      {
        __instance.health = __instance.maxHealth;
        __instance.invulnerable = true;
      }
      else if (__instance.invulnerable) __instance.invulnerable = false;
      
      if (Plugin.PlayerNoclip.Value)
      {
        __instance.noClipMode = true;
      }
      else if (__instance.noClipMode) __instance.noClipMode = false;

      if (!RefreshPlayer) return;
      LogPlayer(__instance, true);
      
      __instance.maxStamina = Plugin.PlayerMaxStamina.Value;
      __instance.staminaRunDrainValue = Plugin.PlayerStaminaRunDrain.Value;
      __instance.staminaRegenValue = Plugin.PlayerStaminaRegen.Value;
      
      __instance.maxHealth = Plugin.PlayerMaxHealth.Value;
      if (__instance.health > __instance.maxHealth)
      {
        __instance.health = __instance.maxHealth;
      }

      __instance.healthRegenInterval = Plugin.PlayerHealthRegenInterval.Value;
      __instance.healthRegenModifier = Plugin.PlayerHealthRegenModifier.Value;
      __instance.healthRegenValue = Plugin.PlayerHealthRegenValue.Value;
      __instance.defaultFOV = Plugin.PlayerFOV.Value;
      __instance.currentDestFOV = Plugin.PlayerFOV.Value;

      __instance.walkSpeed = Plugin.PlayerWalkSpeed.Value;
      __instance.runSpeed = Plugin.PlayerRunSpeed.Value;
      __instance.runSpeedModifier = Plugin.PlayerRunSpeedModifier.Value;

      LogPlayer(__instance);
    }
    RefreshPlayer = false;
  }

  private static void LogPlayer(Player player, bool before = false)
  {
    if (!Plugin.LogDebug.Value) return;
      
    Plugin.LogDivider();
    Plugin.Log.LogInfo(before ? "[Player] BEFORE MODIFICATION" : "[Player] AFTER MODIFICATION");
    Plugin.Log.LogInfo(
      $"[Player] Has {player.healthUpgrades} health upgrades. Expected base game health is {100 + player.healthUpgrades * 25}");
    Plugin.Log.LogInfo(
      $"[Player] MaxHP: {player.maxHealth} | HPR Interval: {player.healthRegenInterval} | HPR Modifier: {player.healthRegenModifier} | HPR Value: {player.healthRegenValue}");
    Plugin.Log.LogInfo(
      $"[Player] Max Stamina: {player.maxStamina} | SR Drain: {player.staminaRunDrainValue} | SR Regen: {player.staminaRegenValue}");
    Plugin.Log.LogInfo(
      $"[Player] WS: {player.walkSpeed} | RS: {player.runSpeed} | RS Modifier: {player.runSpeedModifier}");
    Plugin.Log.LogInfo($"[Player] FoV: {player.currentDestFOV}");
    Plugin.LogDivider();
  }

  [HarmonyPatch(typeof(Player), nameof(Player.getHit), [typeof(float), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool)])]
  [HarmonyPrefix]
  // ReSharper disable InconsistentNaming
  public static void PlayerGotHit(Player __instance, float damage, Transform attackerTransform, bool CanCutInHalf, bool byPlayer, ref bool canInterrupt, bool normalHit, bool showRedScreen, bool force, bool dontShowHealthBar)
    // ReSharper restore InconsistentNaming
  {
    if (Plugin.PlayerCantGetInterrupted.Value)
    {
      canInterrupt = false;
    }
  }
}