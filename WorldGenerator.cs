using HarmonyLib;

namespace DarkwoodCustomizer;

internal class WorldGeneratorPatch
{
    [HarmonyPatch(typeof(WorldGenerator), "onFinished")]
    [HarmonyPatch(MethodType.Normal)]
    [HarmonyPostfix]
    private static void WorldGeneratorLoad(WorldGenerator instance)
    {
        Plugin.Log.LogInfo("World was loaded");
        WorkbenchPatch.Chapter2LoadOnNextOpen = instance.chapterID != 2;
    }
}