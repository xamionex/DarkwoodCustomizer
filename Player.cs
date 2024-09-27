using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

public class PlayerPatch
{
    public static bool RefreshPlayer = true;

    [HarmonyPatch(typeof(Player), nameof(Player.registerMe))]
    [HarmonyPostfix]
    public static void PlayerRegistered(Player __instance)
    {
        __instance.maxHealth = Plugin.PlayerMaxHealth.Value;
    }

    [HarmonyPatch(typeof(Player), nameof(Player.Update))]
    [HarmonyPostfix]
    public static void PlayerUpdate(Player __instance)
    {
        if (Plugin.PlayerStaminaModification.Value && Plugin.PlayerInfiniteStamina.Value)
        {
            __instance.stamina = __instance.maxStamina;
            if (Plugin.PlayerInfiniteStaminaEffect.Value)
            {
                __instance.flashStaminaBar();
            }
        }
        if (Plugin.PlayerHealthModification.Value && Plugin.PlayerGodmode.Value)
        {
            __instance.health = __instance.maxHealth;
            __instance.invulnerable = true;
        }
        else if (__instance.invulnerable) __instance.invulnerable = false;
        if (!RefreshPlayer) return;
        if (Plugin.PlayerStaminaModification.Value)
        {
            __instance.maxStamina = Plugin.PlayerMaxStamina.Value;
            __instance.staminaRegenInterval = Plugin.PlayerStaminaRegenInterval.Value;
            __instance.staminaRegenValue = Plugin.PlayerStaminaRegenValue.Value;
        }
        if (Plugin.PlayerHealthModification.Value)
        {
            __instance.maxHealth = Plugin.PlayerMaxHealth.Value;
            if (__instance.health > __instance.maxHealth)
            {
                __instance.health = __instance.maxHealth;
            }
            __instance.healthRegenInterval = Plugin.PlayerHealthRegenInterval.Value;
            __instance.healthRegenModifier = Plugin.PlayerHealthRegenModifier.Value;
            __instance.healthRegenValue = Plugin.PlayerHealthRegenValue.Value;
        }
        if (Plugin.PlayerModification.Value)
        {
            __instance.defaultFOV = Plugin.PlayerFOV.Value;
            __instance.currentDestFOV = Plugin.PlayerFOV.Value;
        }
        if (Plugin.PlayerSpeedModification.Value)
        {
            __instance.walkSpeed = Plugin.PlayerWalkSpeed.Value;
            __instance.runSpeed = Plugin.PlayerRunSpeed.Value;
            __instance.runSpeedModifier = Plugin.PlayerRunSpeedModifier.Value;
        }
        if (Plugin.LogDebug.Value)
        {
            Plugin.LogDivider();
            Plugin.Log.LogInfo($"[Player] Has {__instance.healthUpgrades} health upgrades. Expected base game health is {100 + __instance.healthUpgrades * 25}");
            Plugin.Log.LogInfo($"[Player] MaxHP: {__instance.maxHealth} | HPR Interval: {__instance.healthRegenInterval} | HPR Modifier: {__instance.healthRegenModifier} | HPR Value: {__instance.healthRegenValue}");
            Plugin.Log.LogInfo($"[Player] Max Stamina: {__instance.maxStamina} | SR Interval: {__instance.staminaRegenInterval} | SR Value: {__instance.staminaRegenValue}");
            Plugin.Log.LogInfo($"[Player] WS: {__instance.walkSpeed} | RS: {__instance.runSpeed} | RS Modifier: {__instance.runSpeedModifier}");
            Plugin.Log.LogInfo($"[Player] FoV: {__instance.currentDestFOV}");
            Plugin.LogDivider();
        }
        RefreshPlayer = false;
    }

    [HarmonyPatch(typeof(Player), nameof(Player.getHit), [typeof(float), typeof(Transform), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool)])]
    [HarmonyPrefix]
    public static void gotHit(Player __instance, float damage, Transform attackerTransform, bool CanCutInHalf, bool byPlayer, ref bool canInterrupt, bool normalHit, bool showRedScreen, bool force, bool dontShowHealthBar)
    {
        if (Plugin.PlayerCantGetInterrupted.Value)
        {
            canInterrupt = false;
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.restartMeleeIndicator))]
    [HarmonyPrefix]
    public static void RestartMeleeIndicator(ref bool __runOriginal)
    {
        if (Plugin.PlayerCantGetInterrupted.Value)
        {
            __runOriginal = false;
        }
        else
        {
            __runOriginal = true;
        }
    }
}