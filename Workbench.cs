using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCustomizer;

public class WorkbenchPatch
{
    private static readonly Dictionary<string, CraftingRecipes> CustomizedRecipes = [];

    [HarmonyPatch(typeof(Workbench), nameof(Workbench.setRecipes))]
    [HarmonyPrefix]
    public static void ItemPatch(Workbench __instance)
    {
        if (!Plugin.WorkbenchModification.Value) return;
        foreach (var RecipeProperty in Plugin.CustomCraftingRecipes.Properties())
        {
            if (!CustomizedRecipes.TryGetValue(RecipeProperty.Name, out _))
            {
                var RecipeObject = RecipeProperty.Value as JObject;
                if (RecipeObject != null)
                {
                    WorkbenchCraftingAddRecipe(RecipeProperty.Name, RecipeObject, __instance);
                }
                else
                {
                    CustomizedRecipes.Add(RecipeProperty.Name, (Resources.Load(Singleton<ItemsDatabase>.Instance.recipesDict[RecipeProperty.Name]) as GameObject).GetComponent<CraftingRecipes>());
                }
            }
        }
    }

    public static void WorkbenchCraftingAddRecipe(string ItemName, JObject RecipeObject, Workbench instance)
    {
        var ItemIcon = RecipeObject["icon"]?.Value<string>();
        var LevelToAddTo = RecipeObject["requiredlevel"]?.Value<int>() ?? 0;
        var RequirementsToken = RecipeObject["requirements"];

        if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"Adding recipe of {ItemName}");

        CustomizedRecipes.Add(ItemName, (Resources.Load(ItemIcon ?? "inventoryitems/meleeweapons/knife") as GameObject).AddComponent<CraftingRecipes>());
        CustomizedRecipes[ItemName].recipes = [new CraftingRecipes.Recipe { requirements = [] }];

        if (RequirementsToken != null)
        {
            if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo("Looping through requirements");
            foreach (var requirement in RequirementsToken.Children<JProperty>())
            {
                var itemName = requirement.Name;
                var amount = requirement.Value.Value<int>();

                if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"Adding requirement of {itemName} for {amount} amount");
                var item = Singleton<ItemsDatabase>.Instance.getItem(itemName, true);

                if (item == null)
                {
                    if (Plugin.LogWorkbench.Value) Plugin.Log.LogError($"Item {itemName} does not exist!");
                    continue;
                }

                if (!item.stackable && item.maxDurability > 0)
                {
                    if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"Adding durability requirement of {itemName} for {amount} amount");
                    CustomizedRecipes[ItemName].recipes[0].requirements.Add(new CraftingRequirement

                    {
                        item = item,
                        durabilityAmount = amount
                    });
                }
                else
                {
                    if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"Adding amount requirement of {itemName} for {amount} amount");
                    CustomizedRecipes[ItemName].recipes[0].requirements.Add(new CraftingRequirement
                    {
                        item = item,
                        amount = amount
                    });
                }
            }
        }

        if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"Added recipe of {ItemName} with {CustomizedRecipes[ItemName].recipes[0].requirements.Count} requirements at level {LevelToAddTo} workbench");
        instance.levels[LevelToAddTo].recipes.Add(CustomizedRecipes[ItemName]);
    }
}
