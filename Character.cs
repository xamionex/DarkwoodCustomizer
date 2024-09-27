using System.Collections.Generic;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

public class CharacterPatch
{
    public static List<string> CustomCharactersList = [];

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
                    case "damage":
                        //__instance.damage = float.Parse(Stat.ToString());
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
