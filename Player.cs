using System;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class PlayerPatch
{
  public static bool RefreshPlayer = true;
  private static Light2D _fovLight;
  private static LayerMask _originalFovLightShadowLayer;
  // The game's own FoV, captured once per player instance before the mod overwrites it.
  // Used to restore normal vision during the day while the night only FoV setting is on.
  private static Player _fovPlayer;
  private static float _vanillaFov;
  
  [HarmonyPatch(typeof(Player), "fireWeapon")]
  [HarmonyPostfix]
  private static void PlayerFiresWeapon()
  {
    if (!Plugin.CustomItemsModification.Value) return;
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
    // registerMe runs while the player is still untouched, so defaultFOV is the game's own value here
    CaptureVanillaFov(__instance);
    __instance.maxHealth = Plugin.PlayerMaxHealth.Value;
  }

  private static void CaptureVanillaFov(Player player)
  {
    if (ReferenceEquals(_fovPlayer, player)) return;
    _fovPlayer = player;
    _vanillaFov = player.defaultFOV;
  }

  // Cutscenes set their own FoV, the night only FoV override must not fight them
  private static bool IsCutscenePlaying()
  {
    var controller = Singleton<Controller>.Instance;
    return controller && controller.playingCutscene;
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
      else if (!Singleton<CharacterSpawner>.Instance)
      {
        Plugin.Log.LogWarning("Spawn Character was toggled but the character spawner is not available");
      }
      else
      {
        Plugin.Log.LogInfo($"Spawning character {Plugin.CheatsSpawnCharacterName.Value}");
        var character = Singleton<CharacterSpawner>.Instance.spawnCharacterAround(__instance.gameObject, Vector3.zero, 300f, Plugin.CheatsSpawnCharacterName.Value, false);
        if (!character)
          Plugin.Log.LogWarning($"Failed to spawn character {Plugin.CheatsSpawnCharacterName.Value}: no free spot found or invalid type");
        else
          Plugin.Log.LogInfo($"Successfully spawned character {Plugin.CheatsSpawnCharacterName.Value}");
      }
    }

    if (Plugin.PlayerModification.Value && Plugin.PlayerInvisible.Value)
    {
      if (!__instance.invisible)
      {
        __instance.setInvisible(true);
      }
    }
    else if (__instance.invisible && (!__instance.effects || !__instance.effects.hasEffectType(CharacterEffectType.ninja)))
    {
      __instance.setInvisible(false);
    }

    if (Plugin.PlayerModification.Value && Plugin.PlayerSeeBehindWalls.Value)
    {
      // The FOV light shadows walls around the player.
      // A zero shadow layer makes the vision mesh ignore walls, like fly-by mode does. 
      // The component is cached so the layer is only set once, setting it every frame would rebuild the vision mesh constantly.
      if (!_fovLight)
      {
        _fovLight = __instance._transform.Find("PlayerFOVLight").GetComponent<Light2D>();
        _originalFovLightShadowLayer = _fovLight.ShadowLayer;
      }
      if (_fovLight.ShadowLayer.value != 0)
      {
        _fovLight.ShadowLayer = 0;
      }
    }
    else if (_fovLight)
    {
      if (_fovLight.ShadowLayer.value != _originalFovLightShadowLayer.value)
      {
        _fovLight.ShadowLayer = _originalFovLightShadowLayer;
      }
      _fovLight = null;
    }

    if (Plugin.CharacterEffectsModification.Value)
    {
      if (Plugin.EffectManagerApplyEffect.Value || Plugin.EffectManagerRemoveEffect.Value || Plugin.EffectManagerRemoveAllEffects.Value)
      {
        if (!__instance.effects)
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

    // Keeps the effect manager toggles honored: disabled effects stay off, effects marked to re-apply on loss come back
    CharacterEffectsPatch.EnforceConfiguredToggles(__instance);

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

      CaptureVanillaFov(__instance);
      // Night only FoV: the configured FoV replaces the game's own value at night and the original value comes back during the day. Runs every frame so dawn and dusk switch the FoV by themselves.
      // Cutscenes set their own FoV, leave those alone.
      if (Plugin.PlayerFOVNightOnly.Value && !IsCutscenePlaying())
      {
        __instance.currentDestFOV = Core.isDay() ? _vanillaFov : Plugin.PlayerFOV.Value;
      }

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
      if (Plugin.PlayerFOVNightOnly.Value)
      {
        // defaultFOV drives the game's own FoV logic (for example the Fearful skill tweens back to it), so it keeps the game's value and only the destination FoV is overridden
        __instance.defaultFOV = _vanillaFov;
        __instance.currentDestFOV = Core.isDay() ? _vanillaFov : Plugin.PlayerFOV.Value;
      }
      else
      {
        __instance.defaultFOV = Plugin.PlayerFOV.Value;
        __instance.currentDestFOV = Plugin.PlayerFOV.Value;
      }

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
    Plugin.Log.LogInfo($"[Player] Has {player.healthUpgrades} health upgrades. Expected base game health is {100 + player.healthUpgrades * 25}");
    Plugin.Log.LogInfo($"[Player] MaxHP: {player.maxHealth} | HPR Interval: {player.healthRegenInterval} | HPR Modifier: {player.healthRegenModifier} | HPR Value: {player.healthRegenValue}");
    Plugin.Log.LogInfo($"[Player] Max Stamina: {player.maxStamina} | SR Drain: {player.staminaRunDrainValue} | SR Regen: {player.staminaRegenValue}");
    Plugin.Log.LogInfo($"[Player] WS: {player.walkSpeed} | RS: {player.runSpeed} | RS Modifier: {player.runSpeedModifier}");
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

  // The cursor name shown for an armed trap is Language.Get(item.name, "Objects"), which is the raw prefab name ("beartrap") or the loot name a triggered trap was renamed to ("Scrap metal").
  // Neither reflects the Recover Items settings, so put the reward text back in right after the cursor composed it.
  // The pickup text of a triggered trap goes through Item.getRealName() instead, which ItemPatch already overrides.
  [HarmonyPatch(typeof(Player), "selectObjectMouseAndKeyboard")]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void TrapCursorTextMouse(Player __instance, Transform selectedTransform)
  {
    FixTrapCursorText(__instance, selectedTransform);
  }

  [HarmonyPatch(typeof(Player), "selectObjectController")]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void TrapCursorTextController(Player __instance, Transform selectedTransform)
  {
    FixTrapCursorText(__instance, selectedTransform);
  }

  private static void FixTrapCursorText(Player player, Transform selectedTransform)
  {
    if (!Plugin.DefensesModification.Value || !selectedTransform) return;

    var item = selectedTransform.GetComponent<Item>();
    if (!item || item.isDroppedItem) return;

    var trigger = item.GetComponent<Trigger>();
    if (!trigger || !DefensesPatch.IsTrapType(trigger)) return;

    var reward = DefensesPatch.GetTrapRewardName(trigger);
    if (reward == null) return;

    if (!player.MouseText || !player.MouseText.activeInHierarchy) return;
    var textMesh = player.MouseText.GetComponent<tk2dTextMesh>();
    if (!textMesh) return;

    // Only swap out the name the game itself wrote for this trap, never a label some other part of the UI has put there in the same frame.
    if (textMesh.text != Language.Get(item.name, "Objects")) return;
    textMesh.text = reward;
  }
}
