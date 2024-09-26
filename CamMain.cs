using DarkwoodCustomizer;
using HarmonyLib;

public class CamMainPatch
{
    public static bool RefreshCamera = true;

    [HarmonyPatch(typeof(CamMain), "Update")]
    [HarmonyPostfix]
    public static void CamUpdate(CamMain __instance)
    {
        if (Plugin.CameraModification.Value && RefreshCamera)
        {
            Singleton<CamMain>.Instance.GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            Singleton<CamMain>.Instance.transform.Find("CamLights").GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            Singleton<CamMain>.Instance.transform.Find("CamFOV").GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            Singleton<CamMain>.Instance.transform.Find("CamAmbient").GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            Singleton<CamMain>.Instance.transform.Find("CamLightning").GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            Singleton<CamMain>.Instance.transform.Find("CamTopMask").GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            RefreshCamera = false;
        }
    }
}