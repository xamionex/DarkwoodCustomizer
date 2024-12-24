using HarmonyLib;

namespace DarkwoodCustomizer;

internal class WorldGeneratorPatch
{
    [HarmonyPatch(typeof(WorldGenerator), "onFinished")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    private static void WorldGeneratorLoad(WorldGenerator __instance)
    {
        Plugin.Log.LogInfo("World was loaded");
        WorkbenchPatch.Chapter2LoadOnNextOpen = __instance.chapterID != 2;
    }
}