using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class WorkbenchPatch
{
  private static readonly List<string> Chapter2Restricted =
  [
    "exp_bio2_meat_mutated",
    "note_truck_01",
    "map_bio2",
    "piotrek_parts_3",
    "piotrek_parts_6",
    "piotrek_parts_1",
    "krzyzyk_01",
    "weapon_pistol_01_pellet",
    "weapon_submachine_01_full",
    "ammo_single_pellet",
    "piotrek_parts_5",
    "note_pigshed_flyer",
    "note_pigshed_02",
    "plank_nails",
    "dead_rat",
    "jajeczka_wilk",
    "twins_church_01",
    "note_gagarin",
    "rocket_blueprint_01",
    "violin",
    "lesnyLudek",
    "piotrek_parts_2",
    "porterWhistle",
    "kartkiEkspedycja",
    "ring_wedding_babahunter",
    "hunter_photo_1",
    "weapon_flamethrower_homeMade",
    "note_macius_1",
    "magazynPan_01",
    "kremCzekoladowy",
    "doctor_wife_photo",
    "key_bunker_act1",
    "key_wolfmanHideout_01",
    "shawl_sister_01",
    "chicken_egg_red",
    "chain_well",
    "key_act1_biom3",
    "magazynPlomyk_01",
    "key_big_farm_02_shed",
    "cable",
    "map_bio3",
    "piotrek_parts_4"
  ];

  public static bool Chapter2LoadOnNextOpen;
  private static readonly Dictionary<string, CraftingRecipes> CustomizedRecipesLog = [];
  private static readonly Dictionary<string, CraftingRecipes> CustomizedRecipes = [];
  private static string _logTypeFlag = "";
  private static bool _onFirst;

  [HarmonyPatch(typeof(Workbench), nameof(Workbench.setRecipes))]
  [HarmonyPrefix]
  public static void WorkbenchRecipes(Workbench instance)
  {
    if (!Plugin.CraftingRecipesModification.Value) return;
    _onFirst = true;
    if (Plugin.CustomCraftingRecipesUseDefaults.Value)
    {
      _logTypeFlag = "[DEFAULTCUSTOMRECIPES]";
      foreach (var recipeProperty in Plugin.DefaultCustomCraftingRecipes.Properties())
        if (recipeProperty.Value is JObject recipeObject) WorkbenchCraftingAddRecipe(recipeProperty.Name, recipeObject, instance);
    }
    _logTypeFlag = "[USERCUSTOMRECIPES]";
    foreach (var recipeProperty in Plugin.CustomCraftingRecipes.Properties())
      if (recipeProperty.Value is JObject recipeObject) WorkbenchCraftingAddRecipe(recipeProperty.Name, recipeObject, instance);
    Chapter2LoadOnNextOpen = true;
  }

  public static void WorkbenchCraftingAddRecipe(string itemName, JObject recipeObject, Workbench instance)
  {
    if (Chapter2Restricted.Contains(itemName) && !Chapter2LoadOnNextOpen)
    {
      Plugin.Log.LogInfo($"Skipping {itemName} since chapter 2 has to yet load it, open the workbench again for it to load");
      return;
    }
    
    var itemResource = recipeObject["icon"]?.Value<string>() ?? recipeObject["resource"]?.Value<string>();
    var levelToAddTo = recipeObject["requiredlevel"]?.Value<int>() - 1 ?? 0;
    var requirementsToken = recipeObject["requirements"];

    if (_onFirst) CustomizedRecipes.Clear();
    _onFirst = false;

    var itemResourceObject = LoadResource(itemResource, true);
    if (itemResourceObject == null)
    {
      itemResourceObject = ItemsDatabase.Instance.getItem(itemName, false).gameObject;
    }
    else
    {
      Plugin.Log.LogWarning($"{_logTypeFlag} Item {itemName} is unused and will not be loaded!");
      if (!Plugin.CraftingUnusedContinue.Value) return;
      Plugin.Log.LogWarning($"{_logTypeFlag} Trying to load {itemName} anyway because trying to load unused is enabled!");
    }

    if (itemResourceObject == null)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Item {itemName} does not exist, stopping as to not break the plugin!");
      return;
    }

    var itemPath = itemResourceObject.GetComponent<CraftingRecipes>() ?? itemResourceObject.AddComponent<CraftingRecipes>();

    CustomizedRecipesLog[itemName] = itemPath;
    CustomizedRecipes[itemName] = itemPath;

    CustomizedRecipes[itemName].recipes = [new CraftingRecipes.Recipe { requirements = [] }];
    CustomizedRecipes[itemName].recipes[0].produceAmount = recipeObject["givesamount"]?.Value<int>() ?? 1;

    if (requirementsToken != null)
    {
      foreach (var requirement in requirementsToken.Children<JProperty>())
      {
        var requirementItemName = requirement.Name;

        var item = ItemsDatabase.Instance.getItem(requirementItemName, true);

        if (item == null)
        {
          if (Plugin.LogWorkbench.Value) Plugin.Log.LogError($"{_logTypeFlag} Item {requirementItemName} does not exist!");
          continue;
        }

        if (item.maxDurability > 0 && requirement.Value.Value<string>().Contains("."))
        {
          CustomizedRecipes[itemName].recipes[0].requirements.Add(new CraftingRequirement
          {
            item = item,
            durabilityAmount = requirement.Value?.Value<float>() ?? 0.5f
          });
        }
        else
        {
          CustomizedRecipes[itemName].recipes[0].requirements.Add(new CraftingRequirement
          {
            item = item,
            amount = requirement.Value?.Value<int>() ?? 1
          });
        }
      }
    }

    for (var i = 0; i < 8; i++)
    {
      var index = instance.levels[i].recipes.FindIndex(r => r.name == CustomizedRecipesLog[itemName].name);
      if (index != -1)
      {
        instance.levels[i].recipes.RemoveAt(index);
      }
    }

    if (Plugin.LogWorkbench.Value) Plugin.Log.LogInfo($"{_logTypeFlag} Added recipe of {itemName} with {CustomizedRecipes[itemName].recipes[0].requirements.Count} requirements at level {levelToAddTo} workbench");
    instance.levels[levelToAddTo].recipes.Add(CustomizedRecipes[itemName]);
  }

  public static GameObject LoadResource(string itemName, bool unusedOnly = false)
  {
    var resourcePaths = new[]
    {
            $"InventoryItems/{itemName}",
            $"InventoryItems_NotUsed/{itemName}",
        };
    if (unusedOnly)
    {
      resourcePaths = new[] { $"InventoryItems_NotUsed/{itemName}" };
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
    if (unusedOnly) return null;

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
      var resourcePath = $"InventoryItems/{category}/{itemName}";
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
