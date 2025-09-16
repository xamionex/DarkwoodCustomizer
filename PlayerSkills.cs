using HarmonyLib;

namespace DarkwoodCustomizer;

internal class PlayerSkillsPatch
{
  [HarmonyPatch(typeof(PlayerSkill), "setfarsight")]
  [HarmonyPostfix]
  public static void SetFarsight(PlayerSkills __instance)
  {
    if (__instance.Farsight && Plugin.PlayerSkillsModification.Value)
    {
      Singleton<CamMain>.Instance.seeDistance = Plugin.PlayerFarsightDistance.Value;
    }
  }
}