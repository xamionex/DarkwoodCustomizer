using System.Collections.Generic;
using DarkwoodCustomizer;
using HarmonyLib;

public class CharacterPatch
{
    public static List<string> CustomCharactersList = new List<string>();

    [HarmonyPatch(typeof(Character), nameof(Character.Update))]
    [HarmonyPrefix]
    public static void CharUpdate(Character __instance)
    {
        if (Plugin.CustomCharacters.TryGetValue(__instance.name, out Dictionary<string, float> Stats))
        {
            foreach (var Stat in Stats)
            {
                switch (Stat.Key)
                {
                    case "health":
                        __instance.maxHealth = Stat.Value;
                        break;
                    case "walkspeed":
                        __instance.idleWalkSpeed = Stat.Value;
                        break;
                    case "runspeed":
                        __instance.chaseSpeed = Stat.Value;
                        break;
                    case "damage":
                        //__instance.damage = Stat.Value;
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
        if (Plugin.CustomCharacters.TryGetValue(__instance.name, out Dictionary<string, float> Stats))
        {
            foreach (var Stat in Stats)
            {
                switch (Stat.Key)
                {
                    case "health":
                        __instance.maxHealth = Stat.Value;
                        __instance.health = Stat.Value;
                        break;
                    case "walkspeed":
                        __instance.idleWalkSpeed = Stat.Value;
                        break;
                    case "runspeed":
                        __instance.chaseSpeed = Stat.Value;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
