using BepInEx;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class CharacterEffectsPatch
{
    public static List<string> LogEffect = [];
    public static string LogStats = "";
    private static float _lastToggleCheckTime = -9999f;

    // Effect manager toggles: Disable keeps an effect off the player, Re-apply on loss puts it back whenever the game drops it, for example on death or sleep.
    // They are stored in the character effects config under the plain effect type, so the signed entries the game writes (type_duration_modifier_interval) are skipped by the parse below.
    public static void EnforceConfiguredToggles(Player player)
    {
        if (!Plugin.CharacterEffectsModification.Value) return;
        if (!player) return;
        if (Time.time - _lastToggleCheckTime < 0.5f) return;
        _lastToggleCheckTime = Time.time;

        var effects = player.effects ? player.effects : player.GetComponent<CharacterEffects>();
        if (!effects) return;

        try
        {
            foreach (var property in Plugin.CharacterEffects.Properties())
            {
                if (property.Value is not JObject data) continue;
                var disable = data["disable"]?.Value<bool>() ?? false;
                var reapply = data["reapplyOnLoss"]?.Value<bool>() ?? false;
                if (!disable && !reapply) continue;
                if (!Enum.TryParse(property.Name, true, out CharacterEffectType type)) continue;

                if (disable)
                {
                    if (effects.hasEffectType(type))
                    {
                        effects.deleteThisTypeOfEffect(type);
                        if (Plugin.LogDebug.Value)
                            Plugin.Log.LogInfo($"[Effects] Removed disabled effect {type}");
                    }
                    continue;
                }

                if (effects.hasEffectType(type)) continue;
                effects.activate(type, data["duration"]?.Value<float>() ?? 0f, data["modifier"]?.Value<float>() ?? 1f, data["interval"]?.Value<float>() ?? 0f, 0f);
                if (Plugin.LogDebug.Value)
                    Plugin.Log.LogInfo($"[Effects] Re-applied {type}");
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"[Effects] Failed to enforce effect toggles: {e.Message}");
        }
    }

    [HarmonyPatch(typeof(CharacterEffect), nameof(CharacterEffect.initialize))]
    [HarmonyPostfix]
    // ReSharper disable once InconsistentNaming
    public static void EffectPatch(CharacterEffect __instance, InvItemEffect effect)
    {
        if (!Plugin.CharacterEffectsModification.Value) return;
        if (Singleton<Dreams>.Instance.dreaming) return;

        var typeclean = effect.type.ToString();
        var type = $"{typeclean}_d{effect.duration.ToString(CultureInfo.InvariantCulture)}_m{effect.modifier.ToString(CultureInfo.InvariantCulture)}_i{effect.interval.ToString(CultureInfo.InvariantCulture)}";
        var customEffects = Plugin.CharacterEffects;

        // Initialize effect in config if it doesn't exist
        if (!customEffects.ContainsKey(type))
        {
            customEffects[type] = new JObject
            {
                { "duration", effect.duration },
                { "modifier", effect.modifier },
                { "interval", effect.interval },
                { "stopsBleeding", effect.stopsBleeding },
                { "stopsPoison", effect.stopsPoison },
                { "hasPoisonOverlay", effect.hasPoisonOverlay },
                { "activateSound", effect.activateSound },
                { "startDelay", effect.startDelay },
            };
            Plugin.SaveCharacterEffects = true;
        }

        // Log effect properties
        if (!LogEffect.Contains(type))
        {
            LogEffect.Add(type);
            LogStats += $"\n----------------------------------------\n[EFFECT] ID [{type}] Stats:\n";
            LogStats += $"{type}.duration = {effect.duration}\n";
            LogStats += $"{type}.modifier = {effect.modifier}\n";
            LogStats += $"{type}.interval = {effect.interval}\n";
            LogStats += $"{type}.stopsBleeding = {effect.stopsBleeding}\n";
            LogStats += $"{type}.stopsPoison = {effect.stopsPoison}\n";
            LogStats += $"{type}.hasPoisonOverlay = {effect.hasPoisonOverlay}\n";
            LogStats += $"{type}.activateSound = {effect.activateSound}\n";
            LogStats += $"{type}.startDelay = {effect.startDelay}\n";
            LogStats += $"Saved as {type} but the game ID for it is just {typeclean}\n";
            LogStats += "----------------------------------------\n";

            var logPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "EffectLog.log");
            if (!File.Exists(logPath) || File.ReadAllText(logPath) != LogStats)
            {
                File.WriteAllText(logPath, LogStats);
            }
        }

        // Apply modifications
        SetEffectValues(__instance, (JObject)Plugin.CharacterEffects[type]);
    }

    private static void SetEffectValues(CharacterEffect effect, JObject data)
    {
        if (data == null) return;

        if (float.TryParse(data["duration"]?.Value<string>(), out var duration)) 
            effect.duration = duration;
        if (float.TryParse(data["modifier"]?.Value<string>(), out var modifier)) 
            effect.modifier = modifier;
        if (float.TryParse(data["interval"]?.Value<string>(), out var interval)) 
            effect.interval = interval;
        if (bool.TryParse(data["stopsBleeding"]?.Value<string>(), out var stopsBleeding)) 
            effect.stopsBleeding = stopsBleeding;
        if (bool.TryParse(data["stopsPoison"]?.Value<string>(), out var stopsPoison)) 
            effect.stopsPoison = stopsPoison;
        if (bool.TryParse(data["hasPoisonOverlay"]?.Value<string>(), out var hasPoisonOverlay)) 
            effect.hasPoisonOverlay = hasPoisonOverlay;
        if (data.ContainsKey("activateSound")) 
            effect.activateSound = data["activateSound"]?.Value<string>();
        if (float.TryParse(data["startDelay"]?.Value<string>(), out var startDelay)) 
            effect.startDelay = startDelay;
    }
}
