using HarmonyLib;

namespace DarkwoodCustomizer;

internal class ControllerPatch
{
  [HarmonyPatch(typeof(Controller), nameof(Controller.refreshTime))]
  [HarmonyPostfix]
  public static void PatchTime(Controller instance)
  {
    if (Plugin.TimeModification.Value)
    {
      if (Plugin.TimeStop.Value)
      {
        instance.CurrentTime -= 1;
      }
      if (Plugin.UseCurrentTime.Value)
      {
        instance.CurrentTime = Plugin.CurrentTime.Value;
      }
      if (Plugin.ResetWell.Value)
      {
        Player.Instance.fedToday = false;
      }
    }
  }

  [HarmonyPatch(typeof(Controller), nameof(Controller.setTimeChangeInterval))]
  [HarmonyPostfix]
  public static void PatchTimeInterval(Controller instance)
  {
    // Lower value is slower time interval
    if (Plugin.TimeModification.Value)
    {
      if (instance.CurrentTime < instance.nightTime)
      {
        instance.timeChangeInterval = Plugin.DaytimeFlow.Value;
        return;
      }
      instance.timeChangeInterval = Plugin.NighttimeFlow.Value;
    }
  }
}