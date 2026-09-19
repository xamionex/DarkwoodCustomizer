using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
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

  // Size the workbench recipe grid ends up with once Inventory.show() has run (see InventoryPatch.InventorySlots).
  // Inventory.show() runs AFTER Workbench.setRecipes() inside Workbench.open(), so on the first open of a session the grid is still at its vanilla size while setRecipes() fills it.
  // We grow it early so the slot count is already correct.
  private static int ConfiguredGridSlots() =>
    Plugin.CraftingModification.Value
      ? Plugin.CraftingRightSlots.Value * Plugin.CraftingDownSlots.Value
      : 35; // 5*7 vanilla

  private static string TypeOf(CraftingRecipes recipes)
  {
    if (recipes == null) return "<null>";
    var invItem = recipes.GetComponent<InvItem>();
    return invItem == null ? "<no InvItem>" : invItem.type;
  }

  // Debug only: dumps every recipe entry of every level so name/type mix-ups are visible in the log.
  private static void LogWorkbenchState(Workbench instance, string when)
  {
    if (!Plugin.LogWorkbench.Value) return;
    Plugin.Log.LogInfo($"[WB-Diag] {when}: workbench '{instance.name}', currentLevel={instance.currentLevel}, recipe grid slots={instance.workbenchInventory.slots.Count}");
    for (var i = 0; i < instance.levels.Count; i++)
    {
      var level = instance.levels[i];
      foreach (var recipes in level.recipes)
      {
        if (recipes == null)
        {
          Plugin.Log.LogInfo($"[WB-Diag]   idx {i} (Level.level={level.level}): <null/destroyed recipe entry>");
          continue;
        }
        var type = TypeOf(recipes);
        var mismatch = recipes.name == type ? "" : "   <-- object name != item type";
        Plugin.Log.LogInfo($"[WB-Diag]   idx {i} (Level.level={level.level}): obj='{recipes.name}' type='{type}' recipes={recipes.recipes.Count}{mismatch}");
      }
    }
  }

  // Runs AFTER WorkbenchRecipes (Priority.Last) so the custom recipes are already counted, and grows the grid first.
  // Previously this ran first: it counted only the vanilla recipes (passes), WorkbenchRecipes then injected more, and the original setRecipes() ran out of slots (getNextFreeSlot() == null -> exception, workbench doesn't open).
  // The next attempt then saw the real count against the un-grown grid and skipped setRecipes() -> empty crafting menu.
  [HarmonyPatch(typeof(Workbench), nameof(Workbench.setRecipes))]
  [HarmonyPrefix]
  [HarmonyPriority(Priority.Last)]
  // ReSharper disable once InconsistentNaming
  private static bool WorkbenchSetRecipesSafety(Workbench __instance)
  {
    var grid = __instance.workbenchInventory;

    var target = ConfiguredGridSlots();
    if (target > grid.slots.Count)
    {
      if (Plugin.LogWorkbench.Value)
        Plugin.Log.LogInfo($"[WB-Diag] Growing recipe grid of '{__instance.name}' from {grid.slots.Count} to {target} slots before setRecipes()");
      InventoryPatch.ChangeSlots(grid, target);
    }

    var recipeBookSlots = Player.Instance.Crafting.slots.Count(t => !InvItemClass.isNull(t.invItem) && t.invItem.baseClass.GetComponent<CraftingRecipes>() != null);
    var levelSlots = __instance.levels.Where(t => t.level <= __instance.currentLevel + 1).Sum(t => t.recipes.Where(t1 => t1 != null).Sum(t1 => t1.recipes.Count));
    var requiredSlots = recipeBookSlots + levelSlots;

    if (Plugin.LogWorkbench.Value)
      Plugin.Log.LogInfo($"[WB-Diag] setRecipes() on '{__instance.name}': needs {requiredSlots} slots (recipe book {recipeBookSlots} + level recipes {levelSlots}), grid has {grid.slots.Count}");

    if (requiredSlots <= grid.slots.Count) return true;
    Plugin.Log.LogError($"Workbench '{__instance.name}' requires {requiredSlots} slots but workbenchInventory only has {grid.slots.Count}. Skipping setRecipes() to prevent item duplication/corruption. If you changed Crafting slot sizes, try increasing them or resetting to defaults.");
    return false;
  }

  [HarmonyPatch(typeof(Workbench), nameof(Workbench.setRecipes))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  public static void WorkbenchRecipes(Workbench __instance)
  {
    if (!Plugin.CraftingRecipesModification.Value) return;
    _onFirst = true;
    if (Plugin.CustomCraftingRecipesUseDefaults.Value)
    {
      _logTypeFlag = "[DefaultCustomCraftingRecipes]";
      foreach (var recipeProperty in Plugin.DefaultCustomCraftingRecipes.Properties())
        if (recipeProperty.Value is JObject recipeObject) TryAddRecipe(recipeProperty.Name, recipeObject, __instance);
    }
    _logTypeFlag = "[UserCustomCraftingRecipes]";
    foreach (var recipeProperty in Plugin.CustomCraftingRecipes.Properties())
      if (recipeProperty.Value is JObject recipeObject) TryAddRecipe(recipeProperty.Name, recipeObject, __instance);
    Chapter2LoadOnNextOpen = true;
    LogWorkbenchState(__instance, "after recipe injection");
  }

  // One bad entry must never be able to stop the workbench from opening.
  private static void TryAddRecipe(string itemName, JObject recipeObject, Workbench instance)
  {
    try
    {
      WorkbenchCraftingAddRecipe(itemName, recipeObject, instance);
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Failed to add recipe '{itemName}', skipping it: {e}");
    }
  }

  private static void WorkbenchCraftingAddRecipe(string itemName, JObject recipeObject, Workbench instance)
  {
    if (Chapter2Restricted.Contains(itemName) && !Chapter2LoadOnNextOpen)
    {
      Plugin.Log.LogInfo($"Skipping {itemName} since chapter 2 has to yet load it, open the workbench again for it to load");
      return;
    }

    var levelCount = Math.Min(8, instance.levels.Count);

    // An entry whose GameObject name equals the recipe key counts as "already there".
    // NOTE: this matches on the GameObject name, NOT on the item type, so the log below prints both.
    for (var i = 0; i < levelCount; i++)
    {
      var existing = instance.levels[i].recipes.FirstOrDefault(r => r != null && r.name == itemName);
      if (existing == null) continue;
      if (Plugin.LogWorkbench.Value)
      {
        var existingType = TypeOf(existing);
        Plugin.Log.LogInfo($"{_logTypeFlag} Skipping {itemName}, already present in workbench (level idx {i}, obj='{existing.name}', type='{existingType}').");
        if (existingType != itemName)
          Plugin.Log.LogWarning($"{_logTypeFlag} [WB-Diag] '{itemName}' was skipped because an existing recipe object is NAMED '{itemName}' but produces '{existingType}'. The custom recipe for '{itemName}' was NOT applied.");
      }
      return;
    }

    var itemResource = recipeObject["icon"]?.Value<string>() ?? recipeObject["resource"]?.Value<string>() ?? itemName;
    var requiredLevel = recipeObject["requiredlevel"]?.Value<int>() ?? 1;
    var levelToAddTo = requiredLevel - 1;
    var requirementsToken = recipeObject["requirements"];

    if (levelToAddTo < 0 || levelToAddTo >= levelCount)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Recipe '{itemName}' has requiredlevel {requiredLevel}, which must be between 1 and {levelCount}. Skipping it.");
      return;
    }

    if (_onFirst) CustomizedRecipes.Clear();
    _onFirst = false;

    var itemResourceObject = LoadResource(itemResource, true);
    if (itemResourceObject == null)
    {
      // ItemsDatabase.getItem() logs "No item type X" and returns null for unknown ids, so check first.
      if (!ItemsDatabase.Instance.hasItem(itemName))
      {
        Plugin.Log.LogError($"{_logTypeFlag} Item '{itemName}' does not exist, skipping recipe.");
        return;
      }
      itemResourceObject = ItemsDatabase.Instance.getItem(itemName, false)?.gameObject;
    }
    else
    {
      Plugin.Log.LogWarning($"{_logTypeFlag} Item {itemName} is unused and will not be loaded!");
      if (!Plugin.CraftingUnusedContinue.Value) return;
      Plugin.Log.LogWarning($"{_logTypeFlag} Trying to load {itemName} anyway because trying to load unused is enabled!");
    }

    if (itemResourceObject == null)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Item {itemName} does not exist, skipping recipe.");
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

        var item = ItemsDatabase.Instance.getItem(requirementItemName);

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

    // Remove any existing entry with the same GameObject name from every level (so the recipe only lives at its new level).
    var prefabName = itemPath.name;
    for (var i = 0; i < levelCount; i++)
    {
      var recipes = instance.levels[i].recipes;
      var index = recipes.FindIndex(r => r != null && r.name == prefabName);
      if (index == -1) continue;
      if (Plugin.LogWorkbench.Value)
      {
        var removed = recipes[index];
        if (ReferenceEquals(removed, itemPath))
          Plugin.Log.LogInfo($"{_logTypeFlag} [WB-Diag] Re-adding '{itemName}': removing its previous entry from level idx {i}");
        else
          Plugin.Log.LogWarning($"{_logTypeFlag} [WB-Diag] Adding '{itemName}' REMOVED a different recipe entry from level idx {i}: obj='{removed.name}' type='{TypeOf(removed)}' (prefab name '{prefabName}')");
      }
      recipes.RemoveAt(index);
    }

    if (Plugin.LogWorkbench.Value)
      Plugin.Log.LogInfo($"{_logTypeFlag} Added recipe of {itemName} (obj='{itemPath.name}', type='{TypeOf(itemPath)}') with {CustomizedRecipes[itemName].recipes[0].requirements.Count} requirements at level {requiredLevel} (index {levelToAddTo}) workbench");
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
      resourcePaths = [$"InventoryItems_NotUsed/{itemName}"];
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