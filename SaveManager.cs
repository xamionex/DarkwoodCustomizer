using DarkwoodCustomizer;
using HarmonyLib;

public class SaveManagerPatch
{
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Load))]
    [HarmonyPostfix]
    public static void SaveManagerLoad(SaveManager __instance)
    {
        Plugin.Log.LogInfo("Save was loaded!");
        PlayerPatch.RefreshPlayer = true;
        PlayerPatch.SaveLoaded = true;
    }
}