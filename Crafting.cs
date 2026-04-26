using HarmonyLib;

namespace DarkwoodCustomizer;

internal static class CraftingPatch
{
    // Prevents the game from consuming items when crafting
    [HarmonyPatch(typeof(CraftingRecipes.Recipe), nameof(CraftingRecipes.Recipe.removeIngredients))]
    [HarmonyPrefix]
    private static bool Prefix_RemoveIngredients()
    {
        return !Plugin.FreeCrafting.Value;
    }

    [HarmonyPatch(typeof(CraftingRequirement), "refresh")]
    [HarmonyPostfix] 
    // ReSharper disable once InconsistentNaming
    private static void Postfix_CraftingRequirement_Refresh(CraftingRequirement __instance)
    {
        if (Plugin.FreeCrafting.Value)
        {
            // Force the requirement to always be met
            __instance.met = true;
        }
    }
}