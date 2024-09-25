using DarkwoodCustomizer;
using HarmonyLib;

[HarmonyPatch]
public class CharacterPatch
{
    [HarmonyPatch(typeof(Character), nameof(Character.Update))]
    [HarmonyPrefix]
    public static void CharUpdate(Character __instance)
    {
        // hello :)
        // I am tired right now and will do this sometime later :D
        //Plugin.Log.LogInfo(__instance.name);
    }
}