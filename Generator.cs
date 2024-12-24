using HarmonyLib;

namespace DarkwoodCustomizer;

public static class GeneratorPatch
{
  public static bool RefreshGenerator = true;

  [HarmonyPatch(typeof(Generator), nameof(Generator.drainFuel))]
  [HarmonyPrefix]
  public static void DrainPatch(Generator instance)
  {
    if (Plugin.GeneratorModification.Value)
    {
      if (RefreshGenerator)
      {
        Player.Instance.electricityModifier = Plugin.GeneratorModifier.Value;
      }
      if (Plugin.GeneratorInfiniteFuel.Value)
      {
        instance.fuel = instance.maxFuel;
      }
    }
  }
}