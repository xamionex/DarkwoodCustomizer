using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

public class CharacterPatch
{
    public static List<string> CustomCharactersList = [];

    private static readonly Dictionary<string, (Character.SensorType[], int[])> loggedDamage = new();

    [HarmonyPatch(typeof(Character), "doAttack")]
    [HarmonyPrefix]
    public static void doAttack(Character __instance)
    {
        if (!Plugin.CharacterModification.Value) return;
        if (!loggedDamage.ContainsKey(__instance.name)) loggedDamage[__instance.name] = (__instance.sensorTypes.ToArray(), __instance.sensorTypes.Select(s => s.damage).ToArray());
        if (Plugin.CustomCharacters.TryGetValue(__instance.name, out JToken Stats))
        {
            float damageModifier = float.Parse(Stats["damage"]?.ToString() ?? "0");
            for (int i = 0; i < __instance.sensorTypes.Count; i++)
            {
                __instance.sensorTypes[i].damage = Convert.ToInt32(loggedDamage[__instance.name].Item2[i] * damageModifier);
                if (Plugin.LogCharacters.Value)
                {
                    Plugin.Log.LogInfo($"[CHARACTER] {__instance.name}: One of the characters attacks modified to deal: {loggedDamage[__instance.name].Item2[i] * damageModifier} Damage");
                }
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Update))]
    [HarmonyPrefix]
    public static void CharUpdate(Character __instance)
    {
        if (Plugin.CustomCharacters.TryGetValue(__instance.name, out JToken Stats))
        {
            foreach (var Stat in Stats.Children())
            {
                switch (Stat.Path)
                {
                    case "health":
                        __instance.maxHealth = float.Parse(Stat.ToString());
                        break;
                    case "walkspeed":
                        __instance.idleWalkSpeed = float.Parse(Stat.ToString());
                        break;
                    case "runspeed":
                        __instance.chaseSpeed = float.Parse(Stat.ToString());
                        break;
                    default:
                        break;
                }
            }
        }
    }

    [HarmonyPatch(typeof(Character), "Awake")]
    [HarmonyPrefix]
    public static void ChararcterAwake(Character __instance)
    {
        if (Plugin.LogCharacters.Value)
        {
            if (!CustomCharactersList.Contains(__instance.name))
            {
                CustomCharactersList.Add(__instance.name);
                Plugin.Log.LogInfo($"[CHARACTER] {__instance.name}: ({__instance.chaseSpeed}RS), ({__instance.idleWalkSpeed}WS), ({__instance.maxHealth}MaxHP)");
            }
        }
        if (Plugin.CustomCharacters.TryGetValue(__instance.name, out JToken Stats))
        {
            foreach (var Stat in Stats.Children())
            {
                switch (Stat.Path)
                {
                    case "health":
                        __instance.maxHealth = float.Parse(Stat.ToString());
                        __instance.health = float.Parse(Stat.ToString());
                        break;
                    case "walkspeed":
                        __instance.idleWalkSpeed = float.Parse(Stat.ToString());
                        break;
                    case "runspeed":
                        __instance.chaseSpeed = float.Parse(Stat.ToString());
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
