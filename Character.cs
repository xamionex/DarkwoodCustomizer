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
                    case "speed":
                        __instance.speedModifier = Stat.Value;
                        break;
                    case "damage":
                        //__instance.damage = Stat.Value;
                        break;
                    default:
                        Plugin.Log.LogInfo($"Customized Character {__instance.name} has unknown stat {Stat.Key}");
                        break;
                }
            }
        }
        if (Plugin.LogCharacters.Value)
        {
            if (!CustomCharactersList.Contains(__instance.name))
            {
                CustomCharactersList.Add(__instance.name);
                Plugin.LogDivider();
                Plugin.Log.LogInfo("Since the logging option was enabled I have seen Characters:");
                Plugin.LogDivider();
                foreach (var character in CustomCharactersList)
                {
                    Plugin.Log.LogInfo(character);
                }
                Plugin.LogDivider();
            }
        }
    }

    [HarmonyPatch(typeof(Character), nameof(Character.init))]
    [HarmonyPrefix]
    public static void CharInit(Character __instance)
    {
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
                    case "speed":
                        __instance.speedModifier = Stat.Value;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
