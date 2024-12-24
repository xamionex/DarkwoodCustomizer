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

    bool isInfiniteAmmo;
    bool isInfiniteDurability;
    JObject data;
    (bool, bool) shouldDrain;

    if (Plugin.CustomItems.ContainsKey(Player.Instance.currentItem.type))
    {
      data = (JObject)Plugin.CustomItems[Player.Instance.currentItem.type];
      isInfiniteAmmo = (bool)(data["InfiniteAmmo"] ?? false);
      isInfiniteDurability = (bool)(data["InfiniteDurability"] ?? false);
      shouldDrain = DrainWeapon(Player.Instance.currentItem, data);
    }
    else
    {
      data = (JObject)Plugin.DefaultCustomItems[Player.Instance.currentItem.type];
      isInfiniteAmmo = (bool)(data["InfiniteAmmo"] ?? false);
      isInfiniteDurability = (bool)(data["InfiniteDurability"] ?? false);
      shouldDrain = DrainWeapon(Player.Instance.currentItem, data);
    }
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
    if (isInfiniteAmmo)
    {
      Player.Instance.currentItem.ammo = Player.Instance.currentItem.baseClass.clipSize;
      return;
    }
    if (isInfiniteDurability)
    {
      Player.Instance.currentItem.durability = Player.Instance.currentItem.baseClass.maxDurability;
      return;
    }
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
  public static void PlayerRegistered(Player instance)
  {
    RefreshPlayer = true;
    instance.maxHealth = Plugin.PlayerMaxHealth.Value;
  }

  [HarmonyPatch(typeof(Player), nameof(Player.Update))]
  [HarmonyPostfix]
  public static void PlayerUpdate(Player instance)
  {
    if (Plugin.PlayerStaminaModification.Value && Plugin.PlayerInfiniteStamina.Value)
    {
      instance.stamina = instance.maxStamina;
      if (Plugin.PlayerInfiniteStaminaEffect.Value)
      {
        instance.flashStaminaBar();
      }
    }
    if (Plugin.PlayerHealthModification.Value && Plugin.PlayerGodmode.Value)
    {
      instance.health = instance.maxHealth;
      instance.invulnerable = true;
    }
    else if (instance.invulnerable) instance.invulnerable = false;
    if (!RefreshPlayer) return;
    if (Plugin.PlayerStaminaModification.Value)
    {
      instance.maxStamina = Plugin.PlayerMaxStamina.Value;
      instance.staminaRegenInterval = Plugin.PlayerStaminaRegenInterval.Value;
      instance.staminaRegenValue = Plugin.PlayerStaminaRegenValue.Value;
    }
    if (Plugin.PlayerHealthModification.Value)
    {
      instance.maxHealth = Plugin.PlayerMaxHealth.Value;
      if (instance.health > instance.maxHealth)
      {
        instance.health = instance.maxHealth;
      }
      instance.healthRegenInterval = Plugin.PlayerHealthRegenInterval.Value;
      instance.healthRegenModifier = Plugin.PlayerHealthRegenModifier.Value;
      instance.healthRegenValue = Plugin.PlayerHealthRegenValue.Value;
    }
    if (Plugin.PlayerFOVModification.Value)
    {
      instance.defaultFOV = Plugin.PlayerFOV.Value;
      instance.currentDestFOV = Plugin.PlayerFOV.Value;
    }
    if (Plugin.PlayerSpeedModification.Value)
    {
      instance.walkSpeed = Plugin.PlayerWalkSpeed.Value;
      instance.runSpeed = Plugin.PlayerRunSpeed.Value;
      instance.runSpeedModifier = Plugin.PlayerRunSpeedModifier.Value;
    }
    if (Plugin.LogDebug.Value)
    {
      Plugin.LogDivider();
      Plugin.Log.LogInfo($"[Player] Has {instance.healthUpgrades} health upgrades. Expected base game health is {100 + instance.healthUpgrades * 25}");
      Plugin.Log.LogInfo($"[Player] MaxHP: {instance.maxHealth} | HPR Interval: {instance.healthRegenInterval} | HPR Modifier: {instance.healthRegenModifier} | HPR Value: {instance.healthRegenValue}");
      Plugin.Log.LogInfo($"[Player] Max Stamina: {instance.maxStamina} | SR Interval: {instance.staminaRegenInterval} | SR Value: {instance.staminaRegenValue}");
      Plugin.Log.LogInfo($"[Player] WS: {instance.walkSpeed} | RS: {instance.runSpeed} | RS Modifier: {instance.runSpeedModifier}");
      Plugin.Log.LogInfo($"[Player] FoV: {instance.currentDestFOV}");
      Plugin.LogDivider();
    }
    RefreshPlayer = false;
  }

  [HarmonyPatch(typeof(Player), nameof(Player.getHit), [typeof(float), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool)])]
  [HarmonyPrefix]
  public static void GotHit(Player instance, float damage, Transform attackerTransform, bool canCutInHalf, bool byPlayer, ref bool canInterrupt, bool normalHit, bool showRedScreen, bool force, bool dontShowHealthBar)
  {
    if (Plugin.PlayerCantGetInterrupted.Value)
    {
      canInterrupt = false;
    }
  }
}