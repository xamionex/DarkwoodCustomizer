using System;
using HarmonyLib;

namespace DarkwoodCustomizer;

internal class UIPatch
{
    private static bool _previousUiState;
    private static readonly (string Name, bool Previous, Func<bool> Current)[] Elements = 
    {
        ("HealthBar", false, () => !Plugin.UIDisabledHealthBar.Value),
        ("Lives", false, () => !Plugin.UIDisabledLives.Value),
        ("StaminaBar", false, () => !Plugin.UIDisabledStaminaBar.Value),
        ("Skillbar", false, () => !Plugin.UIDisabledSkillbar.Value)
    };

    [HarmonyPatch(typeof(UI), "Update")]
    [HarmonyPostfix]
    public static void UIUpdate(UI __instance)
    {
        if (!Plugin.UIModification.Value) return;

        // Handle main UI toggle
        if (Plugin.UIDisabled.Value != _previousUiState)
        {
            if (Plugin.UIDisabled.Value)
                __instance.hidePlayerUI();
            else
                __instance.showPlayerUI();
            
            _previousUiState = Plugin.UIDisabled.Value;
        }

        // Handle individual elements
        var barsTransform = __instance.bars.transform;
        
        for (int i = 0; i < Elements.Length; i++)
        {
            var element = Elements[i];
            bool currentState = element.Current();
            
            if (currentState != element.Previous)
            {
                var obj = barsTransform.Find(element.Name).gameObject;
                obj.SetActive(currentState);
                
                // Handle Skillbar's additional Sprite0
                if (element.Name == "Skillbar")
                    barsTransform.Find("Sprite0").gameObject.SetActive(currentState);
                
                Elements[i].Previous = currentState;
            }
        }
    }
}