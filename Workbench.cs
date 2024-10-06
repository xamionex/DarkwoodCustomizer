using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class WorkbenchPatch
{
    private static readonly Dictionary<string, CraftingRecipes> CustomizedRecipesLog = [];
    private static readonly Dictionary<string, CraftingRecipes> CustomizedRecipes = [];
    private static string LogTypeFlag = "";
    private static bool OnFirst;

    [HarmonyPatch(typeof(Workbench), nameof(Workbench.setRecipes))]
    [HarmonyPrefix]
    public static void WorkbenchRecipes(Workbench __instance)
    {
        if (!Plugin.CraftingModification.Value) return;
        OnFirst = true;
        if (Plugin.CustomCraftingRecipesUseDefaults.Value)
        {
            LogTypeFlag = "[DEFAULTCUSTOMRECIPES]";
            foreach (var RecipeProperty in Plugin.DefaultCustomCraftingRecipes.Properties())
                if (RecipeProperty.Value is JObject RecipeObject) WorkbenchCraftingAddRecipe(RecipeProperty.Name, RecipeObject, __instance);
        }
        LogTypeFlag = "[USERCUSTOMRECIPES]";
        foreach (var RecipeProperty in Plugin.CustomCraftingRecipes.Properties())
            if (RecipeProperty.Value is JObject RecipeObject) WorkbenchCraftingAddRecipe(RecipeProperty.Name, RecipeObject, __instance);
    }

    public static void WorkbenchCraftingAddRecipe(string ItemName, JObject RecipeObject, Workbench instance)
    {
        var ItemResource = RecipeObject["icon"]?.Value<string>() ?? RecipeObject["resource"]?.Value<string>();
        var LevelToAddTo = RecipeObject["requiredlevel"]?.Value<int>() - 1 ?? 0;
        var RequirementsToken = RecipeObject["requirements"];

        if (OnFirst) CustomizedRecipes.Clear();
        OnFirst = false;

        GameObject ItemResourceObject = LoadResource(ItemResource, true);
        if (ItemResourceObject == null)
        {
            ItemResourceObject = LoadResource(ItemResource);
        }
        else
        {
            _ = Singleton<ItemsDatabase>.Instance.getItem(ItemName, true);
            Plugin.Log.LogWarning($"{LogTypeFlag} Item {ItemName} is unused and will not be loaded!");
            if (!Plugin.CraftingUnusedContinue.Value) return;
            Plugin.Log.LogWarning($"{LogTypeFlag} Trying to load {ItemName} anyway because trying to load unused is enabled!");
        }

        if (ItemResourceObject == null)
        {
            Plugin.Log.LogError($"{LogTypeFlag} Item {ItemName} does not exist, stopping as to not break the plugin!");
            return;
        }

        CraftingRecipes ItemPath = ItemResourceObject.GetComponent<CraftingRecipes>() ?? ItemResourceObject.AddComponent<CraftingRecipes>();

        if (CustomizedRecipesLog.ContainsKey(ItemName))
            CustomizedRecipesLog[ItemName] = ItemPath;
        else
            CustomizedRecipesLog.Add(ItemName, ItemPath);

        if (CustomizedRecipes.ContainsKey(ItemName))
            CustomizedRecipes[ItemName] = ItemPath;
        else
            CustomizedRecipes.Add(ItemName, ItemPath);

        CustomizedRecipes[ItemName].recipes = [new CraftingRecipes.Recipe { requirements = [] }];
        CustomizedRecipes[ItemName].recipes[0].produceAmount = RecipeObject["givesamount"]?.Value<int>() ?? 1;

        if (RequirementsToken != null)
        {
            foreach (var requirement in RequirementsToken.Children<JProperty>())
            {
                var itemName = requirement.Name;

                var item = Singleton<ItemsDatabase>.Instance.getItem(itemName, true);

                if (item == null)
                {
                    if (Plugin.LogWorkbench.Value) Plugin.Log.LogError($"{LogTypeFlag} Item {itemName} does not exist!");
                    continue;
                }

                if (item.maxDurability > 0 && requirement.Value.Value<string>().Contains("."))
                {
                    CustomizedRecipes[ItemName].recipes[0].requirements.Add(new CraftingRequirement
                    {
                        item = item,
                        durabilityAmount = requirement.Value?.Value<float>() ?? 0.5f
                    });
                }
                else
                {
                    CustomizedRecipes[ItemName].recipes[0].requirements.Add(new CraftingRequirement
                    {
                        item = item,
                        amount = requirement.Value?.Value<int>() ?? 1
                    });
                }
            }
        }

        for (int i = 0; i < 8; i++)
        {
            int index = instance.levels[i].recipes.FindIndex(r => r.name == CustomizedRecipesLog[ItemName].name);
            if (index != -1)
            {
                instance.levels[i].recipes.RemoveAt(index);
            }
        }

        if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"{LogTypeFlag} Added recipe of {ItemName} with {CustomizedRecipes[ItemName].recipes[0].requirements.Count} requirements at level {LevelToAddTo} workbench");
        instance.levels[LevelToAddTo].recipes.Add(CustomizedRecipes[ItemName]);
    }

    public static GameObject LoadResource(string itemName, bool UnusedOnly = false)
    {
        var resourcePaths = new[]
        {
            $"InventoryItems/{itemName}",
            $"InventoryItems_NotUsed/{itemName}",
        };
        if (UnusedOnly)
        {
            resourcePaths = new[]
            {
                $"InventoryItems_NotUsed/{itemName}",
            };
        }
        foreach (var resourcePath in resourcePaths)
        {
            try
            {
                var resource = Resources.Load<GameObject>(resourcePath);
                if (resource != null)
                {
                    return resource;
                }
            }
            catch (Exception)
            {
                // Ignore exceptions when loading resources
            }
        }

        // If we're only looking for unused items it should exit above, if it doesnt that means we're looking for an item that exists in the game
        if (UnusedOnly) return null;

        var categories = new[]
        {
            "Materials",
            "Misc",
            "Ammo",
            "Consumables",
            "Useable",
            "FireArms",
            "MeleeWeapons",
            "Traps",
            "ThrownItems",
            "ExpObjs",
            "Home",
        };
        foreach (var category in categories)
        {
            string resourcePath = $"InventoryItems/{category}/{itemName}";
            try
            {
                var resource = Resources.Load<GameObject>(resourcePath);
                if (resource != null)
                {
                    return resource;
                }
            }
            catch (Exception)
            {
                // Ignore exceptions when loading resources
            }
        }

        return null;
    }
}
