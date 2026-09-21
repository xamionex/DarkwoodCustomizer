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
  // ReSharper disable once CollectionNeverQueried.Global
  public static readonly Dictionary<string, CraftingRecipes> CustomizedRecipesLog = [];

  // The recipe this mod appended to an item prefab this session, per prefab.
  // The recipe component is shared with the item itself, so the mod's recipe is kept next to whatever the game ships (the workbench gives every recipe its own slot) and tracked here so re-opening the workbench skips it instead of adding it again.
  private static readonly Dictionary<CraftingRecipes, CraftingRecipes.Recipe> ModRecipes = [];

  // The recipes the game itself had on each item prefab, captured before this mod touches them.
  // A recipe with "replace": true empties them out, and this is what puts them back when the flag is turned off again or the config is edited.
  private static readonly Dictionary<CraftingRecipes, List<CraftingRecipes.Recipe>> VanillaRecipes = [];
  private static string _logTypeFlag = "";

  // Size the workbench recipe grid ends up with once Inventory.show() has run (see InventoryPatch.InventorySlots).
  // Inventory.show() runs AFTER Workbench.setRecipes() inside Workbench.open(), so on the first open of a session the grid is still at its vanilla size while setRecipes() fills it.
  // We grow it early so the slot count is already correct.
  private static int ConfiguredGridSlots() =>
    Plugin.CraftingModification.Value
      ? Plugin.CraftingRightSlots.Value * Plugin.CraftingDownSlots.Value
      : 35; // 5*7 vanilla

  private static string TypeOf(CraftingRecipes recipes)
  {
    if (!recipes) return "<null>";
    var invItem = recipes.GetComponent<InvItem>();
    return !invItem ? "<no InvItem>" : invItem.type;
  }

  // The item type living on a prefab, used to tell whether a resolved prefab is the item a recipe key asked for.
  private static string TypeOfPrefab(GameObject prefab)
  {
    if (!prefab) return "<null>";
    var invItem = prefab.GetComponent<InvItem>();
    return !invItem ? "<no InvItem>" : invItem.type;
  }

  // The asset name behind a table resource path, e.g. "InventoryItems/meleeWeapons/pitchfork" -> "pitchfork".
  private static string PrefabAssetName(string tablePath)
  {
    if (string.IsNullOrEmpty(tablePath)) return "";
    var trimmed = tablePath.TrimEnd('/');
    var index = trimmed.LastIndexOf('/');
    return index < 0 ? trimmed : trimmed.Substring(index + 1);
  }

  // Debug only: dumps every recipe entry of every level so name/type mix-ups are visible in the log.
  private static void LogWorkbenchState(Workbench instance, string when)
  {
    if (!Plugin.LogWorkbench.Value) return;
    Plugin.Log.LogInfo($"[WB-Diag] {when}: workbench '{instance.name}', scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}', currentLevel={instance.currentLevel}, recipe grid slots={instance.workbenchInventory.slots.Count}");
    for (var i = 0; i < instance.levels.Count; i++)
    {
      var level = instance.levels[i];
      foreach (var recipes in level.recipes)
      {
        if (!recipes)
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

    var recipeBookSlots = Player.Instance.Crafting.slots.Count(t => !InvItemClass.isNull(t.invItem) && t.invItem.baseClass.GetComponent<CraftingRecipes>());
    var levelSlots = __instance.levels.Where(t => t.level <= __instance.currentLevel + 1).Sum(t => t.recipes.Where(t1 => t1).Sum(t1 => t1.recipes.Count));
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
    SaveVanillaRecipes(__instance);
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

  // Writes the game's own workbench recipes into CustomCraftingRecipes.json, the same way the other custom files are populated, so they can be edited or replaced.
  // The entries are saved disabled with "replace": false, so nothing changes until one of them is enabled.
  private static void SaveVanillaRecipes(Workbench instance)
  {
    try
    {
      var added = 0;
      foreach (var level in instance.levels)
      {
        foreach (var entry in level.recipes.Where(entry => entry).Where(entry => !ModRecipes.ContainsKey(entry)))
        {
          if (!VanillaRecipes.TryGetValue(entry, out var vanillaRecipes))
          {
            vanillaRecipes = [.. entry.recipes];
            VanillaRecipes[entry] = vanillaRecipes;
          }

          var invItem = entry.GetComponent<InvItem>();
          if (!invItem || string.IsNullOrEmpty(invItem.type)) continue;
          if (Plugin.CustomCraftingRecipes.ContainsKey(invItem.type)) continue;
          if (vanillaRecipes.Count == 0) continue;

          // The config format holds one recipe per item, so the item's first recipe is saved.
          var recipe = vanillaRecipes[0];
          var requirements = new JObject();
          var duplicateRequirements = 0;
          foreach (var requirement in recipe.requirements.Where(requirement => requirement?.item))
          {
            if (requirements.ContainsKey(requirement.item.type))
            {
              duplicateRequirements++;
              continue;
            }
            requirements[requirement.item.type] = requirement.durabilityAmount > 0f
              ? new JValue(requirement.durabilityAmount)
              : new JValue(requirement.amount);
          }

          if (requirements.Count == 0) continue;

          Plugin.CustomCraftingRecipes[invItem.type] = new JObject
          {
            ["enabled"] = false,
            ["replace"] = false,
            ["requiredlevel"] = level.level,
            ["resource"] = invItem.type,
            ["givesamount"] = recipe.produceAmount,
            ["requirements"] = requirements
          };
          added++;

          if (duplicateRequirements > 0 && Plugin.LogWorkbench.Value)
            Plugin.Log.LogWarning($"{_logTypeFlag} The game's {invItem.type} recipe needs the same item more than once, only the first amount could be saved.");
        }
      }

      if (added == 0) return;
      Plugin.SaveCraftingRecipes = true;
      Plugin.Log.LogInfo($"{_logTypeFlag} Saved {added} game recipe(s) to CustomCraftingRecipes.json, they are disabled until you enable them");
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Failed to save the game's recipes: {e.Message}");
    }
  }

  private static void WorkbenchCraftingAddRecipe(string itemName, JObject recipeObject, Workbench instance)
  {
    // Entries apply unless the config disables them.
    // The entries this mod writes for the game's own recipes are saved disabled, so they stay inert until the user enables them.
    if (recipeObject["enabled"]?.Value<bool>() == false)
    {
      if (Plugin.LogWorkbench.Value)
        Plugin.Log.LogInfo($"{_logTypeFlag} Skipping {itemName}, it is disabled in the config.");
      return;
    }

    if (Chapter2Restricted.Contains(itemName) && !Chapter2LoadOnNextOpen)
    {
      Plugin.Log.LogInfo($"Skipping {itemName} since chapter 2 has to yet load it, open the workbench again for it to load");
      return;
    }

    var levelCount = Math.Min(8, instance.levels.Count);

    var itemResource = recipeObject["icon"]?.Value<string>() ?? recipeObject["resource"]?.Value<string>() ?? itemName;
    var requiredLevel = recipeObject["requiredlevel"]?.Value<int>() ?? 1;
    var levelToAddTo = requiredLevel - 1;
    var requirementsToken = recipeObject["requirements"];

    if (levelToAddTo < 0 || levelToAddTo >= levelCount)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Recipe '{itemName}' has requiredlevel {requiredLevel}, which must be between 1 and {levelCount}. Skipping it.");
      return;
    }

    var itemResourceObject = LoadResource(itemResource, true);
    if (!itemResourceObject)
    {
      // ItemsDatabase.getItem() logs "No item type X" and returns null for unknown ids, so check first.
      if (!ItemsDatabase.Instance.hasItem(itemName))
      {
        Plugin.Log.LogError($"{_logTypeFlag} Item '{itemName}' does not exist, skipping recipe.");
        return;
      }
      // Some game versions and item adding mods leave keys in the table without a prefab path, and Resources.Load() throws on a null path instead of returning null.
      var itemTablePath = ItemsDatabase.Instance.itemsDict.TryGetValue(itemName, out var itemResourcePath) ? itemResourcePath : null;
      if (string.IsNullOrEmpty(itemTablePath))
      {
        Plugin.Log.LogError($"{_logTypeFlag} Item '{itemName}' has no prefab in this game's item table, skipping recipe. Another mod may have added the key without an item.");
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

    if (!itemResourceObject)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Item {itemName} does not exist, stopping as to not break the plugin!");
      return;
    }

    // The prefab has to be the item that was asked for.
    // When the game's item table points a key at a different asset (some game builds map "pitchfork" onto the homemade flamethrower's prefab, for example), taking the prefab's recipe component below would overwrite that other item's recipe and move it to this level.
    // Two things are checked and either one passing is enough to continue:
    // the prefab's item type, and the asset name of the table's resource path.
    // Keys that resolve to a world object prefab (the mushroom plants behind the default recipes, for example) have an InvItem type of the item they hand out but their asset is named after the key, so the asset name covers those.
    var itemsDict = ItemsDatabase.Instance.itemsDict;
    var tablePath = itemsDict.TryGetValue(itemName, out var path) ? path : "";
    var assetName = PrefabAssetName(tablePath);
    var prefabType = TypeOfPrefab(itemResourceObject);
    if (prefabType != itemName && prefabType != itemResource && assetName != itemName && assetName != itemResource)
    {
      // Other keys pointing at the very same resource path: if any show up, the game's own table aliases these items.
      var sharedWith = string.Join(", ", itemsDict.Where(kv => kv.Value == tablePath && kv.Key != itemName).Select(kv => kv.Key));
      Plugin.Log.LogError($"{_logTypeFlag} Skipping '{itemName}': the game's item table maps it to prefab '{itemResourceObject.name}' (item type '{prefabType}', resource path '{tablePath}', other keys with the same path: [{sharedWith}], scene '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}'), which is a different item. Adding this recipe would overwrite the recipe of '{prefabType}'.");
      return;
    }

    // A prefab whose recipe already sits in the workbench belongs to a recipe the game shows, and when its item type is not the key that was asked for this key resolved to a different item's prefab.
    // Adding a recipe there would move and overwrite that item's recipe, which is what used to turn a pitchfork entry into the homemade flamethrower.
    var prefabRecipe = itemResourceObject.GetComponent<CraftingRecipes>();
    if (prefabRecipe && prefabType != itemName && prefabType != itemResource)
    {
      for (var i = 0; i < levelCount; i++)
      {
        if (!instance.levels[i].recipes.Any(r => ReferenceEquals(r, prefabRecipe))) continue;
        Plugin.Log.LogError($"{_logTypeFlag} Skipping '{itemName}': its prefab '{itemResourceObject.name}' has item type '{prefabType}' and already has a recipe in the workbench at level {i + 1}. This key is not a real item in this game version.");
        return;
      }
    }

    var itemPath = prefabRecipe ?? itemResourceObject.AddComponent<CraftingRecipes>();
    CustomizedRecipesLog[itemName] = itemPath;

    // Everything is resolved before the recipe is assigned: the component is shared with the item itself, so a recipe that fails halfway through must not leave the item with a half built recipe.
    var newRecipe = new CraftingRecipes.Recipe
    {
      requirements = [],
      produceAmount = recipeObject["givesamount"]?.Value<int>() ?? 1
    };
    var requirementProblems = 0;

    if (requirementsToken != null)
    {
      foreach (var requirement in requirementsToken.Children<JProperty>())
      {
        var requirementItemName = requirement.Name;

        if (!ItemsDatabase.Instance.hasItem(requirementItemName))
        {
          Plugin.Log.LogError($"{_logTypeFlag} Requirement '{requirementItemName}' of recipe '{itemName}' does not exist, leaving it out of the recipe.");
          requirementProblems++;
          continue;
        }

        var requirementPath = ItemsDatabase.Instance.itemsDict.TryGetValue(requirementItemName, out var requirementResourcePath) ? requirementResourcePath : null;
        if (string.IsNullOrEmpty(requirementPath))
        {
          Plugin.Log.LogError($"{_logTypeFlag} Requirement '{requirementItemName}' of recipe '{itemName}' has no prefab in this game's item table, leaving it out of the recipe.");
          requirementProblems++;
          continue;
        }

        var item = ItemsDatabase.Instance.getItem(requirementItemName);

        if (!item)
        {
          Plugin.Log.LogError($"{_logTypeFlag} Requirement '{requirementItemName}' of recipe '{itemName}' could not be loaded, leaving it out of the recipe.");
          requirementProblems++;
          continue;
        }

        if (item.type != requirementItemName)
        {
          // Still added below: a key that resolves to a prefab with another item type is how world objects work (the mushroom plants behind the default recipes hand out the mushroom item), but it is worth saying out loud because the recipe then needs the resolved item, not the key.
          Plugin.Log.LogError($"{_logTypeFlag} Requirement '{requirementItemName}' of recipe '{itemName}' resolves to item '{item.type}' in this game version, the recipe will need '{item.type}'.");
        }

        if (item.maxDurability > 0 && requirement.Value.Value<string>().Contains("."))
        {
          newRecipe.requirements.Add(new CraftingRequirement
          {
            item = item,
            durabilityAmount = requirement.Value.Value<float>()
          });
        }
        else
        {
          newRecipe.requirements.Add(new CraftingRequirement
          {
            item = item,
            amount = requirement.Value.Value<int>()
          });
        }
      }
    }

    if (requirementsToken is { HasValues: true } && newRecipe.requirements.Count == 0)
    {
      Plugin.Log.LogError($"{_logTypeFlag} Recipe '{itemName}' has no valid requirements ({requirementProblems} problems), skipping it instead of adding a free craft.");
      return;
    }

    // "replace": true empties out whatever the game ships for this item so only this recipe remains, "replace": false keeps them and adds this one next to them (the workbench gives every recipe its own slot).
    // Missing means true, which is what the mod did before the flag existed.
    // The game's recipes are captured in VanillaRecipes before anything is touched, which is what puts them back if the flag is turned off again or the config is edited.
    if (!VanillaRecipes.TryGetValue(itemPath, out var vanillaRecipes))
    {
      vanillaRecipes = [.. itemPath.recipes];
      VanillaRecipes[itemPath] = vanillaRecipes;
    }

    var replaceVanilla = recipeObject["replace"]?.Value<bool>() ?? true;
    if (!replaceVanilla && vanillaRecipes.Any(vanilla => RecipeMatches(vanilla, newRecipe)))
    {
      if (Plugin.LogWorkbench.Value)
        Plugin.Log.LogInfo($"{_logTypeFlag} Skipping {itemName}, the configured recipe is the same as the game's own recipe. Set \"replace\": true to replace it instead.");
      return;
    }

    ModRecipes.TryGetValue(itemPath, out var previousRecipe);
    var vanillaPresent = vanillaRecipes.All(vanilla => itemPath.recipes.Contains(vanilla));
    var vanillaAbsent = vanillaRecipes.All(vanilla => !itemPath.recipes.Contains(vanilla));

    if (previousRecipe != null && RecipeMatches(previousRecipe, newRecipe) && (replaceVanilla ? vanillaAbsent : vanillaPresent))
    {
      if (Plugin.LogWorkbench.Value)
        Plugin.Log.LogInfo($"{_logTypeFlag} Skipping {itemName}, its recipe is already applied on '{itemPath.name}' next to {itemPath.recipes.Count - 1} game recipe(s).");
      return;
    }

    if (previousRecipe != null) itemPath.recipes.Remove(previousRecipe);

    if (replaceVanilla)
    {
      foreach (var vanilla in vanillaRecipes) itemPath.recipes.Remove(vanilla);
      if (Plugin.LogWorkbench.Value)
        Plugin.Log.LogInfo($"{_logTypeFlag} Replacing the game's {vanillaRecipes.Count} recipe(s) of {itemName}.");
    }
    else
    {
      foreach (var vanilla in vanillaRecipes.Where(vanilla => !itemPath.recipes.Contains(vanilla))) itemPath.recipes.Add(vanilla);
    }

    itemPath.recipes.Add(newRecipe);
    ModRecipes[itemPath] = newRecipe;

    // Remove any existing entry with the same GameObject name from every level (so the recipe only lives at its new level).
    var prefabName = itemPath.name;
    for (var i = 0; i < levelCount; i++)
    {
      var recipes = instance.levels[i].recipes;
      var index = recipes.FindIndex(r => r && r.name == prefabName);
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
      Plugin.Log.LogInfo($"{_logTypeFlag} Added recipe of {itemName} (obj='{itemPath.name}', type='{TypeOf(itemPath)}') with {newRecipe.requirements.Count} requirements at level {requiredLevel} (index {levelToAddTo}) workbench, '{itemPath.name}' now has {itemPath.recipes.Count} recipe(s)");
    instance.levels[levelToAddTo].recipes.Add(itemPath);
  }

  // Compares two recipes by what they produce and need, so re-opening the workbench with an unchanged config does not apply the mod's recipe a second time while a changed config replaces it.
  private static bool RecipeMatches(CraftingRecipes.Recipe a, CraftingRecipes.Recipe b)
  {
    if (a == null || b == null) return false;
    if (a.produceAmount != b.produceAmount) return false;
    if (a.requirements.Count != b.requirements.Count) return false;

    var used = new bool[b.requirements.Count];
    foreach (var requirementA in a.requirements)
    {
      if (!requirementA?.item) return false;
      var found = false;
      for (var i = 0; i < b.requirements.Count; i++)
      {
        if (used[i]) continue;
        var requirementB = b.requirements[i];
        if (!requirementB?.item) continue;
        if (requirementA.item.type != requirementB.item.type) continue;
        if (requirementA.amount != requirementB.amount) continue;
        if (!Mathf.Approximately(requirementA.durabilityAmount, requirementB.durabilityAmount)) continue;
        used[i] = true;
        found = true;
        break;
      }
      if (!found) return false;
    }
    return true;
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
        if (resource)
        {
          return resource;
        }
      }
      catch (Exception)
      {
        // Ignore exceptions when loading resources
      }
    }

    // If we're only looking for unused items it should exit above, if it doesn't that means we're looking for an item that exists in the game
    if (unusedOnly) return null;

    // The exact folder names under Resources/InventoryItems, matching the game's own item table.
    // Resource paths are case-sensitive on Linux and macOS, so the capitalised variants this used to have could never load anything there.
    var categories = new[]
    {
            "_other",
            "ammo",
            "consumables",
            "expObjs",
            "firearms",
            "home",
            "journalItems",
            "maps",
            "materials",
            "meleeWeapons",
            "misc",
            "questItems",
            "thrownItems",
            "traps",
            "useable",
        };
    foreach (var category in categories)
    {
      var resourcePath = $"InventoryItems/{category}/{itemName}";
      try
      {
        var resource = Resources.Load<GameObject>(resourcePath);
        if (resource)
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