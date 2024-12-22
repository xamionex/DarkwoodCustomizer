using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class WorkbenchPatch
{
  private static readonly List<string> _chapter2Restricted =
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
  private static string LogTypeFlag = "";
  private static bool OnFirst;

  [HarmonyPatch(typeof(Workbench), nameof(Workbench.setRecipes))]
  [HarmonyPrefix]
  public static void WorkbenchRecipes(Workbench __instance)
  {
    if (!Plugin.CraftingRecipesModification.Value) return;
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
    Chapter2LoadOnNextOpen = true;
  }

  public static void WorkbenchCraftingAddRecipe(string ItemName, JObject RecipeObject, Workbench instance)
  {
    if (_chapter2Restricted.Contains(ItemName) && !Chapter2LoadOnNextOpen)
    {
      Plugin.Log.LogInfo($"Skipping {ItemName} since chapter 2 has to yet load it, open the workbench again for it to load");
      return;
    }
    
    var ItemResource = RecipeObject["icon"]?.Value<string>() ?? RecipeObject["resource"]?.Value<string>();
    var LevelToAddTo = RecipeObject["requiredlevel"]?.Value<int>() - 1 ?? 0;
    var RequirementsToken = RecipeObject["requirements"];

    if (OnFirst) CustomizedRecipes.Clear();
    OnFirst = false;

    GameObject ItemResourceObject = LoadResource(ItemResource, true);
    if (ItemResourceObject == null)
    {
      ItemResourceObject = ItemsDatabase.Instance.getItem(ItemName, false).gameObject;
    }
    else
    {
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

    CustomizedRecipesLog[ItemName] = ItemPath;
    CustomizedRecipes[ItemName] = ItemPath;

    CustomizedRecipes[ItemName].recipes = [new CraftingRecipes.Recipe { requirements = [] }];
    CustomizedRecipes[ItemName].recipes[0].produceAmount = RecipeObject["givesamount"]?.Value<int>() ?? 1;

    if (RequirementsToken != null)
    {
      foreach (var requirement in RequirementsToken.Children<JProperty>())
      {
        var itemName = requirement.Name;

        var item = ItemsDatabase.Instance.getItem(itemName, true);

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
