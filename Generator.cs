using HarmonyLib;

namespace DarkwoodCustomizer;

public static class GeneratorPatch
{
  public static bool RefreshGenerator = true;

  [HarmonyPatch(typeof(Generator), nameof(Generator.drainFuel))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  public static void DrainPatch(Generator __instance)
  {
    if (!Plugin.GeneratorModification.Value) return;
    if (RefreshGenerator)
    {
      Player.Instance.electricityModifier = Plugin.GeneratorModifier.Value;
    }
    if (Plugin.GeneratorInfiniteFuel.Value)
    {
      __instance.fuel = __instance.maxFuel;
    }
  }
}