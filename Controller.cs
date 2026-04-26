using HarmonyLib;

namespace DarkwoodCustomizer;

internal class ControllerPatch
{
  [HarmonyPatch(typeof(Controller), nameof(Controller.refreshTime))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void PatchTime(Controller __instance)
  {
    if (!Plugin.TimeModification.Value) return;
    if (Plugin.TimeStop.Value)
    {
      __instance.CurrentTime -= 1;
    }
    if (Plugin.UseCurrentTime.Value)
    {
      __instance.CurrentTime = Plugin.CurrentTime.Value;
    }
    if (Plugin.ResetWell.Value)
    {
      Player.Instance.fedToday = false;
    }
  }

  [HarmonyPatch(typeof(Controller), nameof(Controller.setTimeChangeInterval))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void PatchTimeInterval(Controller __instance)
  {
    // Lower value is slower time interval
    if (!Plugin.TimeModification.Value) return;
    if (__instance.CurrentTime < __instance.nightTime)
    {
      __instance.timeChangeInterval = Plugin.DaytimeFlow.Value;
      return;
    }
    __instance.timeChangeInterval = Plugin.NighttimeFlow.Value;
  }
}