using BepInEx;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class CharacterEffectsPatch
{
    public static List<string> LogEffect = [];
    public static string LogStats = "";

    [HarmonyPatch(typeof(CharacterEffect), nameof(CharacterEffect.initialize))]
    [HarmonyPostfix]
    public static void EffectPatch(CharacterEffect __instance, InvItemEffect effect)
    {
        if (!Plugin.CharacterEffectsModification.Value) return;
        if (Singleton<Dreams>.Instance.dreaming) return;

        var type = effect.type.ToString();
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
            LogStats += "----------------------------------------\n";

            var logPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "EffectLog.log");
            if (!File.Exists(logPath) || File.ReadAllText(logPath) != LogStats)
            {
                File.WriteAllText(logPath, LogStats);
            }
        }

        // Apply modifications
        if (Plugin.CharacterEffectsUseDefaults.Value) 
            SetEffectValues(__instance, (JObject)Plugin.DefaultCharacterEffects[type]);
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
