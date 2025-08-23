using HarmonyLib;

namespace DarkwoodCustomizer;

internal class CamMainPatch
{
    private static bool _previousPostFxState;
    private static bool _previousVignetteState;
    private static float _previousFoV;
    
    // Cache child camera names to avoid repeated string operations
    private static readonly string[] ChildCameraNames = 
    {
        "CamLights",
        "CamFOV",
        "CamAmbient",
        "CamLightning",
        "CamTopMask"
    };
    
    [HarmonyPatch(typeof(CamMain), "Update")]
    [HarmonyPostfix]
    public static void CamUpdate(CamMain __instance)
    {
        if (!Plugin.CameraModification.Value) return;
        
        // Cache the main camera component
        var mainCamera = __instance.GetComponent<tk2dCamera>();
        
        // Handle FoV changes
        if (mainCamera.ZoomFactor != Plugin.CameraFoV.Value || _previousFoV != Plugin.CameraFoV.Value)
        {
            mainCamera.ZoomFactor = Plugin.CameraFoV.Value;
            
            // Update all child cameras
            var instanceTransform = __instance.transform;
            foreach (var cameraName in ChildCameraNames)
            {
                instanceTransform.Find(cameraName).GetComponent<tk2dCamera>().ZoomFactor = Plugin.CameraFoV.Value;
            }
            
            _previousFoV = Plugin.CameraFoV.Value;
        }
        
        // Cache the PostFX transform
        var postFxTransform = __instance.transform.Find("CamPostFX");
        
        if (_previousPostFxState != Plugin.CameraDisablePostFX.Value)
        {
            postFxTransform.GetComponent<AmplifyColorEffect>().enabled = !Plugin.CameraDisablePostFX.Value;
            _previousPostFxState = Plugin.CameraDisablePostFX.Value;
        }
        if (_previousVignetteState != Plugin.CameraDisableVignette.Value)
        {
            postFxTransform.GetComponent<Vignetting>().enabled = !Plugin.CameraDisableVignette.Value;
            _previousVignetteState = Plugin.CameraDisableVignette.Value;
        }
    }
}