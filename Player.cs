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
      Plugin.CheatsGiveItem = false;
      __instance.Inventory.addItemTypeToPlayer(Plugin.CheatsGiveItemName.Value, Plugin.CheatsGiveItemAmount.Value, true);
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