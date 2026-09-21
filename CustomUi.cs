using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal static class CustomUiPatch
{
  public static bool IsAnyUiOpen;
  public static bool IsCapturingHotkey;

  private const int MinWindowWidth = 250;
  private const int MinWindowHeight = 180;

  private static readonly Dictionary<int, Rect> WindowRects = new();
  private static readonly Dictionary<int, string[]> WindowBuffers = new();
  private static int _resizingWindow = -1;
  private static Rect _resizeStartRect;
  private static int _draggingWindow = -1;
  private static Vector2 _dragStartPos;
  private static Vector2 _dragGrabOffset;

  private static readonly List<string> UiSections = [];
  private static readonly List<TabDefinition> Tabs = [];
  private static int _tab;
  private static Vector2 _tabScroll;
  private static readonly Dictionary<string, string> EditBuffers = new();
  private static GUIStyle _tabTitleStyle;
  private static GUIStyle _keyLabelStyle;

  // F2 tab layout. A tab either lists its groups with the exact "Section/Key" entries in drawing order, or falls back to drawing every entry of its sections in config order (used by the merged Skills tab, which keeps the tier names as sub headers).
  private sealed class TabDefinition
  {
    public readonly string Name;
    public readonly string[] Sections;
    public readonly (string Title, string[] Keys)[] Groups;

    public TabDefinition(string name, string[] sections)
    {
      Name = name;
      Sections = sections;
    }

    public TabDefinition(string name, (string Title, string[] Keys)[] groups)
    {
      Name = name;
      Groups = groups;
    }
  }

  // Sections that no longer get an F2 tab, their settings live in the F6 windows or the effect manager
  private static readonly string[] SectionsWithoutTab = ["Cheats", "Crafting", "Characters", "Effects", "RandomInventories", "CustomItems"];

  // Builds the F2 tabs. The custom file sections (Characters, Effects, Random Inventories, Crafting recipes) moved into the F6 windows, their loading toggles are drawn there above the save and reload buttons.
  private static void BuildTabs()
  {
    if (UiSections.Count == 0)
    {
      foreach (var entry in GetEntries())
      {
        if (entry.Definition.Section == "!Mod" && entry.Definition.Key == "Version") continue;
        if (!UiSections.Contains(entry.Definition.Section)) UiSections.Add(entry.Definition.Section);
      }
    }

    if (Tabs.Count > 0 || UiSections.Count == 0) return;

    Tabs.Add(new TabDefinition("!Mod", ["!Mod"]));
    Tabs.Add(new TabDefinition("Items & Crafting",
    [
      ("Crafting", ["Items/Enable Section", "Crafting/Enable Free Crafting"]),
      ("Stack Sizes", ["Items/Enable Global Stack Size", "Items/Global Stack Resize"]),
      ("Durability", ["Items/Enable Global Max Durability", "Items/Global Max Durability"]),
    ]));
    Tabs.Add(new TabDefinition("Inventories",
    [
      ("Toggles", ["Inventories/Enable Workbench Modification", "Inventories/Remove Excess Slots", "Inventories/Enable Inventory Modification", "Inventories/Enable Hotbar Modification", "Inventories/Enable Trader Modification", "Inventories/Enable Crafting Modification"]),
      ("Slots", ["Inventories/Workbench Right Slots", "Inventories/Workbench Down Slots", "Inventories/Inventory Right Slots", "Inventories/Inventory Down Slots", "Inventories/Hotbar Right Slots", "Inventories/Hotbar Down Slots", "Inventories/Trader Right Slots", "Inventories/Trader Down Slots", "Inventories/Crafting Window Right Slots", "Inventories/Crafting Window Down Slots"]),
      ("Positioning", ["Inventories/Storage X Offset", "Inventories/Storage Z Offset", "Inventories/TraderInventory Window X Offset", "Inventories/TraderInventory Window Z Offset", "Inventories/TraderSell Window X Offset", "Inventories/TraderSell Window Z Offset", "Inventories/TraderBuy Window X Offset", "Inventories/TraderBuy Window Z Offset", "Inventories/TraderClose Button X Offset", "Inventories/TraderClose Button Z Offset", "Inventories/Crafting Window X Offset", "Inventories/Crafting Window Z Offset"]),
    ]));
    Tabs.Add(new TabDefinition("Player",
    [
      ("General", ["Player/Enable Section"]),
      ("Stats", ["Player/Player FoV", "Player/Player FoV at Night Only", "Player/Player Sight Distance", "Player/Player Eagle Eye Distance"]),
      ("Stamina", ["Player/Max Stamina", "Player/Stamina Drain Value", "Player/Stamina Regen Value"]),
      ("Health", ["Player/Max Health", "Player/Health Regen Interval", "Player/Health Regen Value", "Player/Health Regen Modifier"]),
      ("Speed", ["Player/Walk Speed", "Player/Run Speed", "Player/Run Speed Modifier"]),
      ("Cheats", ["Player/Cant Get Interrupted", "Player/Infinite Stamina", "Player/Infinite Stamina Effect", "Player/Enable Godmode", "Player/Enable Noclip", "Player/Invisible", "Player/Can see behind walls"]),
    ]));
    Tabs.Add(new TabDefinition("Skills", [.. UiSections.Where(s => s == "Skills" || s.StartsWith("Skills - ", StringComparison.Ordinal))]));
    Tabs.Add(new TabDefinition("Time",
    [
      ("Toggles", ["Time/Enable Section", "Time/Stop Time", "Time/Reset Well"]),
      ("Time Flow", ["Time/Daytime Flow", "Time/Nighttime Flow"]),
      ("Change Time", ["Time/Set Time", "Time/Set Current Time"]),
    ]));
    Tabs.Add(new TabDefinition("Generator", ["Generator"]));
    Tabs.Add(new TabDefinition("Camera & UI",
    [
      ("Camera", ["Camera/Enable Section", "Camera/Camera Zoom Factor", "Camera/Disable Post FX", "Camera/Disable Vignette"]),
      ("UI", ["UI/Enable Section", "UI/Disable UI/HUD", "UI/Disable Healthbar", "UI/Disable Lives", "UI/Disable Staminabar", "UI/Disable Skillbar (current effects)"]),
    ]));
    Tabs.Add(new TabDefinition("Enemies",
    [
      ("Toggles", ["Enemies/Disable Night Floor Gore (Requires Save Reload)", "Enemies/Creatures always know where player is"]),
      ("Spawn Chances", ["Enemies/Creature Spawn Chance During Nighttime", "Enemies/Creature Spawn Chance During Daytime"]),
      ("Spawn Multiplier", ["Enemies/Creature Enemy Multiplier During Nighttime", "Enemies/Creature Enemy Multiplier During Daytime"]),
    ]));
    Tabs.Add(new TabDefinition("Hotkeys",
    [
      ("Cheats", ["Hotkeys/Toggle Godmode", "Hotkeys/Toggle Noclip", "Hotkeys/Toggle Infinite Stamina", "Hotkeys/Toggle Time Stop", "Hotkeys/Toggle Free Crafting", "Hotkeys/Toggle Invisible"]),
      ("Menus", ["Hotkeys/Toggle HUD/UI", "Hotkeys/Open Customizer UI", "Hotkeys/Open Item Spawner", "Hotkeys/Open Enemy Spawner", "Hotkeys/Open Effect Manager", "Hotkeys/Open Custom Data", "Hotkeys/Open UI Manager", "Hotkeys/Open Inventory Editor", "Hotkeys/Save Cursor Position"]),
    ]));
    Tabs.Add(new TabDefinition("Loot",
    [
      ("General", ["Loot/Enable Mushroom Respawn", "Loot/Enable Loot Respawn"]),
      ("Mushrooms", ["Loot/Mushroom Respawn Time", "Loot/Mushroom Respawn Per Day"]),
      ("Loot", ["Loot/Loot Respawn Time", "Loot/Loot Respawn Per Day"]),
    ]));
    Tabs.Add(new TabDefinition("Defenses",
    [
      ("General", ["Defenses/Enable Section", "Defenses/Enable Debug Logs"]),
      ("Barricades", ["Defenses/Barricade Health Modification", "Defenses/Barricade Health Multiplier", "Defenses/Enable Barricade Healing", "Defenses/Barricade Heal Interval", "Defenses/Barricade Heal Percent", "Defenses/Only Player Can Damage Barricades"]),
      ("Beartraps", ["Defenses/BearTrap Recovery", "Defenses/BearTrap Recover Items", "Defenses/Enable BearTrap Damage Modification", "Defenses/BearTrap Damage", "Defenses/BearTrap Auto Recharge", "Defenses/BearTrap Recharge Time"]),
      ("Chaintraps", ["Defenses/ChainTrap Recovery", "Defenses/ChainTrap Recover Items", "Defenses/Enable ChainTrap Damage Modification", "Defenses/ChainTrap Damage", "Defenses/ChainTrap Auto Recharge", "Defenses/ChainTrap Recharge Time"]),
    ]));

    // Anything that is not in the layout above and not moved to another window still gets a tab
    foreach (var section in from section in UiSections where !SectionsWithoutTab.Contains(section) where !Tabs.Any(t => t.Sections != null && t.Sections.Contains(section)) where !Tabs.Any(t => t.Groups != null && t.Groups.Any(g => g.Keys.Any(k => k.StartsWith(section + "/", StringComparison.Ordinal)))) select section)
    {
      Tabs.Add(new TabDefinition(section, [section]));
    }
  }

  private static string _itemSearch = "";
  private static string _itemName = "";
  private static string _itemAmount = "1";
  private static Vector2 _itemScroll;
  private static string[] _allItems = [];

  private static string _enemyName = "";
  private static string _enemySearch = "";
  private static bool _spawnOnSavedPos;
  private static bool _hasSavedCursorPos;
  private static Vector3 _savedCursorPos;
  private static Vector2 _enemyScroll;
  private static string[] _enemyNames;
  private static readonly string[] EnemyCandidates =
  [
    "Banshee", "BansheeBaby", "Centipede", "ChomperBlack", "ChomperHalf",
    "ChomperRed", "ChomperRed_small", "ChomperBride", "Dog", "DogMutated",
    "HumanSpider", "HumanSpiderMinion", "Kamikaze", "Pig", "Rabbit",
    "Raven", "Chicken", "Deer", "Redneck", "Redneck02",
    "Redneck03", "Spider01", "Spider02", "Spider03_day", "Swamper1",
    "Villager", "Villager1_Burning", "Villager3_plank", "VillagerTorch", "Bride",
    "Brat_babykury", "AntagonistAct2Lv4", "FakeChars/NightWorms_01", "FakeChars/NightWorms_02", "FakeChars/Worms_enemy_01",
    "FakeChars/ForestSpirit2", "FakeChars/pig_big_mutant", "FakeChars/larva_big_01",
    "FakeChars/bug_cockroach_big", "FakeChars/bug_cockroach_huge", "FakeChars/Zombie_male_sitting", "FakeChars/Zombie_female_bathing", "FakeChars/AreaBird",
  ];

  private static readonly Dictionary<CharacterEffectType, EffectInputs> EffectBuffers = new();
  private static Vector2 _effectScroll;

  // Inventory editor state
  private static int _selectedInvWindow = -1;
  private static int _selectedInvSlot = -1;
  private static string _invSlotItemName = "";
  private static string _invSlotAmount = "1";
  private static readonly Dictionary<int, Vector2> InvScrolls = new();

  // Custom data editor state
  private static string _dataSelectedKey = "";
  private static string _dataNewKey = "";
  private static string _dataKeysSearch = "";
  private static Vector2 _dataKeysScroll;
  private static Vector2 _dataPropsScroll;
  private static readonly Dictionary<string, string> PropBuffers = new();
  private static GUIStyle _leftButtonStyle;
  private static GUIStyle _infoStyle;
  private static string _infoText = "";
  private static string _infoKey = "";

  // Sub-picker state: which nested array/object is being edited with a picker
  private static string _subPickerKey = ""; // e.g. "LootContainer_WoodenCrate_1A"
  private static string _subPickerProp = ""; // e.g. "items" or "Attacks"
  private static string _subPickerNewItem = "";
  private static Vector2 _subPickerScroll;
  // Nested path below the sub-picker property used by the generic editor (e.g. requirements/wire)
  private static readonly List<string> SubPickerPath = [];

  private static ConfigEntryBase _editingHotkey;
  private static HotkeyCapture _capture;
  private static bool _wasForbidInputs;
  // Stays true across window switches (F2 -> F3), only reset when the last window closes.
  // Without this, switching windows would re-capture the input state while it is already modified and closing would restore a frozen game.
  private static bool _sessionActive;

  private static string _status = "";
  private static float _statusUntil;
  private static string _editorStatus = "";
  private static float _editorStatusUntil;
  // The item quick-pick has its own status line as well, so its messages only show up there
  private static string _quickPickStatus = "";
  private static float _quickPickStatusUntil;
  // Keeps the game cursor visible for a short time after closing a window, the game hides it on its own when the player is not hovering anything
  private static float _cursorVisibleUntil;

  private sealed class EffectInputs
  {
    public string Duration = "0";
    public string Modifier = "1";
    public string Interval = "0";
  }

  private sealed class DataFile(
    string name,
    Func<string> path,
    Func<JObject> get,
    Action<JObject> set,
    Func<JToken> newKeyTemplate = null,
    string[] settings = null)
  {
    public readonly string Name = name;
    public readonly Func<string> Path = path;
    public readonly Func<JObject> Get = get;
    public readonly Action<JObject> Set = set;
    // Template for a new top level key, per file type
    public readonly Func<JToken> NewKeyTemplate = newKeyTemplate;
    // Config entries of this file (loading toggles and readmes) drawn above the save and reload buttons
    public readonly string[] Settings = settings ?? [];
  }

  private static readonly DataFile[] DataFiles =
  [
    new("Custom Items", () => Plugin.CustomItemsPath, () => Plugin.CustomItems, o => Plugin.CustomItems = o,
      () => new JObject { { "name", "New Item" }, { "description", "" } },
      ["CustomItems/Enable Section", "Items/Load Mod Defaults First", "CustomItems/Note"]),
    new("Custom Crafting Recipes", () => Plugin.CustomCraftingRecipesPath, () => Plugin.CustomCraftingRecipes, o => Plugin.CustomCraftingRecipes = o,
      () => new JObject { { "requiredlevel", 1 }, { "resource", "" }, { "givesamount", 1 }, { "requirements", new JObject() } },
      ["Crafting/Enable Crafting Recipes Modification", "Crafting/Load Mod Defaults First", "Crafting/Try to load unused items", "Crafting/Note1", "Crafting/Note2"]),
    new("Custom Characters", () => Plugin.CustomCharactersPath, () => Plugin.CustomCharacters, o => Plugin.CustomCharacters = o,
      () => new JObject { { "Health", 100 }, { "WalkSpeed", 1 }, { "RunSpeed", 1 }, { "Attacks", new JArray() } },
      ["Characters/Enable Section", "Characters/Note"]),
    new("Custom Character Effects", () => Plugin.CharacterEffectsPath, () => Plugin.CharacterEffects, o => Plugin.CharacterEffects = o,
      () => new JObject { { "duration", 0 }, { "modifier", 1 }, { "interval", 0 } },
      ["Effects/Enable Section", "Effects/Note"]),
    new("Custom Random Inventories", () => Plugin.CustomRandomInventoriesPath, () => Plugin.CustomRandomInventories, o => Plugin.CustomRandomInventories = o,
      () => new JObject { { "presets", new JObject() } },
      ["RandomInventories/Enable Section", "RandomInventories/Note"]),
    new("Custom Loot", () => Plugin.CustomLootPath, () => Plugin.CustomLoot, o => Plugin.CustomLoot = o,
      () => new JObject { { "enabled", false }, { "replace", false }, { "items", new JArray() } },
      ["Loot/Enable Section", "Loot/Note"]),
  ];

  private static DataFile _selectedDataFile;
  private static Vector2 _itemKeysScroll;
  private static string _itemKeysSearch = "";

  // Known keys per editor with the value they get when they are added from the available keys
  // picker, so users do not need to know the key names from the wiki.
  private static readonly (string Name, JToken Default)[] RecipeKeys =
  [
    ("enabled", true),
    ("replace", true),
    ("requiredlevel", 1),
    ("resource", ""),
    ("givesamount", 1),
    ("requirements", new JObject()),
  ];

  private static readonly (string Name, JToken Default)[] LootEntryKeys =
  [
    ("enabled", false),
    ("replace", false),
    ("items", new JArray()),
  ];

  private static readonly (string Name, JToken Default)[] CharacterKeys =
  [
    ("Health", 100),
    ("WalkSpeed", 2),
    ("RunSpeed", 4),
    ("Attacks", new JArray()),
  ];

  private static readonly (string Name, JToken Default)[] EffectKeys =
  [
    ("duration", 0f),
    ("modifier", 0f),
    ("interval", 0f),
    ("stopsBleeding", false),
    ("stopsPoison", false),
    ("hasPoisonOverlay", false),
    ("activateSound", ""),
    ("startDelay", 0f),
  ];

  private static readonly (string Name, JToken Default)[] RandomInvKeys =
  [
    ("presets", new JArray()),
  ];

  private static (string Name, JToken Default)[] AvailableKeysFor(string editorName)
  {
    return editorName switch
    {
      "Custom Items" => ItemKeys,
      "Custom Crafting Recipes" => RecipeKeys,
      "Custom Characters" => CharacterKeys,
      "Custom Character Effects" => EffectKeys,
      "Custom Random Inventories" => RandomInvKeys,
      "Custom Loot" => LootEntryKeys,
      _ => [],
    };
  }

  // All known Custom Items keys with their default values, used by the item key picker in the Custom Items editor
  private static readonly (string Name, JToken Default)[] ItemKeys =
  [
    ("name", "New Item"),
    ("description", ""),
    ("iconType", ""),
    ("fireMode", "semi"),
    ("hasAmmo", false),
    ("canBeReloaded", false),
    ("ammoReloadType", "magazine"),
    ("ammoType", ""),
    ("hasDurability", false),
    ("maxDurability", 100),
    ("ignoreDurabilityInValue", true),
    ("repairable", true),
    ("flamethrowerdrag", 0.4f),
    ("flamethrowercontactDamage", 20),
    ("flamethrowerBurnDamage", 0f),
    ("damage", 10),
    ("clipSize", 100),
    ("value", 100),
    ("maxAmount", 100),
    ("stackable", true),
    ("isStackable", true),
    ("ExpValue", 100),
    ("IsExpItem", false),
    ("InfiniteAmmo", false),
    ("InfiniteDurability", false),
    ("drainDurabilityOnShot", false),
    ("drainAmmoOnShot", true),
    ("aimDontSlow", false),
    ("aimFOV", 0f),
    ("fireRate", 0),
    ("requirements", new JObject()),
    ("rottenItem", ""),
    ("rottenItemMaxAmount", 100),
    ("rottenItemStackable", true),
    ("rottenItemValue", 100),
    ("rottenItemExpValue", 100),
    ("rottenItemIsExpItem", false),
    ("activateSound", ""),
    ("addsHotbarSlot", false),
    ("addsInventorySlot", false),
    ("addSlotAmount", 1),
    ("addsPoisonImmunity", false),
    ("aimFinishedFrame", 0),
    ("aimReturnSound", ""),
    ("aimSound", ""),
    ("aniLibrary", ""),
    ("armorValue", 0),
    ("attack2Sound", ""),
    ("attackDoesNotInterrupt", false),
    ("attackSound", ""),
    ("attackSoundRange", 0f),
    ("barricadeDamageDurabilityDrain", 0),
    ("burstAmount", 0),
    ("canAttackFrame", 0),
    ("canBeAimed", false),
    ("canBePlaced", false),
    ("canCutInHalf", false),
    ("canResumeAim", false),
    ("damageDurabilityDrain", 0),
    ("deactivateSound", ""),
    ("destroySound", ""),
    ("dontRemoveOnUse", false),
    ("dropOnReleaseAim", false),
    ("durabilityDrain", 0f),
    ("durabilityRegeneration", 0f),
    ("emptyClipSound", ""),
    ("examinable", false),
    ("getSound", ""),
    ("givesLife", false),
    ("givesSkillSlot", false),
    ("hideSound", ""),
    ("isAmmo", false),
    ("isArmor", false),
    ("isFirearm", false),
    ("isFlashlight", false),
    ("isImportantItem", false),
    ("isMap", false),
    ("isMelee", false),
    ("isNaturalLight", false),
    ("isRepairKit", false),
    ("isThrowable", false),
    ("isWorkbenchUpgrade", false),
    ("maxAim", 0f),
    ("minAim", 0f),
    ("needsToBeOnHotbar", false),
    ("nightVision", false),
    ("noMuzzleFlash", false),
    ("notUseableWhenAiming", false),
    ("onBrokenText", ""),
    ("placeOnUse", false),
    ("projectileAmount", 0),
    ("protectsFromShadows", false),
    ("recoilAmount", 0f),
    ("recoverableAfterThrown", false),
    ("regeneratesWhenInactive", false),
    ("reloadSound", ""),
    ("specialBarricadeDamage", 0),
    ("specialBarricadeDamageDurabilityDrain", 0),
    ("specialDamage", 0),
    ("specialDamageDurabilityDrain", 0),
    ("spillsLiquid", false),
    ("stacksDurability", false),
    ("staminaAttackDrain", 0),
    ("staminaSpecialAttackDrain", 0),
    ("takesDamageOnPlayerHit", false),
    ("zoom", 0f),
  ];

  // Descriptions for the known Custom Items keys, shown in the ? info bar of the Custom Data editor
  private static readonly Dictionary<string, string> ItemKeyDescriptions = new()
  {
    ["name"] = "The display name of the item in the game.",
    ["description"] = "A brief description of what the item is.",
    ["iconType"] = "The item's icon, requires an item ID.",
    ["fireMode"] = "The fire mode of a weapon: semi, burst, fullauto/auto, or single.",
    ["hasAmmo"] = "Whether the item requires ammunition.",
    ["canBeReloaded"] = "Whether the item can be reloaded.",
    ["ammoReloadType"] = "The reload type: magazine reloads the whole clip, single uses one item per bullet.",
    ["ammoType"] = "The type of ammo the item uses, requires an item ID.",
    ["hasDurability"] = "Whether the item has durability.",
    ["maxDurability"] = "The maximum durability of the item.",
    ["ignoreDurabilityInValue"] = "Whether durability is ignored when selling the item.",
    ["repairable"] = "Whether the item can be repaired.",
    ["flamethrowerdrag"] = "The drag of the flamethrower's rigidbody, only applies to flamethrowers.",
    ["flamethrowercontactDamage"] = "The contact damage of the flamethrower's flame, only applies to flamethrowers.",
    ["flamethrowerBurnDamage"] = "The burn damage per tick of the flamethrower, only applies to flamethrowers.",
    ["damage"] = "The damage output of the item.",
    ["clipSize"] = "The maximum ammo capacity of the weapon.",
    ["value"] = "The item's selling price.",
    ["maxAmount"] = "The maximum stack amount of the item.",
    ["stackable"] = "Whether the item can be stacked in inventory.",
    ["isStackable"] = "Whether the item can be stacked in inventory.",
    ["ExpValue"] = "The amount of experience gained when the item is used.",
    ["IsExpItem"] = "Whether the item can be used for cooking.",
    ["InfiniteAmmo"] = "When true, the item never runs out of ammo.",
    ["InfiniteDurability"] = "When true, the item never loses durability.",
    ["drainDurabilityOnShot"] = "Whether firing drains durability.",
    ["drainAmmoOnShot"] = "Whether firing drains ammo.",
    ["aimDontSlow"] = "Whether aiming does not slow the player down.",
    ["aimFOV"] = "The field of view while aiming.",
    ["fireRate"] = "The firing rate of the weapon.",
    ["requirements"] = "The items required to repair the item, format: { \"itemId\": amount }. The amount is the durability amount for items with durability, otherwise the item count.",
    ["rottenItem"] = "The item ID the item rots into, used by mushrooms.",
    ["rottenItemMaxAmount"] = "The max stack amount of the rotten item.",
    ["rottenItemStackable"] = "Whether the rotten item can be stacked.",
    ["rottenItemValue"] = "The selling price of the rotten item.",
    ["rottenItemExpValue"] = "The experience awarded by the rotten item.",
    ["rottenItemIsExpItem"] = "Whether the rotten item can be used for cooking.",
    ["activateSound"] = "The sound played when the item is activated.",
    ["addsHotbarSlot"] = "Whether the item adds a hotbar slot.",
    ["addsInventorySlot"] = "Whether the item adds an inventory slot.",
    ["addSlotAmount"] = "The number of slots added when the item is picked up.",
    ["addsPoisonImmunity"] = "Whether the item grants poison immunity.",
    ["aimFinishedFrame"] = "The animation frame at which aiming finishes.",
    ["aimReturnSound"] = "The sound played when the player stops aiming.",
    ["aimSound"] = "The sound played when the player starts aiming.",
    ["aniLibrary"] = "The animation library used by the item.",
    ["armorValue"] = "The armor value of the item.",
    ["attack2Sound"] = "The sound played for the second attack.",
    ["attackDoesNotInterrupt"] = "Whether the attack does not interrupt the player.",
    ["attackSound"] = "The sound played when attacking.",
    ["attackSoundRange"] = "The range at which the attack sound is heard.",
    ["barricadeDamageDurabilityDrain"] = "The durability drained when dealing barricade damage.",
    ["burstAmount"] = "The number of shots in a burst.",
    ["canAttackFrame"] = "The animation frame at which the attack can happen.",
    ["canBeAimed"] = "Whether the item can be aimed.",
    ["canBePlaced"] = "Whether the item can be placed in the world.",
    ["canCutInHalf"] = "Whether the item can cut enemies in half.",
    ["canResumeAim"] = "Whether aiming can be resumed after releasing the aim button.",
    ["damageDurabilityDrain"] = "The durability drained when dealing damage.",
    ["deactivateSound"] = "The sound played when the item is deactivated.",
    ["destroySound"] = "The sound played when the item is destroyed.",
    ["dontRemoveOnUse"] = "Whether the item is not removed from inventory when used.",
    ["dropOnReleaseAim"] = "Whether the item is dropped when the player releases the aim button.",
    ["durabilityDrain"] = "The durability drained per use.",
    ["durabilityRegeneration"] = "The durability regenerated over time.",
    ["emptyClipSound"] = "The sound played when the clip is empty.",
    ["examinable"] = "Whether the item can be examined.",
    ["getSound"] = "The sound played when the item is picked up.",
    ["givesLife"] = "Whether using the item restores health.",
    ["givesSkillSlot"] = "Whether the item gives a skill slot.",
    ["hideSound"] = "The sound played when the item is hidden.",
    ["isAmmo"] = "Whether the item is ammunition.",
    ["isArmor"] = "Whether the item is armor.",
    ["isFirearm"] = "Whether the item is a firearm.",
    ["isFlashlight"] = "Whether the item is a flashlight.",
    ["isImportantItem"] = "Whether the wolf will not steal the item.",
    ["isMap"] = "Whether the item is a map.",
    ["isMelee"] = "Whether the item is a melee weapon.",
    ["isNaturalLight"] = "Whether the item emits natural light.",
    ["isRepairKit"] = "Whether the item is a repair kit.",
    ["isThrowable"] = "Whether the item can be thrown.",
    ["isWorkbenchUpgrade"] = "Whether the item is a workbench upgrade.",
    ["maxAim"] = "The maximum aim distance.",
    ["minAim"] = "The minimum aim distance.",
    ["needsToBeOnHotbar"] = "Whether the item needs to be on the hotbar to be used.",
    ["nightVision"] = "Whether the item grants night vision.",
    ["noMuzzleFlash"] = "Whether the weapon has no muzzle flash.",
    ["notUseableWhenAiming"] = "Whether the item cannot be used while aiming.",
    ["onBrokenText"] = "The message shown when the item breaks.",
    ["placeOnUse"] = "Whether the item is placed in the world when used.",
    ["projectileAmount"] = "The number of projectiles fired per shot.",
    ["protectsFromShadows"] = "Whether the item protects the player from shadows.",
    ["recoilAmount"] = "The recoil of the weapon.",
    ["recoverableAfterThrown"] = "Whether the item can be recovered after being thrown.",
    ["regeneratesWhenInactive"] = "Whether durability regenerates while the item is not in use.",
    ["reloadSound"] = "The sound played when reloading.",
    ["specialBarricadeDamage"] = "The barricade damage of the special attack.",
    ["specialBarricadeDamageDurabilityDrain"] = "The durability drained when dealing special barricade damage.",
    ["specialDamage"] = "The damage of the special attack.",
    ["specialDamageDurabilityDrain"] = "The durability drained when dealing special damage.",
    ["spillsLiquid"] = "Whether the item spills liquid when aimed.",
    ["stacksDurability"] = "Whether durability stacks with the stack amount.",
    ["staminaAttackDrain"] = "The stamina drained per attack.",
    ["staminaSpecialAttackDrain"] = "The stamina drained per special attack.",
    ["takesDamageOnPlayerHit"] = "Whether the item takes damage when the player is hit.",
    ["zoom"] = "The zoom level while aiming.",
  };

  // Descriptions for the Custom Characters keys, shown in the ? info bar of the Custom Data editor
  private static readonly Dictionary<string, string> CharacterKeyDescriptions = new()
  {
    ["Health"] = "The maximum health of the character.",
    ["WalkSpeed"] = "The walking speed of the character.",
    ["RunSpeed"] = "The running speed of the character.",
    ["Attacks"] = "The list of attacks the character can use.",
  };

  // Descriptions for the attack fields shown in the Custom Characters attacks picker
  private static readonly Dictionary<string, string> AttackKeyDescriptions = new()
  {
    ["AttackName(ReadOnly)"] = "The internal name of the attack, read only, it comes from the game's own attack sensor.",
    ["AttackIsRanged(ReadOnly)"] = "Whether the attack is a ranged attack, read only.",
    ["Damage"] = "The damage the attack deals to the player.",
    ["BarricadeDamage"] = "The damage the attack deals to barricades and doors.",
  };

  private static string GetCharacterDescription(string propName)
  {
    if (CharacterKeyDescriptions.TryGetValue(propName, out var description)) return description;
    return AttackKeyDescriptions.TryGetValue(propName, out var attackDescription) ? attackDescription : null;
  }

  // Descriptions for the Custom Character Effects keys, shown in the ? info bar of the Custom Data editor
  private static readonly Dictionary<string, string> EffectKeyDescriptions = new()
  {
    ["duration"] = "How long the effect lasts, 0 means permanent.",
    ["modifier"] = "The strength of the effect, most effects use 1 as the default.",
    ["interval"] = "How often the effect applies, used by damage over time effects.",
    ["stopsBleeding"] = "Whether the effect stops bleeding.",
    ["stopsPoison"] = "Whether the effect stops poison.",
    ["hasPoisonOverlay"] = "Whether the effect shows the poison overlay.",
    ["activateSound"] = "The sound played when the effect activates.",
    ["startDelay"] = "The delay before the effect starts.",
  };

  // Descriptions for the Custom Crafting Recipes keys, shown in the ? info bar of the Custom Data editor
  private static readonly Dictionary<string, string> RecipeKeyDescriptions = new()
  {
    ["enabled"] = "Whether this entry is applied at all. Missing means enabled.",
    ["replace"] = "Whether the game's own recipe for this item is replaced by this one. Missing means replaced, set it to false to add this recipe next to the game's one instead.",
    ["requiredlevel"] = "The crafting level required to craft the item.",
    ["resource"] = "The item produced by the recipe.",
    ["givesamount"] = "The quantity of the resource produced.",
    ["requirements"] = "The items and amounts needed to craft the resource, format: { \"itemId\": amount }.",
    ["item"] = "The item ID used as an ingredient.",
    ["amount"] = "How many of the item are needed. Decimal values work for items with durability, like gasoline.",
  };

  // Descriptions for the Custom Random Inventories keys, shown in the ? info bar of the Custom Data editor
  private static readonly Dictionary<string, string> RandomInvKeyDescriptions = new()
  {
    ["presets"] = "The list of random inventory presets.",
    ["type"] = "The item type that can spawn.",
    ["amountMin"] = "The minimum amount of the item that can spawn.",
    ["amountMax"] = "The maximum amount of the item that can spawn.",
    ["chance"] = "The chance the item spawns, from 0 to 1.",
  };

  // Descriptions for the Custom Loot keys, shown in the ? info bar of the Custom Data editor
  private static readonly Dictionary<string, string> LootKeyDescriptions = new()
  {
    ["enabled"] = "Whether custom loot is active for the entity.",
    ["replace"] = "Whether the entity's default loot is replaced, otherwise custom loot fills the empty slots.",
    ["items"] = "The list of items the entity can drop.",
    ["item"] = "The item ID that can drop.",
    ["minAmount"] = "The minimum amount of the item that can drop.",
    ["maxAmount"] = "The maximum amount of the item that can drop.",
    ["chance"] = "The chance the item drops, from 0 to 1.",
  };

  // Returns the description for a property in the currently selected data file, or null if there is none
  private static string GetPropertyDescription(string propName)
  {
    if (_selectedDataFile == null) return null;
    return _selectedDataFile.Name switch
    {
      "Custom Items" => ItemKeyDescriptions.TryGetValue(propName, out var d) ? d : null,
      "Custom Characters" => GetCharacterDescription(propName),
      "Custom Character Effects" => EffectKeyDescriptions.TryGetValue(propName, out var d) ? d : null,
      "Custom Crafting Recipes" => RecipeKeyDescriptions.TryGetValue(propName, out var d) ? d : null,
      "Custom Random Inventories" => RandomInvKeyDescriptions.TryGetValue(propName, out var d) ? d : null,
      "Custom Loot" => LootKeyDescriptions.TryGetValue(propName, out var d) ? d : null,
      _ => null
    };
  }

  // Sound picker state for the Custom Character Effects editor
  private static string _soundPickerFor = "";
  private static string _soundPickerSearch = "";

  // Item ID picker: opens from the Pick buttons next to item ID fields and add rows.
  // _idPickerMode is "" to write into a field, or "loot"/"preset"/"requirement" to add an entry.
  // _idPickerName scopes a field to a child object (a preset key), _idPickerIndex scopes it to an array entry (a loot slot).
  private static string _idPickerKey = "";
  private static string _idPickerMode = "";
  private static string _idPickerName = "";
  private static int _idPickerIndex = -1;
  private static string _idPickerSearch = "";
  private static Vector2 _idPickerScroll;

  // The value part of an editor row has a fixed width so the Up, Down and X buttons line up on every row, and the nested value size label is kept short so it does not push them out.
  private const float ValueColumnWidth = 300f;
  private const float SizeLabelWidth = 110f;
  private static Vector2 _soundPickerScroll;
  private static string[] _allSounds = [];

  private static readonly (int Id, string Name)[] UiWindows =
  [
    (9001, "Customizer"),
    (9002, "Item Spawner"),
    (9003, "Enemy Spawner"),
    (9004, "Effect Manager"),
    (9005, "Custom Data Files"),
    (9006, "Custom Data Editor"),
    (9007, "UI Manager"),
    (9008, "Inventory Editor: Hotbar"),
    (9009, "Inventory Editor: Player"),
    (9010, "Inventory Editor: Container"),
    (9011, "Inventory Editor: Item Quickpick"),
  ];

  // Game height minus 25%, centered, 570px wide
  private static Rect MenuRect()
  {
    var height = Mathf.Max(Screen.height * 0.75f, 400f);
    return new Rect((Screen.width - 570f) / 2f, (Screen.height - height) / 2f, 570f, height);
  }

  // The F2 window holds the setting rows (name, [?] button, value, reset button) and the tab grid. It is kept close to half the screen width, resize it further with the drag handle or the UI Manager.
  private static Rect CustomizerRect()
  {
    var height = Mathf.Clamp(Screen.height * 0.92f, 460f, 1400f);
    var width = Mathf.Clamp(Screen.width * 0.5f, 820f, 1300f);
    return new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
  }

  // Effect manager default: full height, wide enough for the values, the Apply and Remove buttons and the Disable and Re-apply on loss checkboxes on one row
  private static Rect EffectManagerRect()
  {
    const float width = 940f;
    return new Rect((Screen.width - width) / 2f, 0f, width, Screen.height);
  }

  // The three inventory editor windows sit in one centered row, and the quickpick spans that whole row underneath them so the two together form a rectangle.
  private const float InventoryWindowWidth = 460f;
  private const float InventoryWindowGap = 24f;

  private static float InventoryRowWidth()
  {
    return InventoryWindowWidth * 3f + InventoryWindowGap * 2f;
  }

  // Kept on screen even when the game window is narrower than the row, which would otherwise push the first window off the left edge.
  private static float InventoryRowX()
  {
    return Mathf.Max(24f, (Screen.width - InventoryRowWidth()) / 2f);
  }

  private static Rect DefaultRectFor(int windowId)
  {
    switch (windowId)
    {
      case 9001:
        return CustomizerRect();
      case 9002:
      case 9003:
        return MenuRect();
      case 9004:
        return EffectManagerRect();
      case 9005:
      {
        // Custom data files: anchored to the top left corner with no margins
        return new Rect(0f, 0f, 320f, Screen.height * 0.45f);
      }
      case 9006:
      {
        // Custom data editor: fills everything to the right of the file window, from the top of the screen to the bottom
        const float fileWindowWidth = 320f;
        const float gap = 6f;
        var left = fileWindowWidth + gap;
        return new Rect(left, 0f, Mathf.Max(Screen.width - left, 400f), Mathf.Max(Screen.height, 400f));
      }
      case 9007:
        return new Rect(80f, 80f, 570f, 480f);
      case 9008:
      case 9009:
      case 9010:
      {
        // Workspace layout: three inventory windows side by side, each taking 45% of the screen height, centered as a row
        var h = Screen.height * 0.45f;
        var y = Screen.height * 0.05f;
        var idx = windowId - 9008;
        return new Rect(InventoryRowX() + idx * (InventoryWindowWidth + InventoryWindowGap), y, InventoryWindowWidth, h);
      }
      case 9011:
        // Quickpick: the row of inventory windows repeated underneath them, same left edge and width so the two form one block
        return new Rect(InventoryRowX(), Screen.height * 0.52f, InventoryRowWidth(), Screen.height * 0.45f);
      default:
        return new Rect(80f, 80f, 570f, 600f);
    }
  }

  private static Rect GetWindowRect(int windowId)
  {
    if (WindowRects.TryGetValue(windowId, out var rect)) return rect;
    rect = DefaultRectFor(windowId);
    WindowRects[windowId] = rect;
    return rect;
  }

  private static Rect ClampWindowRect(Rect rect)
  {
    rect.width = Mathf.Max(MinWindowWidth, rect.width);
    rect.height = Mathf.Max(MinWindowHeight, rect.height);
    rect.x = Mathf.Clamp(rect.x, 0f, Mathf.Max(0f, Screen.width - rect.width));
    rect.y = Mathf.Clamp(rect.y, 0f, Mathf.Max(0f, Screen.height - rect.height));
    return rect;
  }

  public static void ToggleCustomizerUi()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.UiOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    Plugin.UiOpen = true;
    OpenUi();
  }

  public static void ToggleItemSpawner()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.ItemSpawnerOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    Plugin.ItemSpawnerOpen = true;
    OpenUi();
  }

  public static void ToggleEnemySpawner()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.EnemySpawnerOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    Plugin.EnemySpawnerOpen = true;
    OpenUi();
  }

  public static void ToggleEffectManager()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.EffectManagerOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    Plugin.EffectManagerOpen = true;
    OpenUi();
  }

  public static void ToggleCustomData()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.CustomDataOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    Plugin.CustomDataOpen = true;
    OpenUi();
  }

  public static void ToggleUiManager()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.UiManagerOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    Plugin.UiManagerOpen = true;
    SyncWindowBuffers();
    OpenUi();
  }

  public static void ToggleInventoryEditor()
  {
    if (IsCapturingHotkey) return;
    if (Plugin.InventoryEditorOpen)
    {
      CloseUi();
      return;
    }
    CloseUi(false);
    _selectedInvWindow = -1;
    _selectedInvSlot = -1;
    Plugin.InventoryEditorOpen = true;
    OpenUi();
  }

  private static void OpenUi()
  {
    var firstOpen = !_sessionActive;
    IsAnyUiOpen = true;
    try
    {
      // Only capture the previous input state on the first open.
      // Switching directly between windows must not re-capture it, otherwise closing would restore the wrong state and leave the player frozen.
      if (firstOpen)
      {
        _sessionActive = true;
        _wasForbidInputs = Core.forbidInputs;
      }
      Core.forbidInputs = true;
      // The game keeps running while a menu is open, the cursor stays visible
      Core.showGameCursor(0f);
    }
    catch (Exception e)
    {
      Plugin.Log.LogError("Failed to open custom UI: " + e);
    }
  }

  public static void CloseUi(bool restore = true)
  {
    IsAnyUiOpen = false;
    Plugin.UiOpen = false;
    Plugin.ItemSpawnerOpen = false;
    Plugin.EnemySpawnerOpen = false;
    Plugin.EffectManagerOpen = false;
    Plugin.CustomDataOpen = false;
    Plugin.UiManagerOpen = false;
    Plugin.InventoryEditorOpen = false;
    _editingHotkey = null;
    _capture = null;
    _resizingWindow = -1;
    _selectedInvWindow = -1;
    _selectedInvSlot = -1;
    IsCapturingHotkey = false;
    if (!restore) return;
    try
    {
      if (_sessionActive)
      {
        _sessionActive = false;
        Core.forbidInputs = _wasForbidInputs;
      }
      // Keep the cursor visible for a few seconds after closing so it does not pop out from under the player
      _cursorVisibleUntil = Time.realtimeSinceStartup + 3f;
      Core.showGameCursor(0f);
    }
    catch (Exception e)
    {
      Plugin.Log.LogError("Failed to close custom UI: " + e);
    }
  }

  [HarmonyPatch(typeof(InputScript), nameof(InputScript.onPressEsc))]
  [HarmonyPrefix]
  public static bool OnPressEscPrefix()
  {
    if (IsCapturingHotkey)
    {
      _editingHotkey = null;
      _capture = null;
      IsCapturingHotkey = false;
      return false;
    }
    if (!IsAnyUiOpen) return true;
    CloseUi();
    return false;
  }

  public static void DrawUi()
  {
    if (!IsAnyUiOpen)
    {
      // Cursor grace period after the last window closed
      if (!(Time.realtimeSinceStartup < _cursorVisibleUntil)) return;
      try
      {
        Core.showGameCursor(0f);
      }
      catch (Exception e)
      {
        Plugin.Log.LogError("Custom UI draw error: " + e);
      }
      return;
    }
    try
    {
      // Keep the cursor visible while any window is open, the game logic runs normally now so it has to be re-shown every frame
      Core.showGameCursor(0f);
      // Resize and title bar drag input come first so window content (scrollviews etc.) cannot consume the mouse events while dragging
      if (Plugin.UiOpen)
      {
        ProcessResizeHandleInput(9001);
        DrawTitleBarDrag(9001);
      }
      else if (Plugin.ItemSpawnerOpen)
      {
        ProcessResizeHandleInput(9002);
        DrawTitleBarDrag(9002);
      }
      else if (Plugin.EnemySpawnerOpen)
      {
        ProcessResizeHandleInput(9003);
        DrawTitleBarDrag(9003);
      }
      else if (Plugin.EffectManagerOpen)
      {
        ProcessResizeHandleInput(9004);
        DrawTitleBarDrag(9004);
      }
      else if (Plugin.CustomDataOpen)
      {
        ProcessResizeHandleInput(9005);
        DrawTitleBarDrag(9005);
        ProcessResizeHandleInput(9006);
        DrawTitleBarDrag(9006);
      }
      else if (Plugin.UiManagerOpen)
      {
        ProcessResizeHandleInput(9007);
        DrawTitleBarDrag(9007);
      }
      else if (Plugin.InventoryEditorOpen)
      {
        ProcessResizeHandleInput(9008);
        DrawTitleBarDrag(9008);
        ProcessResizeHandleInput(9009);
        DrawTitleBarDrag(9009);
        ProcessResizeHandleInput(9010);
        DrawTitleBarDrag(9010);
        ProcessResizeHandleInput(9011);
        DrawTitleBarDrag(9011);
      }

      if (Plugin.UiOpen)
      {
        WindowRects[9001] = DrawWindow(9001, CustomizerWindow, "DarkwoodCustomizer");
      }
      else if (Plugin.ItemSpawnerOpen)
      {
        WindowRects[9002] = DrawWindow(9002, ItemSpawnerWindow, "Item Spawner");
      }
      else if (Plugin.EnemySpawnerOpen)
      {
        WindowRects[9003] = DrawWindow(9003, EnemySpawnerWindow, "Enemy Spawner");
      }
      else if (Plugin.EffectManagerOpen)
      {
        WindowRects[9004] = DrawWindow(9004, EffectManagerWindow, "Player Effect Manager");
      }
      else if (Plugin.CustomDataOpen)
      {
        WindowRects[9005] = DrawWindow(9005, CustomDataFilesWindow, "Custom Data");
        WindowRects[9006] = DrawWindow(9006, CustomDataEditorWindow, "Custom Data Editor");
      }
      else if (Plugin.UiManagerOpen)
      {
        WindowRects[9007] = DrawWindow(9007, UiManagerWindow, "UI Manager");
      }
      else if (Plugin.InventoryEditorOpen)
      {
        WindowRects[9008] = DrawWindow(9008, HotbarWindow, "Hotbar");
        WindowRects[9009] = DrawWindow(9009, PlayerInventoryWindow, "Player Inventory");
        WindowRects[9010] = DrawWindow(9010, ContainerWindow, "Open Container");
        WindowRects[9011] = DrawWindow(9011, QuickPickWindow, "Item Quickpick");
      }

      // Draw the resize handles on top of everything so scrollbars do not cover them
      if (Plugin.UiOpen) DrawResizeHandleVisual(9001);
      else if (Plugin.ItemSpawnerOpen) DrawResizeHandleVisual(9002);
      else if (Plugin.EnemySpawnerOpen) DrawResizeHandleVisual(9003);
      else if (Plugin.EffectManagerOpen) DrawResizeHandleVisual(9004);
      else if (Plugin.CustomDataOpen)
      {
        DrawResizeHandleVisual(9005);
        DrawResizeHandleVisual(9006);
      }
      else if (Plugin.UiManagerOpen) DrawResizeHandleVisual(9007);
      else if (Plugin.InventoryEditorOpen)
      {
        DrawResizeHandleVisual(9008);
        DrawResizeHandleVisual(9009);
        DrawResizeHandleVisual(9010);
        DrawResizeHandleVisual(9011);
      }

      // Poll the real cursor while a resize or drag is active.
      // Wine/Proton drops MouseDrag events during slow movement, so the IMGUI event stream cannot be trusted for tracking; the raw input position always can.
      UpdateResizeFromMouse();
      UpdateDragFromMouse();
    }
    catch (Exception e)
    {
      Plugin.Log.LogError("Custom UI draw error: " + e);
    }
  }

  // Draws a window and stores its rect.
  // While the user is actively dragging or resizing, the tracked rect is kept instead of the one GUILayout.Window returns, so content changes (scrollbars appearing, min sizes) cannot fight the drag and cause lag or jitter.
  private static readonly HashSet<int> TimedWindows = [];

  private static Rect DrawWindow(int windowId, GUI.WindowFunction func, string title)
  {
    var rect = GetWindowRect(windowId);
    // Time the first draw of every window so a slow one is visible in the log
    var firstDraw = Plugin.LogDebug.Value && TimedWindows.Add(windowId);
    var started = firstDraw ? Time.realtimeSinceStartup : 0f;
    var result = GUI.Window(windowId, rect, func, title);
    if (firstDraw)
      Plugin.Log.LogInfo($"[UI] First draw of window {windowId} '{title}' took {(Time.realtimeSinceStartup - started) * 1000f:F1}ms");
    if (_resizingWindow == windowId || _draggingWindow == windowId)
    {
      return rect;
    }
    return ClampWindowRect(result);
  }

  // Menu preload: windows do their setup work on the frame the player opens them, which can freeze the game for a moment.
  // These steps run one per frame once the game is up instead, so the cost is paid before any menu is opened.
  // This works slightly but it's still not enough, TODO: Improve
  private static readonly Queue<(string Name, Action Step)> WarmUpSteps = new();
  private static bool _warmUpFinished;

  public static void WarmUpStep()
  {
    if (_warmUpFinished) return;
    // Run once the game is up, or after a while even if a menu was opened first
    if (!ItemsDatabase.Instance && Time.frameCount < 900) return;

    if (WarmUpSteps.Count == 0)
    {
      WarmUpSteps.Enqueue(("item id list", () => _allItems = GetItemIds()));
      WarmUpSteps.Enqueue(("sound list", LoadAllSounds));
      WarmUpSteps.Enqueue(("data files", () =>
      {
        foreach (var file in DataFiles) file.Get();
      }));
    }

    var (name, step) = WarmUpSteps.Dequeue();
    var started = Time.realtimeSinceStartup;
    try
    {
      step();
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"[UI] Menu preload step '{name}' failed: {e.Message}");
    }
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[UI] Menu preload step '{name}' took {(Time.realtimeSinceStartup - started) * 1000f:F1}ms");
    if (WarmUpSteps.Count == 0) _warmUpFinished = true;
  }

  // Bottom-right corner resize handle. Input is processed before the windows draw (so scrollbars cannot steal the events) and the visual is drawn after (so it stays on top of the scrollbars).
  // The handle is 40px so it is easy to grab even when a horizontal scrollbar sits at the bottom of the window.
  private const float ResizeHandleSize = 40f;

  private static Rect GetResizeHandleRect(int windowId)
  {
    var rect = GetWindowRect(windowId);
    return new Rect(rect.x + rect.width - ResizeHandleSize, rect.y + rect.height - ResizeHandleSize, ResizeHandleSize, ResizeHandleSize);
  }

  private static void ProcessResizeHandleInput(int windowId)
  {
    var handleRect = GetResizeHandleRect(windowId);
    var e = Event.current;

    if (e.type == EventType.MouseDown && e.button == 0 && handleRect.Contains(e.mousePosition))
    {
      _resizingWindow = windowId;
      _resizeStartRect = GetWindowRect(windowId);
      e.Use();
    }
    else if (_resizingWindow == windowId && e.type == EventType.MouseUp)
    {
      _resizingWindow = -1;
      e.Use();
    }
  }

  // Polls the true cursor position while a resize is active.
  // This is done outside the IMGUI event stream because Wine/Proton coalesces and drops MouseDrag events during slow mouse movement, which made slow resizes stall.
  // Input.mousePosition is always up to date every frame.
  private static void UpdateResizeFromMouse()
  {
    if (_resizingWindow == -1) return;
    // Wine can drop the MouseUp event too, poll the button state as a fallback
    if (!Input.GetMouseButton(0))
    {
      _resizingWindow = -1;
      return;
    }
    var start = _resizeStartRect;
    var mouse = (Vector2)Input.mousePosition;
    mouse.y = Screen.height - mouse.y; // IMGUI and Input use different origins
    var delta = mouse - new Vector2(start.x + start.width, start.y + start.height);
    WindowRects[_resizingWindow] = ClampWindowRect(new Rect(
      start.x,
      start.y,
      start.width + delta.x,
      start.height + delta.y));
  }

  private static void DrawResizeHandleVisual(int windowId)
  {
    var handleRect = GetResizeHandleRect(windowId);
    var e = Event.current;
    if (e.type != EventType.Repaint) return;
    if (_resizingWindow == windowId)
    {
      GUI.color = new Color(1f, 1f, 0.5f);
    }
    GUI.Box(handleRect, "<->");
    GUI.color = Color.white;
  }

  // Title bar drag handled in screen space, before the window draws, so it keeps tracking the mouse even when the cursor leaves the window bounds.
  // The rightmost 30px are left out so the X button stays clickable.
  private static void DrawTitleBarDrag(int windowId)
  {
    var rect = GetWindowRect(windowId);
    var titleRect = new Rect(rect.x, rect.y, Mathf.Max(rect.width - 30f, 0f), 20f);
    var e = Event.current;
    if (e.type == EventType.MouseDown && titleRect.Contains(e.mousePosition))
    {
      _draggingWindow = windowId;
      _dragStartPos = new Vector2(rect.x, rect.y);
      // Where in the window the grab happened, so the window does not jump
      _dragGrabOffset = e.mousePosition - new Vector2(rect.x, rect.y);
      e.Use();
    }
    else if (_draggingWindow == windowId && e.type == EventType.MouseUp)
    {
      _draggingWindow = -1;
      e.Use();
    }
  }

  // Same Wine/Proton workaround as UpdateResizeFromMouse: poll the real cursor position every frame instead of relying on coalesced MouseDrag events.
  private static void UpdateDragFromMouse()
  {
    if (_draggingWindow == -1) return;
    if (!Input.GetMouseButton(0))
    {
      _draggingWindow = -1;
      return;
    }
    var start = _dragStartPos;
    var rect = GetWindowRect(_draggingWindow);
    var mouse = (Vector2)Input.mousePosition;
    mouse.y = Screen.height - mouse.y;
    var delta = mouse - (start + _dragGrabOffset);
    WindowRects[_draggingWindow] = ClampWindowRect(new Rect(
      start.x + delta.x,
      start.y + delta.y,
      rect.width,
      rect.height));
  }

  private static void DrawWindowHeader()
  {
    GUILayout.BeginHorizontal();
    GUILayout.FlexibleSpace();
    if (GUILayout.Button("X", GUILayout.Width(24f)))
    {
      CloseUi();
    }
    GUILayout.EndHorizontal();
  }

  private static void DrawStatus()
  {
    if (string.IsNullOrEmpty(_status)) return;
    if (Time.realtimeSinceStartup > _statusUntil)
    {
      _status = "";
      return;
    }
    GUI.color = new Color(1f, 0.9f, 0.5f);
    GUILayout.Label(_status);
    GUI.color = Color.white;
  }

  private static void SetStatus(string message)
  {
    _status = message;
    _statusUntil = Time.realtimeSinceStartup + 6f;
  }

  // The data editor has its own status line so editor messages do not show up in the file window as well
  private static void SetEditorStatus(string message)
  {
    _editorStatus = message;
    _editorStatusUntil = Time.realtimeSinceStartup + 6f;
  }

  private static bool DrawEditorStatus()
  {
    if (string.IsNullOrEmpty(_editorStatus)) return false;
    if (Time.realtimeSinceStartup > _editorStatusUntil)
    {
      _editorStatus = "";
      return false;
    }
    GUI.color = new Color(1f, 0.9f, 0.5f);
    GUILayout.Label(_editorStatus);
    GUI.color = Color.white;
    return true;
  }

  // The quickpick has its own status line too, so a pick message never shows up in the other windows
  private static void SetQuickPickStatus(string message)
  {
    _quickPickStatus = message;
    _quickPickStatusUntil = Time.realtimeSinceStartup + 6f;
  }

  private static void DrawQuickPickStatus()
  {
    if (string.IsNullOrEmpty(_quickPickStatus)) return;
    if (Time.realtimeSinceStartup > _quickPickStatusUntil)
    {
      _quickPickStatus = "";
      return;
    }
    GUI.color = new Color(1f, 0.9f, 0.5f);
    GUILayout.Label(_quickPickStatus);
    GUI.color = Color.white;
  }

  // GetConfigEntries respects the Order attributes used across the config, unlike Entries.Values, so it is used despite being marked obsolete.
#pragma warning disable CS0618 // Type or member is obsolete
  private static IEnumerable<ConfigEntryBase> GetEntries()
  {
    return Plugin.Instance.Config.GetConfigEntries();
  }
#pragma warning restore CS0618 // Type or member is obsolete

  private static void CustomizerWindow(int id)
  {
    if (!Plugin.Instance)
    {
      GUILayout.Label("Plugin not loaded yet");
      return;
    }

    BuildTabs();

    if (Tabs.Count == 0)
    {
      GUILayout.Label("No config sections found");
      return;
    }

    if (_tab >= Tabs.Count) _tab = 0;

    // All tabs visible at once, no scrolling
    _tab = GUILayout.SelectionGrid(_tab, [.. Tabs.Select(t => t.Name)], 4);

    _tabScroll = GUILayout.BeginScrollView(_tabScroll);
    DrawTabEntries(Tabs[_tab]);
    GUILayout.EndScrollView();

    // Setting descriptions are shown in a fixed bar at the bottom instead of hover tooltips.
    // IMGUI hover tooltips rely on mouse move events that Wine/Proton drops, so clicking the ? button is reliable everywhere.
    DrawInfoBar();
  }

  // Draws the entries of a tab: grouped tabs draw their groups in order, flat tabs draw every entry of their sections (the merged Skills tab keeps each tier as a sub header)
  private static void DrawTabEntries(TabDefinition tab)
  {
    if (tab.Groups != null)
    {
      var byKey = new Dictionary<string, ConfigEntryBase>();
      foreach (var entry in GetEntries())
      {
        byKey[entry.Definition.Section + "/" + entry.Definition.Key] = entry;
      }

      foreach (var group in tab.Groups)
      {
        var entries = group.Keys.Select(key => byKey.TryGetValue(key, out var entry) ? entry : null).Where(e => e != null && !IsHiddenFromUi(e)).ToList();
        if (entries.Count == 0) continue;
        DrawDivider();
        DrawGroupTitle(group.Title);
        DrawDivider();
        foreach (var entry in entries) DrawConfigEntry(entry);
      }
      return;
    }

    var merged = tab.Sections.Length > 1;
    var lastSection = "";
    foreach (var entry in GetEntries())
    {
      if (IsHiddenFromUi(entry) || !tab.Sections.Contains(entry.Definition.Section)) continue;
      if (merged && entry.Definition.Section != lastSection)
      {
        if (lastSection.Length > 0)
        {
          DrawDivider();
          DrawGroupTitle(entry.Definition.Section);
          DrawDivider();
        }
        lastSection = entry.Definition.Section;
      }
      DrawConfigEntry(entry);
    }
  }

  // Full width separator line, used to split groups of settings inside a tab
  private static void DrawDivider()
  {
    var rect = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
    if (Event.current.type != EventType.Repaint) return;
    var previous = GUI.color;
    GUI.color = new Color(1f, 1f, 1f, 0.35f);
    GUI.DrawTexture(new Rect(rect.x, rect.y + 2f, rect.width, 2f), Texture2D.whiteTexture);
    GUI.color = previous;
  }

  private static void DrawGroupTitle(string title)
  {
    _tabTitleStyle ??= new GUIStyle(GUI.skin.label)
    {
      fontStyle = FontStyle.Bold,
      fontSize = 12,
      alignment = TextAnchor.MiddleLeft,
      // A fresh state, assigning GUI.skin.label.normal directly would recolor every label
      normal = new GUIStyleState { textColor = new Color(1f, 0.9f, 0.55f) }
    };
    GUILayout.Label(title, _tabTitleStyle, GUILayout.ExpandWidth(true));
  }

  // Height of the info bar that shows the pinned description, the taller box fits the larger font
  private const float InfoBarHeight = 110f;

  private static void DrawInfoBar()
  {
    GUILayout.Box("", GUILayout.Height(4f));
    _infoStyle ??= new GUIStyle(GUI.skin.box)
    {
      alignment = TextAnchor.UpperLeft,
      wordWrap = true,
      fontSize = 14,
      padding = new RectOffset(10, 10, 8, 8),
    };
    GUILayout.Box(
      string.IsNullOrEmpty(_infoText)
        ? "Click the ? button next to any setting to see its description here."
        : _infoText, _infoStyle, GUILayout.Height(InfoBarHeight));
  }

  private static bool IsHiddenFromUi(ConfigEntryBase entry)
  {
    // Save Cursor Position only appears in the enemy spawner window
    return entry.Definition.Section == "Hotkeys" && entry.Definition.Key == "Save Cursor Position";
  }

  private static void DrawConfigEntry(ConfigEntryBase entry)
  {
    if (IsHiddenFromUi(entry)) return;

    var key = entry.Definition.Section + "/" + entry.Definition.Key;
    var description = entry.Description?.Description ?? "";
    GUILayout.BeginHorizontal();
    _keyLabelStyle ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
    GUILayout.Label(entry.Definition.Key, _keyLabelStyle, GUILayout.Width(320f));
    if (string.IsNullOrEmpty(description))
    {
      GUILayout.Space(28f);
    }
    else if (GUILayout.Button("?", GUILayout.Width(24f)))
    {
      if (_infoKey == key)
      {
        _infoKey = "";
        _infoText = "";
      }
      else
      {
        _infoKey = key;
        _infoText = $"{entry.Definition.Key}:\n{description}";
      }
    }
    GUILayout.Space(4f);

    var type = entry.SettingType;
    try
    {
      if (type == typeof(bool))
      {
        var value = (bool)entry.BoxedValue;
        var newValue = GUILayout.Toggle(value, "");
        if (newValue != value) entry.BoxedValue = newValue;
      }
      else if (type == typeof(int))
      {
        if (!EditBuffers.TryGetValue(key, out var buffer))
        {
          buffer = ((int)entry.BoxedValue).ToString(CultureInfo.InvariantCulture);
          EditBuffers[key] = buffer;
        }
        var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(140f));
        if (newBuffer != buffer)
        {
          EditBuffers[key] = newBuffer;
          if (int.TryParse(newBuffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
          {
            if ((int)entry.BoxedValue != parsed) entry.BoxedValue = parsed;
          }
        }
      }
      else if (type == typeof(float))
      {
        if (!EditBuffers.TryGetValue(key, out var buffer))
        {
          buffer = ((float)entry.BoxedValue).ToString(CultureInfo.InvariantCulture);
          EditBuffers[key] = buffer;
        }
        var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(140f));
        if (newBuffer != buffer)
        {
          EditBuffers[key] = newBuffer;
          if (float.TryParse(newBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
          {
            if (Math.Abs((float)entry.BoxedValue - parsed) > 0.0001f) entry.BoxedValue = parsed;
          }
        }
      }
      else if (type == typeof(string))
      {
        var value = (string)entry.BoxedValue;
        var newValue = GUILayout.TextField(value, GUILayout.Width(300f));
        if (newValue != value) entry.BoxedValue = newValue;
      }
      else if (type == typeof(KeyboardShortcut))
      {
        DrawHotkeyEditor(entry);
      }
      else
      {
        GUILayout.Label(entry.GetSerializedValue(), GUILayout.Width(280f));
      }
    }
    catch (Exception e)
    {
      GUILayout.Label("Error: " + e.Message, GUILayout.Width(280f));
    }

    GUILayout.FlexibleSpace();
    if (GUILayout.Button("Reset", GUILayout.Width(64f)))
    {
      ResetConfigEntry(entry);
    }
    GUILayout.EndHorizontal();
  }

  // Puts a setting back to the default value it was bound with
  private static void ResetConfigEntry(ConfigEntryBase entry)
  {
    try
    {
      entry.BoxedValue = entry.DefaultValue;
      EditBuffers.Remove(entry.Definition.Section + "/" + entry.Definition.Key);
      SetStatus("Reset " + entry.Definition.Key + " to its default value");
    }
    catch (Exception e)
    {
      Plugin.Log.LogError("Failed to reset " + entry.Definition.Section + "/" + entry.Definition.Key + ": " + e);
      SetStatus("Error resetting " + entry.Definition.Key + ": " + e.Message);
    }
  }

  private static void DrawHotkeyEditor(ConfigEntryBase entry)
  {
    if (_editingHotkey == entry)
    {
      IsCapturingHotkey = true;
      // Drop text field focus so the key press is not consumed by an editor
      GUIUtility.keyboardControl = -1;
      var preview = _capture != null && _capture.Preview.Length > 0 ? " (" + _capture.Preview + ")" : "";
      GUILayout.Label("Press keys..." + preview + " (Esc cancels)", GUILayout.Width(260f));
    }
    else
    {
      GUILayout.Label(entry.GetSerializedValue(), GUILayout.Width(180f));
      if (!GUILayout.Button("Change", GUILayout.Width(70f))) return;
      _editingHotkey = entry;
      _capture = new HotkeyCapture();
      IsCapturingHotkey = true;
    }
  }

  // Polls the key capture every frame from Plugin.Update.
  // Returns true when the capture finished (committed or cancelled).
  public static bool PollHotkeyCapture()
  {
    if (_capture == null) return true;
    if (!_capture.Poll()) return false;
    _capture = null;
    return true;
  }

  private static readonly KeyCode[] AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

  // Keyboard keys only.
  // KeyCode values below 323 are the keyboard range (letters, numbers, function keys, modifiers), 323+ are mouse and joystick.
  private static bool IsKeyboardKey(KeyCode key)
  {
    var value = (int)key;
    return value is >= 8 and < 323;
  }

  private static bool IsModifierKey(KeyCode key)
  {
    return key is KeyCode.LeftControl or KeyCode.RightControl
        or KeyCode.LeftShift or KeyCode.RightShift
        or KeyCode.LeftAlt or KeyCode.RightAlt
        or KeyCode.LeftCommand or KeyCode.RightCommand;
  }

  // Stateful multi-frame key combo capture, same approach my Salt and Sacrifice mods.
  // Keys held when capture starts are ignored until released so the button that started the capture is not bound.
  // Newly pressed keys are recorded in order and the combo is committed only once everything has been released, so holding a key waits for the rest of the combo instead of binding immediately.
  // Real modifiers (Ctrl/Shift/Alt) become the modifier regardless of the order they were pressed in.
  private sealed class HotkeyCapture
  {
    private readonly HashSet<KeyCode> _ignore = [];
    private readonly List<KeyCode> _seq = [];
    private readonly HashSet<KeyCode> _seen = [];

    public HotkeyCapture()
    {
      foreach (var key in AllKeyCodes)
      {
        if (IsKeyboardKey(key) && Input.GetKey(key)) _ignore.Add(key);
      }
    }

    public string Preview => string.Join(" + ", _seq);

    // Returns true when the combo is complete (all inputs released)
    public bool Poll()
    {
      // Stop ignoring pre-held inputs once they have been released
      _ignore.RemoveWhere(key => !Input.GetKey(key));

      var anyHeld = false;
      foreach (var key in AllKeyCodes)
      {
        if (!IsKeyboardKey(key) || _ignore.Contains(key)) continue;
        if (!Input.GetKey(key)) continue;
        anyHeld = true;
        if (_seen.Add(key)) _seq.Add(key);
      }

      // Wait until something was pressed AND everything has been released
      if (_seq.Count == 0 || anyHeld) return false;

      if (_seq.Contains(KeyCode.Escape))
      {
        // Esc cancels
        _editingHotkey = null;
        IsCapturingHotkey = false;
        return true;
      }

      // Prefer a real modifier regardless of seen order, so a same-frame Ctrl+A is not inverted (Input.GetKey iteration is keycode-ordered)
      var mod = KeyCode.None;
      var main = KeyCode.None;
      foreach (var key in _seq)
      {
        if (IsModifierKey(key))
        {
          if (mod == KeyCode.None) mod = key;
        }
        else if (main == KeyCode.None)
        {
          main = key;
        }
      }

      KeyCode[] modifiers;
      if (mod != KeyCode.None && main != KeyCode.None)
      {
        modifiers = [mod];
      }
      else
      {
        // Single key, or only modifiers pressed: last pressed becomes the main key
        main = _seq[_seq.Count - 1];
        modifiers = _seq.Count >= 2 ? [_seq[0]] : [];
      }

      _editingHotkey.BoxedValue = new KeyboardShortcut(main, modifiers);
      _editingHotkey = null;
      IsCapturingHotkey = false;
      return true;
    }
  }

  private static void ItemSpawnerWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    if (!Player.Instance || !ItemsDatabase.Instance)
    {
      GUILayout.Label("Player or items database not available yet");
      return;
    }

    if (_allItems.Length == 0)
    {
      _allItems = [.. ItemsDatabase.Instance.itemsDict.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    GUILayout.BeginHorizontal();
    GUILayout.Label("Item ID", GUILayout.Width(70f));
    _itemName = GUILayout.TextField(_itemName, GUILayout.Width(200f));
    GUILayout.Label("Amount", GUILayout.Width(55f));
    _itemAmount = GUILayout.TextField(_itemAmount, GUILayout.Width(60f));
    if (GUILayout.Button("Give Item", GUILayout.Width(90f)))
    {
      SetStatus(int.TryParse(_itemAmount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount)
        ? GiveItem(_itemName, amount)
        : "Amount must be a number");
    }
    GUILayout.EndHorizontal();

    GUILayout.Space(6f);
    GUILayout.BeginHorizontal();
    GUILayout.Label("Search", GUILayout.Width(70f));
    _itemSearch = GUILayout.TextField(_itemSearch, GUILayout.Width(200f));
    GUILayout.EndHorizontal();

    var filtered = string.IsNullOrEmpty(_itemSearch)
      ? _allItems
      : [.. _allItems.Where(x => x.IndexOf(_itemSearch, StringComparison.OrdinalIgnoreCase) >= 0)];

    GUILayout.Space(6f);
    // Vertical list only, click an item to fill in its ID
    _itemScroll = GUILayout.BeginScrollView(_itemScroll);
    foreach (var item in filtered)
    {
      if (item == _itemName)
      {
        GUI.color = new Color(0.7f, 0.9f, 1f);
      }
      if (GUILayout.Button(item))
      {
        _itemName = item;
      }
      GUI.color = Color.white;
    }
    GUILayout.EndScrollView();

  }

  private static void EnemySpawnerWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    if (!Player.Instance)
    {
      GUILayout.Label("Player not available yet");
      return;
    }

    if (_enemyNames == null)
    {
      var valid = (from candidate in EnemyCandidates let prefab = Resources.Load("Prefabs/Characters/" + candidate) as GameObject where prefab && prefab.GetComponent<Character>() select candidate).ToList();
      _enemyNames = [.. valid];
      if (_enemyNames.Length == 0)
      {
        _enemyNames = EnemyCandidates;
      }
    }

    GUILayout.Space(4f);
    _spawnOnSavedPos = GUILayout.Toggle(_spawnOnSavedPos, "Spawn on saved position");
    if (_spawnOnSavedPos)
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label(_hasSavedCursorPos
        ? "Saved position: " + _savedCursorPos.x.ToString("F0", CultureInfo.InvariantCulture) + ", " + _savedCursorPos.z.ToString("F0", CultureInfo.InvariantCulture)
        : "No saved position yet", GUILayout.ExpandWidth(true));
      if (GUILayout.Button("Save now", GUILayout.Width(80f)))
      {
        SaveCursorPosition();
      }
      GUILayout.EndHorizontal();
    }

    GUILayout.BeginHorizontal();
    GUILayout.Label("Save Cursor Position", GUILayout.Width(160f));
    DrawHotkeyEditor(Plugin.KeybindSaveCursorPos);
    GUILayout.EndHorizontal();

    GUILayout.Space(4f);
    GUILayout.BeginHorizontal();
    GUILayout.Label("Character ID", GUILayout.Width(90f));
    _enemyName = GUILayout.TextField(_enemyName, GUILayout.Width(220f));
    if (GUILayout.Button("Spawn", GUILayout.Width(80f)))
    {
      SetStatus(SpawnCharacter(_enemyName));
    }
    GUILayout.EndHorizontal();

    GUILayout.Space(6f);
    GUILayout.BeginHorizontal();
    GUILayout.Label("Search", GUILayout.Width(70f));
    _enemySearch = GUILayout.TextField(_enemySearch, GUILayout.Width(200f));
    GUILayout.EndHorizontal();
    GUILayout.Label("Quick spawn (click a name)");
    // No fixed height: fills the remaining window space like the other windows
    _enemyScroll = GUILayout.BeginScrollView(_enemyScroll);
    var filteredEnemies = string.IsNullOrEmpty(_enemySearch)
      ? _enemyNames
      : [.. _enemyNames.Where(n => n.IndexOf(_enemySearch, StringComparison.OrdinalIgnoreCase) >= 0)];
    const int cols = 3;
    for (var i = 0; i < filteredEnemies.Length; i += cols)
    {
      GUILayout.BeginHorizontal();
      for (var j = 0; j < cols && i + j < filteredEnemies.Length; j++)
      {
        var name = filteredEnemies[i + j];
        // Shorten the displayed name so three columns fit without a horizontal scrollbar, the full ID is still used for spawning
        var display = name.StartsWith("FakeChars/", StringComparison.Ordinal) ? name.Substring("FakeChars/".Length) : name;
        if (!GUILayout.Button(display)) continue;
        _enemyName = name;
        SetStatus(SpawnCharacter(_enemyName));
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

  }

  private static void EffectManagerWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    if (!Player.Instance)
    {
      GUILayout.Label("Player not available yet");
      return;
    }

    GUILayout.BeginHorizontal();
    GUILayout.Label("Effects are applied to the player. Duration 0 means permanent. Active effects are marked in green.", GUILayout.ExpandWidth(true));
    if (GUILayout.Button("?", GUILayout.Width(24f)))
    {
      ShowInfo("Effect Manager", "Effect Manager:\nApplies and removes effects on the player. Duration 0 keeps the effect forever, what the modifier does depends on the effect type, the interval is used by effects that tick over time. The ? buttons next to each field explain them, the game ID of the effect is the type name.\nDisable keeps an effect off the player, Re-apply on loss puts it back with the values from this row whenever the game drops it, for example on death or sleep. Both are saved in your custom character effects config under the effect type.\nEffects are saved to your custom config as you get them.");
    }
    if (GUILayout.Button("Remove All", GUILayout.Width(110f)))
    {
      try
      {
        var currentEffects = GetPlayerEffects();
        if (currentEffects) currentEffects.removeAllEffects();
        SetStatus("Removed all effects");
      }
      catch (Exception e)
      {
        Plugin.Log.LogError(e);
        SetStatus("Error removing effects: " + e.Message);
      }
    }
    GUILayout.EndHorizontal();

    _effectScroll = GUILayout.BeginScrollView(_effectScroll);
    foreach (CharacterEffectType type in Enum.GetValues(typeof(CharacterEffectType)))
    {
      DrawEffectRow(type);
    }
    GUILayout.EndScrollView();

    // Descriptions are pinned here by the ? buttons of the effect rows
    DrawInfoBar();
  }

  private static void CustomDataFilesWindow(int id)
  {
    DrawWindowHeader();
    // Editor messages show up here rather than in the editor window, where the extra line would shift the whole view
    if (!DrawEditorStatus()) DrawStatus();

    GUILayout.Label("Select a file to edit in the editor window", GUILayout.ExpandWidth(true));
    GUILayout.Space(4f);

    foreach (var file in DataFiles)
    {
      if (file == _selectedDataFile)
      {
        GUI.color = new Color(0.7f, 0.9f, 1f);
      }
      if (GUILayout.Button(file.Name, GUILayout.Height(34f)))
      {
        SelectDataFile(file);
      }
      GUI.color = Color.white;
    }

    GUILayout.Space(4f);
    GUILayout.Label("Changes apply with the Save button in the editor", GUILayout.ExpandWidth(true));

  }

  private static void SelectDataFile(DataFile file)
  {
    _selectedDataFile = file;
    _dataSelectedKey = "";
    _dataNewKey = "";
    _infoKey = "";
    _infoText = "";
    PropBuffers.Clear();
    // Close every nested editor so switching files never keeps editing a path from the old file
    ClearSubPicker();
    ClearSoundPicker();
    ClearIdPicker();
  }

  private static void ClearSubPicker()
  {
    _subPickerKey = "";
    _subPickerProp = "";
    _subPickerNewItem = "";
    SubPickerPath.Clear();
  }

  private static void ClearSoundPicker()
  {
    _soundPickerFor = "";
    _soundPickerSearch = "";
  }

  private static void ClearIdPicker()
  {
    _idPickerKey = "";
    _idPickerMode = "";
    _idPickerName = "";
    _idPickerIndex = -1;
    _idPickerSearch = "";
  }

  private static void OpenIdPicker(string key, string mode, string name = "", int index = -1)
  {
    _idPickerKey = key;
    _idPickerMode = mode;
    _idPickerName = name;
    _idPickerIndex = index;
    _idPickerSearch = "";
    if (_allItems.Length == 0) _allItems = GetItemIds();
  }

  // Item IDs come from the game's item table when a game is loaded, and from the mod's own item configs otherwise (the main menu has no item table, for example).
  // Both hold the same IDs.
  private static string[] GetItemIds()
  {
    var ids = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
    if (ItemsDatabase.Instance)
    {
      foreach (var key in ItemsDatabase.Instance.itemsDict.Keys) ids.Add(key);
    }
    foreach (var property in Plugin.DefaultCustomItems.Properties()) ids.Add(property.Name);
    foreach (var property in Plugin.CustomItems.Properties()) ids.Add(property.Name);
    return [.. ids];
  }

  // Resolves the token the ID picker writes into.
  // It is either the selected entry itself or the nested container the generic editor is currently on.
  private static JToken ResolveIdPickerTarget(JObject entry)
  {
    if (string.IsNullOrEmpty(_subPickerKey) || string.IsNullOrEmpty(_subPickerProp)) return entry;
    var data = _selectedDataFile?.Get();
    if (data?[_subPickerKey] is not JObject obj || obj[_subPickerProp] == null) return entry;
    var token = obj[_subPickerProp];
    return ResolveSubPickerPath(token) ?? token;
  }

  // Searchable list of item IDs, click one to use it. Opened by the Pick buttons.
  private static bool DrawItemIdPicker(JObject entry)
  {
    if (string.IsNullOrEmpty(_idPickerKey) && string.IsNullOrEmpty(_idPickerMode)) return false;

    GUILayout.BeginVertical("box");
    GUILayout.BeginHorizontal();
    GUILayout.Label(_idPickerMode == "" ? "Pick an item ID for " + _idPickerKey : "Pick an item ID to add", GUILayout.ExpandWidth(true));
    if (GUILayout.Button("Close", GUILayout.Width(60f)))
    {
      ClearIdPicker();
      GUILayout.EndHorizontal();
      GUILayout.EndVertical();
      return true;
    }
    GUILayout.EndHorizontal();
    _idPickerSearch = GUILayout.TextField(_idPickerSearch, GUILayout.Width(220f));

    if (_allItems.Length == 0) _allItems = GetItemIds();
    var filtered = string.IsNullOrEmpty(_idPickerSearch)
      ? _allItems
      : [.. _allItems.Where(x => x.IndexOf(_idPickerSearch, StringComparison.OrdinalIgnoreCase) >= 0)];

    if (filtered.Length == 0)
    {
      GUILayout.Label(_allItems.Length == 0
        ? "No item IDs known yet. Load a save once so the mod can read the game's item list."
        : "No item ID matches the search.", GUILayout.ExpandWidth(true));
    }
    _idPickerScroll = GUILayout.BeginScrollView(_idPickerScroll, GUILayout.Height(Mathf.Max(GetWindowRect(9006).height * 0.4f, 120f)));
    foreach (var itemId in filtered)
    {
      if (!GUILayout.Button(itemId, _leftButtonStyle)) continue;
      ApplyItemIdPick(ResolveIdPickerTarget(entry), itemId);
      // The text fields of the top level rows are buffered, so the buffers have to be reloaded
      // for the picked value to show up
      SyncPropBuffers(entry);
      GUILayout.EndScrollView();
      GUILayout.EndVertical();
      return true;
    }
    GUILayout.EndScrollView();
    GUILayout.EndVertical();
    return true;
  }

  private static void ApplyItemIdPick(JToken target, string itemId)
  {
    switch (_idPickerMode)
    {
      case "loot":
        if (target is JArray lootItems)
        {
          lootItems.Add(new JObject
          {
            { "item", itemId },
            { "minAmount", 1 },
            { "maxAmount", 1 },
            { "chance", 1f },
          });
        }
        break;
      case "requirement":
        if (target is JObject requirements) requirements[itemId] = 1;
        break;
      case "preset":
        if (target is JObject presets && !string.IsNullOrEmpty(_idPickerName) && presets[_idPickerName] is JObject preset)
        {
          preset[itemId] = new JObject
          {
            { "type", itemId },
            { "amountMin", 0 },
            { "amountMax", 1 },
            { "chance", 0.5f },
          };
        }
        break;
      default:
        if (target is JArray array && _idPickerIndex >= 0 && _idPickerIndex < array.Count && array[_idPickerIndex] is JObject row)
        {
          row[_idPickerKey] = itemId;
        }
        else if (!string.IsNullOrEmpty(_idPickerName) && target[_idPickerName] is JObject named)
        {
          named[_idPickerKey] = itemId;
        }
        else if (target is JObject entryObject)
        {
          entryObject[_idPickerKey] = itemId;
        }
        break;
    }

    SetEditorStatus("Picked " + itemId);
    ClearIdPicker();
  }

  // Closes the nested editors and loads the buffers for the newly selected key
  private static void SelectDataKey(JToken value, string key)
  {
    _dataSelectedKey = key;
    _infoKey = "";
    _infoText = "";
    ClearSubPicker();
    ClearSoundPicker();
    SyncPropBuffers(value);
  }

  private static void CustomDataEditorWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    if (_selectedDataFile == null)
    {
      GUILayout.Label("Select a file in the Custom Data window", GUILayout.ExpandWidth(true));

      return;
    }

    // The settings of this file (loading toggles and readmes) sit above the save and reload buttons
    if (_selectedDataFile.Settings.Length > 0)
    {
      var byKey = new Dictionary<string, ConfigEntryBase>();
      foreach (var entry in GetEntries())
      {
        byKey[entry.Definition.Section + "/" + entry.Definition.Key] = entry;
      }
      DrawDivider();
      DrawGroupTitle(_selectedDataFile.Name + " settings");
      DrawDivider();
      foreach (var key in _selectedDataFile.Settings)
      {
        if (byKey.TryGetValue(key, out var entry)) DrawConfigEntry(entry);
      }
    }

    GUILayout.Space(4f);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Save", GUILayout.Width(70f)))
    {
      SaveDataFile();
    }
    if (GUILayout.Button("Reload", GUILayout.Width(70f)))
    {
      SelectDataFile(_selectedDataFile);
    }
    GUILayout.FlexibleSpace();
    GUILayout.Label(_selectedDataFile.Name, GUILayout.ExpandWidth(true));
    GUILayout.EndHorizontal();

    // Add and delete entries right under Save and Reload
    GUILayout.BeginHorizontal();
    _dataNewKey = GUILayout.TextField(_dataNewKey, GUILayout.Width(210f));
    if (GUILayout.Button("Add", GUILayout.Width(55f)))
    {
      AddDataKey();
    }
    var hadSelection = !string.IsNullOrEmpty(_dataSelectedKey);
    GUI.enabled = hadSelection;
    if (GUILayout.Button("Delete Selected", GUILayout.Width(130f)))
    {
      DeleteSelectedDataKey();
    }
    GUI.enabled = true;
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();

    var data = _selectedDataFile.Get();

    GUILayout.BeginHorizontal();
    // Left: top level keys of the json file, clicking one edits its object
    GUILayout.BeginVertical(GUILayout.Width(320f));
    GUILayout.BeginHorizontal();
    GUILayout.Label("Search", GUILayout.Width(50f));
    _dataKeysSearch = GUILayout.TextField(_dataKeysSearch, GUILayout.Width(260f));
    GUILayout.EndHorizontal();
    _dataKeysScroll = GUILayout.BeginScrollView(_dataKeysScroll);
    if (data != null)
    {
      // Left aligned buttons so long names are readable instead of clipped
      _leftButtonStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
      var filtered = string.IsNullOrEmpty(_dataKeysSearch)
        ? data.Properties()
        : data.Properties().Where(p => p.Name.IndexOf(_dataKeysSearch, StringComparison.OrdinalIgnoreCase) >= 0);
      foreach (var prop in filtered)
      {
        if (prop.Name == _dataSelectedKey)
        {
          GUI.color = new Color(0.7f, 0.9f, 1f);
        }
        if (GUILayout.Button(prop.Name, _leftButtonStyle))
        {
          SelectDataKey(prop.Value, prop.Name);
        }
        GUI.color = Color.white;
      }
    }
    GUILayout.EndScrollView();

    // Show the keys of the current editor that the selected entry does not have yet, so any
    // key from the wiki can be added without knowing its exact name.
    if (!string.IsNullOrEmpty(_dataSelectedKey) && data?[_dataSelectedKey] is JObject selectedEntry)
    {
      DrawAvailableKeysPicker(selectedEntry);
    }
    GUILayout.EndVertical();

    // Right: editor for the selected key's properties
    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
    GUILayout.Label(string.IsNullOrEmpty(_dataSelectedKey) ? "Select a key on the left" : "Editing: " + _dataSelectedKey, GUILayout.ExpandWidth(true));
    _dataPropsScroll = GUILayout.BeginScrollView(_dataPropsScroll);
    if (!string.IsNullOrEmpty(_dataSelectedKey) && data != null && data[_dataSelectedKey] is JObject obj)
    {
      // Sound picker takes priority, then the item ID picker, then the sub-picker, then the raw editor
      if (!DrawSoundPicker() && !DrawItemIdPicker(obj) && !DrawSubPicker())
      {
        DrawObjectEditor(obj);
      }
    }
    GUILayout.EndScrollView();
    GUILayout.EndVertical();
    GUILayout.EndHorizontal();

    // Clicking the ? button next to a property shows its description here
    DrawInfoBar();
  }

  // Keys of the current editor that are missing on the selected entry, shown as buttons that add them with a default value.
  // Hidden when there is nothing left to add, and its height follows the number of keys, up to half the editor height for long lists like Custom Items.
  private static void DrawAvailableKeysPicker(JObject target)
  {
    var keys = AvailableKeysFor(_selectedDataFile.Name);
    if (keys.Length == 0) return;

    var available = keys.Where(key => !target.ContainsKey(key.Name)).ToList();
    if (!string.IsNullOrEmpty(_itemKeysSearch))
      available = available.Where(key => key.Name.IndexOf(_itemKeysSearch, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
    if (available.Count == 0 && !Plugin.AlwaysShowKeyPickers.Value) return;

    GUILayout.Space(4f);
    GUILayout.Label(available.Count == 0 ? "Available keys: nothing left to add" : "Available keys (" + available.Count + "):", GUILayout.ExpandWidth(true));
    GUILayout.BeginHorizontal();
    GUILayout.Label("Search", GUILayout.Width(50f));
    _itemKeysSearch = GUILayout.TextField(_itemKeysSearch, GUILayout.Width(220f));
    GUILayout.EndHorizontal();
    var editorHeight = GetWindowRect(9006).height;
    // The picker shares the sidebar with the entry list, so it follows the number of keys but stays around a quarter of the editor height and leaves the rest to the list.
    var listHeight = Mathf.Clamp(available.Count * 26f + 30f, 60f, editorHeight * 0.25f);
    _itemKeysScroll = GUILayout.BeginScrollView(_itemKeysScroll, GUILayout.Height(listHeight));
    foreach (var key in available)
    {
      GUILayout.BeginHorizontal();
      if (GUILayout.Button(key.Name, _leftButtonStyle))
      {
        target[key.Name] = key.Default.DeepClone();
        SyncPropBuffers(target);
        SetEditorStatus("Added key " + key.Name);
      }
      DrawPropertyInfoButton(key.Name);
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();
  }

  // True for keys whose value is an item ID, so a Pick button can offer the list of item IDs
  private static bool IsItemIdKey(string key)
  {
    return key is "item" or "type" or "resource" or "ammoType" or "rottenItem" or "iconType";
  }

  // Moves an object key one place up or down, keeping the rest of the order.
  // Used by the up and down buttons so keys can be re-ordered without a text editor.
  private static void MoveObjectKey(JObject obj, string name, int delta)
  {
    var props = obj.Properties().ToList();
    var index = props.FindIndex(p => p.Name == name);
    if (index < 0) return;
    var target = index + delta;
    if (target < 0 || target >= props.Count) return;
    var value = props[index].Value;
    props[index].Remove();
    if (delta > 0) props[target].AddAfterSelf(new JProperty(name, value));
    else props[target].AddBeforeSelf(new JProperty(name, value));
  }

  // Moves an array entry one place up or down
  private static void MoveArrayEntry(JArray array, int index, int delta)
  {
    var target = index + delta;
    if (index < 0 || target < 0 || target >= array.Count) return;
    var value = array[index];
    array.RemoveAt(index);
    array.Insert(target, value);
  }

  private static void SyncPropBuffers(JToken value)
  {
    PropBuffers.Clear();
    if (value is not JObject obj) return;
    foreach (var prop in obj.Properties())
    {
      PropBuffers[prop.Name] = prop.Value.Type == JTokenType.String ? prop.Value.Value<string>() : prop.Value.ToString(Formatting.None);
    }
  }

  private static void DrawObjectEditor(JObject obj)
  {
    foreach (var prop in obj.Properties())
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label(prop.Name, GUILayout.Width(160f));
      if (GUILayout.Button("?", GUILayout.Width(24f)))
      {
        if (_infoKey == prop.Name)
        {
          _infoKey = "";
          _infoText = "";
        }
        else
        {
          _infoKey = prop.Name;
          var desc = GetPropertyDescription(prop.Name);
          _infoText = desc != null
            ? prop.Name + ":\n" + desc
            : prop.Name + ":\nNo description available.";
        }
      }
      if (!PropBuffers.TryGetValue(prop.Name, out var buffer))
      {
        buffer = prop.Value.Type == JTokenType.String ? prop.Value.Value<string>() : prop.Value.ToString(Formatting.None);
        PropBuffers[prop.Name] = buffer;
      }
      // Fixed width value area so the Up and Down buttons line up on every row, whatever the value is (a narrow checkbox, a text field, or an Edit button with a size label).
      GUILayout.BeginHorizontal(GUILayout.Width(ValueColumnWidth));
      switch (prop.Value.Type)
      {
        case JTokenType.Boolean:
        {
          var val = (bool)prop.Value;
          var newValue = GUILayout.Toggle(val, "");
          if (newValue != val) prop.Value = newValue;
          break;
        }
        case JTokenType.Integer:
        {
          var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(140f));
          if (newBuffer != buffer)
          {
            PropBuffers[prop.Name] = newBuffer;
            if (int.TryParse(newBuffer, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
              if ((int)prop.Value != parsed) prop.Value = parsed;
            }
          }
          break;
        }
        case JTokenType.Float:
        {
          var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(140f));
          if (newBuffer != buffer)
          {
            PropBuffers[prop.Name] = newBuffer;
            if (float.TryParse(newBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
              if (Math.Abs((float)prop.Value - parsed) > 0.0001f) prop.Value = parsed;
            }
          }
          break;
        }
        case JTokenType.Object:
        case JTokenType.Array:
        {
          // Every nested object and array opens in its own structured editor, raw json text fields are not used for nested values anymore
          if (GUILayout.Button("Edit...", GUILayout.Width(70f)))
          {
            _subPickerKey = _dataSelectedKey;
            _subPickerProp = prop.Name;
            _subPickerNewItem = "";
            SubPickerPath.Clear();
          }
          GUILayout.Label(DescribeToken(prop.Value), GUILayout.Width(SizeLabelWidth));
          break;
        }
        case JTokenType.None:
        case JTokenType.Constructor:
        case JTokenType.Property:
        case JTokenType.Comment:
        case JTokenType.String:
        case JTokenType.Null:
        case JTokenType.Undefined:
        case JTokenType.Date:
        case JTokenType.Raw:
        case JTokenType.Bytes:
        case JTokenType.Guid:
        case JTokenType.Uri:
        case JTokenType.TimeSpan:
        default:
        {
          var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(200f));
          if (newBuffer != buffer)
          {
            PropBuffers[prop.Name] = newBuffer;
            prop.Value = newBuffer;
          }
          // Sound picker for the activateSound property of character effects
          if (_selectedDataFile?.Name == "Custom Character Effects" && prop.Name == "activateSound")
          {
            if (GUILayout.Button("Pick", GUILayout.Width(50f)))
            {
              _soundPickerFor = _dataSelectedKey;
              _soundPickerSearch = "";
              LoadAllSounds();
            }
          }
          // Item ID picker for keys that hold an item ID
          if (IsItemIdKey(prop.Name))
          {
            if (GUILayout.Button("Pick", GUILayout.Width(50f)))
            {
              OpenIdPicker(prop.Name, "");
            }
          }
          break;
        }
      }
      GUILayout.EndHorizontal();
      if (GUILayout.Button("Up", GUILayout.Width(40f)))
      {
        MoveObjectKey(obj, prop.Name, -1);
        GUILayout.EndHorizontal();
        return;
      }
      if (GUILayout.Button("Down", GUILayout.Width(46f)))
      {
        MoveObjectKey(obj, prop.Name, 1);
        GUILayout.EndHorizontal();
        return;
      }
      GUILayout.EndHorizontal();
    }
  }

  // Loads all sound IDs from the game's audio categories
  private static void LoadAllSounds()
  {
    _allSounds = [];
    try
    {
      var controller = SingletonMonoBehaviour<AudioController>.Instance;
      if (!controller || controller.AudioCategories == null) return;
      var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      var sounds = (from category in controller.AudioCategories where category?.AudioItems != null from audioItem in category.AudioItems where audioItem != null && !string.IsNullOrEmpty(audioItem.Name) && seen.Add(audioItem.Name) select audioItem.Name).ToList();
      sounds.Sort(StringComparer.OrdinalIgnoreCase);
      _allSounds = [.. sounds];
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
    }
  }

  // Draws the sound picker for the effect currently in _soundPickerFor.
  // Returns true while the picker is active.
  private static bool DrawSoundPicker()
  {
    if (string.IsNullOrEmpty(_soundPickerFor)) return false;
    var data = _selectedDataFile.Get();
    if (data?[_soundPickerFor] is not JObject effect) return false;

    GUILayout.BeginVertical("box");
    GUILayout.BeginHorizontal();
    GUILayout.Label("Pick a sound for " + _soundPickerFor, GUILayout.ExpandWidth(true));
    GUILayout.Label("Search", GUILayout.Width(50f));
    _soundPickerSearch = GUILayout.TextField(_soundPickerSearch, GUILayout.Width(160f));
    if (GUILayout.Button("Close", GUILayout.Width(60f)))
    {
      _soundPickerFor = "";
      return false;
    }
    GUILayout.EndHorizontal();

    var filtered = string.IsNullOrEmpty(_soundPickerSearch)
      ? _allSounds
      : [.. _allSounds.Where(s => s.IndexOf(_soundPickerSearch, StringComparison.OrdinalIgnoreCase) >= 0)];

    _soundPickerScroll = GUILayout.BeginScrollView(_soundPickerScroll);
    foreach (var sound in filtered)
    {
      if (!GUILayout.Button(sound, _leftButtonStyle)) continue;
      effect["activateSound"] = sound;
      PropBuffers["activateSound"] = sound;
      _soundPickerFor = "";
      SetStatus("Sound set to " + sound);
      return false;
    }
    GUILayout.EndScrollView();

    GUILayout.EndVertical();
    return true;
  }

  // Which nested properties get their own purpose built picker instead of the generic editor
  private static bool IsSubPickerProperty(string propName)
  {
    if (_selectedDataFile == null) return false;
    return _selectedDataFile.Name switch
    {
      "Custom Loot" => propName == "items",
      "Custom Random Inventories" => propName == "presets",
      "Custom Characters" => propName == "Attacks",
      "Custom Crafting Recipes" => propName == "requirements",
      "Custom Items" => propName == "requirements",
      _ => false
    };
  }

  // Draws the editor for the currently open nested value.
  // Every object and array gets a structured editor: the known ones (items, presets, Attacks, requirements) use their own picker, everything else uses the generic editor.
  // The editor never creates missing properties. If the selected key does not have the property (for example after switching to another key in the sidebar), it closes instead of writing a new key into the file.
  private static bool DrawSubPicker()
  {
    if (string.IsNullOrEmpty(_subPickerKey) || string.IsNullOrEmpty(_subPickerProp) || _selectedDataFile == null) return false;
    var data = _selectedDataFile.Get();
    if (data?[_subPickerKey] is not JObject obj || obj[_subPickerProp] == null)
    {
      ClearSubPicker();
      return false;
    }

    var token = obj[_subPickerProp];
    GUILayout.BeginVertical("box");
    GUILayout.BeginHorizontal();
    var path = SubPickerPath.Count > 0 ? " / " + string.Join(" / ", SubPickerPath) : "";
    GUILayout.Label("Editing " + _subPickerKey + " / " + _subPickerProp + path, GUILayout.ExpandWidth(true));
    if (SubPickerPath.Count > 0 && GUILayout.Button("Back", GUILayout.Width(60f)))
    {
      SubPickerPath.RemoveAt(SubPickerPath.Count - 1);
    }
    if (GUILayout.Button("Close", GUILayout.Width(60f)))
    {
      ClearSubPicker();
      return false;
    }
    GUILayout.EndHorizontal();

    if (IsSubPickerProperty(_subPickerProp) && SubPickerPath.Count == 0)
    {
      switch (_selectedDataFile.Name)
      {
        case "Custom Loot":
          DrawLootItemsPicker(token);
          break;
        case "Custom Random Inventories":
          DrawRandomInvPresetsPicker(token);
          break;
        case "Custom Characters":
          DrawAttacksPicker(token);
          break;
        case "Custom Crafting Recipes":
        case "Custom Items":
          DrawRequirementsPicker(token);
          break;
      }
    }
    else
    {
      DrawGenericTokenEditor(token);
    }

    GUILayout.EndVertical();
    return true;
  }

  // Resolves the token the generic editor points at: the sub-picker property plus the path
  private static JToken ResolveSubPickerPath(JToken root)
  {
    var token = root;
    foreach (var step in SubPickerPath)
    {
      token = ChildOf(token, step);
      if (token == null) return null;
    }
    return token;
  }

  // Reads a child by key. Arrays use the index as a string so one path type works for both
  private static JToken ChildOf(JToken parent, string key)
  {
    return parent switch
    {
      null => null,
      JArray array => int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
                      index >= 0 && index < array.Count
        ? array[index]
        : null,
      _ => parent[key]
    };
  }

  private static void SetChildOf(JToken parent, string key, JToken value)
  {
    if (parent is JArray array)
    {
      if (int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < array.Count) array[index] = value;
      return;
    }
    parent[key] = value;
  }

  private static string DescribeToken(JToken token)
  {
    return token switch
    {
      JObject obj => obj.Count + " items",
      JArray array => array.Count + " items",
      _ => token.ToString(Formatting.None)
    };
  }

  // Generic structured editor for any nested object or array, used when there is no purpose built picker for the property. Nested values open in place through the path stack.
  private static void DrawGenericTokenEditor(JToken root)
  {
    var token = ResolveSubPickerPath(root);
    switch (token)
    {
      case null:
        GUILayout.Label("The value was removed", GUILayout.ExpandWidth(true));
        return;
      case JObject obj:
        DrawGenericObjectEditor(obj);
        return;
      case JArray array:
        DrawGenericArrayEditor(array);
        return;
      default:
        GUILayout.Label("This is a single value, edit it from the property list", GUILayout.ExpandWidth(true));
        break;
    }
  }

  private static void DrawGenericObjectEditor(JObject obj)
  {
    _subPickerScroll = GUILayout.BeginScrollView(_subPickerScroll);
    foreach (var prop in obj.Properties().ToList())
    {
      // The key can be renamed, do that after the row so the enumeration stays valid
      var keyName = prop.Name;
      GUILayout.BeginHorizontal();
      var newName = GUILayout.TextField(keyName, GUILayout.Width(200f));
      // Fixed width value area so the buttons line up on every row
      GUILayout.BeginHorizontal(GUILayout.Width(ValueColumnWidth));
      DrawGenericValueEditor(obj, keyName);
      GUILayout.EndHorizontal();
      DrawPropertyInfoButton(keyName);
      if (GUILayout.Button("Up", GUILayout.Width(40f)))
      {
        MoveObjectKey(obj, keyName, -1);
        GUILayout.EndHorizontal();
        return;
      }
      if (GUILayout.Button("Down", GUILayout.Width(46f)))
      {
        MoveObjectKey(obj, keyName, 1);
        GUILayout.EndHorizontal();
        return;
      }
      var removed = GUILayout.Button("X", GUILayout.Width(24f));
      GUILayout.EndHorizontal();

      if (removed)
      {
        prop.Remove();
        return;
      }
      if (newName != keyName)
      {
        if (string.IsNullOrEmpty(newName) || obj.ContainsKey(newName)) continue;
        var value = prop.Value;
        prop.Remove();
        obj.Add(newName, value);
        return;
      }
    }
    GUILayout.EndScrollView();

    GUILayout.BeginHorizontal();
    _subPickerNewItem = GUILayout.TextField(_subPickerNewItem, GUILayout.Width(200f));
    if (GUILayout.Button("Add Key", GUILayout.Width(80f)) && !string.IsNullOrEmpty(_subPickerNewItem) && !obj.ContainsKey(_subPickerNewItem))
    {
      obj[_subPickerNewItem] = "";
      _subPickerNewItem = "";
      SetEditorStatus("Added key, edit its value on the new row");
    }
    GUILayout.EndHorizontal();
  }

  private static void DrawGenericArrayEditor(JArray array)
  {
    _subPickerScroll = GUILayout.BeginScrollView(_subPickerScroll);
    for (var i = 0; i < array.Count; i++)
    {
      var keyName = i.ToString(CultureInfo.InvariantCulture);
      GUILayout.BeginHorizontal();
      GUILayout.Label("Item " + i, GUILayout.Width(70f));
      // Fixed width value area so the buttons line up on every row
      GUILayout.BeginHorizontal(GUILayout.Width(ValueColumnWidth));
      DrawGenericValueEditor(array, keyName);
      GUILayout.EndHorizontal();
      DrawPropertyInfoButton(array[i] is JObject item ? item.Properties().FirstOrDefault()?.Name ?? "" : "");
      if (GUILayout.Button("Up", GUILayout.Width(40f)))
      {
        MoveArrayEntry(array, i, -1);
        GUILayout.EndHorizontal();
        break;
      }
      if (GUILayout.Button("Down", GUILayout.Width(46f)))
      {
        MoveArrayEntry(array, i, 1);
        GUILayout.EndHorizontal();
        break;
      }
      if (GUILayout.Button("X", GUILayout.Width(24f)))
      {
        array.RemoveAt(i);
        i--;
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

    GUILayout.BeginHorizontal();
    if (array.Count == 0 || array[0] is JValue)
    {
      _subPickerNewItem = GUILayout.TextField(_subPickerNewItem, GUILayout.Width(200f));
      if (GUILayout.Button("Add Item", GUILayout.Width(80f)))
      {
        array.Add(_subPickerNewItem);
        _subPickerNewItem = "";
      }
    }
    else
    {
      if (GUILayout.Button("Add Object", GUILayout.Width(100f))) array.Add(new JObject());
      if (GUILayout.Button("Add List", GUILayout.Width(90f))) array.Add(new JArray());
    }
    GUILayout.EndHorizontal();
  }

  // Draws the editor for one value inside a generic object or array editor
  private static void DrawGenericValueEditor(JToken parent, string keyName)
  {
    var value = ChildOf(parent, keyName);
    if (value == null) return;
    switch (value.Type)
    {
      case JTokenType.Boolean:
      {
        var current = (bool)value;
        var newValue = GUILayout.Toggle(current, "", GUILayout.Width(24f));
        if (newValue != current) SetChildOf(parent, keyName, newValue);
        break;
      }
      case JTokenType.Integer:
      {
        var current = value.Value<int>();
        var newValue = GUILayout.TextField(current.ToString(CultureInfo.InvariantCulture), GUILayout.Width(120f));
        if (newValue != current.ToString(CultureInfo.InvariantCulture)
            && int.TryParse(newValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed != current)
        {
          SetChildOf(parent, keyName, parsed);
        }
        break;
      }
      case JTokenType.Float:
      {
        var current = value.Value<float>();
        var newValue = GUILayout.TextField(current.ToString(CultureInfo.InvariantCulture), GUILayout.Width(120f));
        if (newValue != current.ToString(CultureInfo.InvariantCulture)
            && float.TryParse(newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && Math.Abs(parsed - current) > 0.0001f)
        {
          SetChildOf(parent, keyName, parsed);
        }
        break;
      }
      case JTokenType.Object:
      case JTokenType.Array:
      {
        if (GUILayout.Button("Open", GUILayout.Width(60f)))
        {
          SubPickerPath.Add(keyName);
        }
        GUILayout.Label(DescribeToken(value), GUILayout.Width(SizeLabelWidth));
        break;
      }
      default:
      {
        var current = value.Type == JTokenType.Null ? "" : value.Value<string>();
        var newValue = GUILayout.TextField(current, GUILayout.Width(240f));
        if (newValue != current) SetChildOf(parent, keyName, newValue);
        // Item ID picker for keys that hold an item ID
        if (IsItemIdKey(keyName) && GUILayout.Button("Pick", GUILayout.Width(50f)))
        {
          OpenIdPicker(keyName, "");
        }
        break;
      }
    }
  }

  // Pins a description in the info bar, clicking the same key again clears it
  private static void ShowInfo(string key, string text)
  {
    if (_infoKey == key)
    {
      _infoKey = "";
      _infoText = "";
      return;
    }
    _infoKey = key;
    _infoText = text ?? key + ":\nNo description available.";
  }

  // Clicking the ? next to a nested property pins its description in the info bar
  private static void DrawPropertyInfoButton(string propName)
  {
    if (string.IsNullOrEmpty(propName))
    {
      GUILayout.Space(28f);
      return;
    }
    if (!GUILayout.Button("?", GUILayout.Width(24f))) return;
    var desc = GetPropertyDescription(propName);
    ShowInfo(propName, desc != null ? propName + ":\n" + desc : null);
  }

  // The effect manager rows use the character effect descriptions
  private static void DrawEffectInfoButton(string label, string descriptionKey)
  {
    if (!GUILayout.Button("?", GUILayout.Width(24f))) return;
    var desc = EffectKeyDescriptions.TryGetValue(descriptionKey, out var d) ? d : null;
    ShowInfo(label, desc != null ? label + ":\n" + desc : null);
  }

  private static void DrawLootItemsPicker(JToken itemsToken)
  {
    if (itemsToken is not JArray items)
    {
      GUILayout.Label("items is not an array", GUILayout.ExpandWidth(true));
      return;
    }

    _subPickerScroll = GUILayout.BeginScrollView(_subPickerScroll);
    for (var i = 0; i < items.Count; i++)
    {
      if (items[i] is not JObject item) continue;
      GUILayout.BeginHorizontal();
      GUILayout.Label("Slot " + i, GUILayout.Width(50f));
      var itemName = item["item"]?.Value<string>() ?? "Empty";
      var newName = GUILayout.TextField(itemName, GUILayout.Width(180f));
      if (newName != itemName) item["item"] = newName;
      DrawPropertyInfoButton("item");
      if (GUILayout.Button("Pick", GUILayout.Width(50f)))
      {
        OpenIdPicker("item", "", "", i);
      }
      GUILayout.Label("Min", GUILayout.Width(30f));
      var min = item["minAmount"]?.Value<int>() ?? 1;
      var newMin = GUILayout.TextField(min.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
      if (int.TryParse(newMin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMin) && parsedMin != min)
        item["minAmount"] = parsedMin;
      DrawPropertyInfoButton("minAmount");
      GUILayout.Label("Max", GUILayout.Width(30f));
      var max = item["maxAmount"]?.Value<int>() ?? 1;
      var newMax = GUILayout.TextField(max.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
      if (int.TryParse(newMax, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMax) && parsedMax != max)
        item["maxAmount"] = parsedMax;
      DrawPropertyInfoButton("maxAmount");
      GUILayout.Label("Chance", GUILayout.Width(50f));
      var chance = item["chance"]?.Value<float>() ?? 1f;
      var newChance = GUILayout.TextField(chance.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
      if (float.TryParse(newChance, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedChance) && Math.Abs(parsedChance - chance) > 0.0001f)
        item["chance"] = parsedChance;
      DrawPropertyInfoButton("chance");
      if (GUILayout.Button("X", GUILayout.Width(24f)))
      {
        items.RemoveAt(i);
        i--;
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

    GUILayout.BeginHorizontal();
    _subPickerNewItem = GUILayout.TextField(_subPickerNewItem, GUILayout.Width(180f));
    if (GUILayout.Button("Add Item", GUILayout.Width(80f)))
    {
      if (!string.IsNullOrEmpty(_subPickerNewItem))
      {
        items.Add(new JObject
        {
          { "item", _subPickerNewItem },
          { "minAmount", 1 },
          { "maxAmount", 1 },
          { "chance", 1f },
        });
        _subPickerNewItem = "";
      }
    }
    if (GUILayout.Button("Pick", GUILayout.Width(50f)))
    {
      OpenIdPicker("", "loot");
    }
    GUILayout.EndHorizontal();
  }

  private static void DrawRandomInvPresetsPicker(JToken presetsToken)
  {
    if (presetsToken is not JObject presets)
    {
      GUILayout.Label("presets is not an object", GUILayout.ExpandWidth(true));
      return;
    }

    _subPickerScroll = GUILayout.BeginScrollView(_subPickerScroll);
    var presetToRemove = "";
    foreach (var preset in presets.Properties())
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label("Preset " + preset.Name, GUILayout.ExpandWidth(true));
      if (GUILayout.Button("X", GUILayout.Width(24f)))
      {
        presetToRemove = preset.Name;
        GUILayout.EndHorizontal();
        break;
      }
      GUILayout.EndHorizontal();
      if (preset.Value is not JObject presetObj) continue;

      // Items are stored directly in the preset object, keyed by item type name
      var itemKeys = presetObj.Properties().Where(p => p.Value is JObject).ToList();
      foreach (var itemProp in itemKeys)
      {
        if (itemProp.Value is not JObject item) continue;
        GUILayout.BeginHorizontal();
        GUILayout.Space(20f);
        var typeName = item["type"]?.Value<string>() ?? itemProp.Name;
        var newType = GUILayout.TextField(typeName, GUILayout.Width(180f));
        DrawPropertyInfoButton("type");
        if (GUILayout.Button("Pick", GUILayout.Width(50f)))
        {
          OpenIdPicker("type", "", preset.Name);
        }
        if (newType != typeName)
        {
          item["type"] = newType;
          // Rename the key to match the new type
          if (newType != itemProp.Name)
          {
            var newProp = new JProperty(newType, item);
            itemProp.Remove();
            presetObj.Add(newProp);
          }
        }
        GUILayout.Label("Min", GUILayout.Width(30f));
        var min = item["amountMin"]?.Value<int>() ?? 0;
        var newMin = GUILayout.TextField(min.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
        if (int.TryParse(newMin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMin) && parsedMin != min)
          item["amountMin"] = parsedMin;
        DrawPropertyInfoButton("amountMin");
        GUILayout.Label("Max", GUILayout.Width(30f));
        var max = item["amountMax"]?.Value<int>() ?? 0;
        var newMax = GUILayout.TextField(max.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
        if (int.TryParse(newMax, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMax) && parsedMax != max)
          item["amountMax"] = parsedMax;
        DrawPropertyInfoButton("amountMax");
        GUILayout.Label("Chance", GUILayout.Width(50f));
        var chance = item["chance"]?.Value<float>() ?? 0f;
        var newChance = GUILayout.TextField(chance.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
        if (float.TryParse(newChance, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedChance) && Math.Abs(parsedChance - chance) > 0.0001f)
          item["chance"] = parsedChance;
        DrawPropertyInfoButton("chance");
        if (GUILayout.Button("X", GUILayout.Width(24f)))
        {
          itemProp.Remove();
        }
        GUILayout.EndHorizontal();
      }

      GUILayout.BeginHorizontal();
      GUILayout.Space(20f);
      _subPickerNewItem = GUILayout.TextField(_subPickerNewItem, GUILayout.Width(180f));
      if (GUILayout.Button("Add Item", GUILayout.Width(80f)))
      {
        if (!string.IsNullOrEmpty(_subPickerNewItem))
        {
          presetObj[_subPickerNewItem] = new JObject
          {
            { "type", _subPickerNewItem },
            { "amountMin", 0 },
            { "amountMax", 0 },
            { "chance", 0f },
          };
          _subPickerNewItem = "";
        }
      }
      if (GUILayout.Button("Pick", GUILayout.Width(50f)))
      {
        OpenIdPicker("", "preset", preset.Name);
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

    if (!string.IsNullOrEmpty(presetToRemove))
    {
      presets.Remove(presetToRemove);
      SetEditorStatus("Removed preset " + presetToRemove);
      return;
    }

    GUILayout.Space(6f);
    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Add Preset", GUILayout.Width(120f)))
    {
      var nextIndex = 0;
      foreach (var preset in presets.Properties())
      {
        if (int.TryParse(preset.Name, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) && index >= nextIndex) nextIndex = index + 1;
      }
      presets[nextIndex.ToString(CultureInfo.InvariantCulture)] = new JObject();
      SetEditorStatus("Added preset " + nextIndex + ", add items to it");
    }
    GUILayout.EndHorizontal();
  }

  private static void DrawAttacksPicker(JToken attacksToken)
  {
    if (attacksToken is not JArray attacks)
    {
      GUILayout.Label("Attacks is not an array", GUILayout.ExpandWidth(true));
      return;
    }

    _subPickerScroll = GUILayout.BeginScrollView(_subPickerScroll);
    for (var i = 0; i < attacks.Count; i++)
    {
      // Each attack is an object with a single numbered key, e.g. { "1": { ... } }
      if (attacks[i] is not JObject attackWrapper) continue;
      var attackProp = attackWrapper.Properties().FirstOrDefault();
      if (attackProp is not { Value: JObject attack }) continue;

      GUILayout.BeginHorizontal();
      GUILayout.Label("Attack " + (i + 1), GUILayout.Width(70f));
      var damage = attack["Damage"]?.Value<int>() ?? 0;
      var newDamage = GUILayout.TextField(damage.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60f));
      if (int.TryParse(newDamage, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDamage) && parsedDamage != damage)
        attack["Damage"] = parsedDamage;
      DrawPropertyInfoButton("Damage");
      GUILayout.Label("Barricade", GUILayout.Width(70f));
      var barricade = attack["BarricadeDamage"]?.Value<int>() ?? 0;
      var newBarricade = GUILayout.TextField(barricade.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60f));
      if (int.TryParse(newBarricade, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBarricade) && parsedBarricade != barricade)
        attack["BarricadeDamage"] = parsedBarricade;
      DrawPropertyInfoButton("BarricadeDamage");
      if (GUILayout.Button("X", GUILayout.Width(24f)))
      {
        attacks.RemoveAt(i);
        i--;
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();

    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Add Attack", GUILayout.Width(90f)))
    {
      attacks.Add(new JObject
      {
        { (attacks.Count + 1).ToString(CultureInfo.InvariantCulture), new JObject
          {
            { "AttackName(ReadOnly)", "" },
            { "AttackIsRanged(ReadOnly)", false },
            { "Damage", 0 },
            { "BarricadeDamage", 0 },
          }
        }
      });
    }
    GUILayout.EndHorizontal();
  }

  private static void DrawRequirementsPicker(JToken requirementsToken)
  {
    if (requirementsToken is not JObject requirements)
    {
      GUILayout.Label("requirements is not an object", GUILayout.ExpandWidth(true));
      return;
    }

    _subPickerScroll = GUILayout.BeginScrollView(_subPickerScroll);
    foreach (var req in requirements.Properties().ToList())
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label("Item", GUILayout.Width(40f));
      var newName = GUILayout.TextField(req.Name, GUILayout.Width(200f));
      DrawPropertyInfoButton("item");
      GUILayout.Label("Amount", GUILayout.Width(50f));
      var amount = req.Value.Type == JTokenType.Float ? req.Value.Value<float>() : req.Value.Value<int>();
      var newAmount = GUILayout.TextField(amount.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60f));
      DrawPropertyInfoButton("amount");
      if (GUILayout.Button("X", GUILayout.Width(24f)))
      {
        req.Remove();
      }
      GUILayout.EndHorizontal();

      // Apply edits after the row so removing the property mid-iteration is safe
      if (newName != req.Name)
      {
        var newReq = new JProperty(newName, req.Value);
        req.Remove();
        requirements.Add(newReq);
        break; // refresh the list next frame
      }
      if (float.TryParse(newAmount, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedAmount)
          && Math.Abs(parsedAmount - amount) > 0.0001f)
      {
        req.Value = parsedAmount;
      }
    }
    GUILayout.EndScrollView();

    GUILayout.BeginHorizontal();
    _subPickerNewItem = GUILayout.TextField(_subPickerNewItem, GUILayout.Width(200f));
    if (GUILayout.Button("Add Requirement", GUILayout.Width(120f)))
    {
      if (!string.IsNullOrEmpty(_subPickerNewItem) && !requirements.ContainsKey(_subPickerNewItem))
      {
        requirements[_subPickerNewItem] = 1;
        _subPickerNewItem = "";
      }
    }
    if (GUILayout.Button("Pick", GUILayout.Width(50f)))
    {
      OpenIdPicker("", "requirement");
    }
    GUILayout.EndHorizontal();
  }

  private static void DeleteSelectedDataKey()
  {
    var data = _selectedDataFile?.Get();
    if (data == null || string.IsNullOrEmpty(_dataSelectedKey)) return;
    data.Remove(_dataSelectedKey);
    _dataSelectedKey = "";
    PropBuffers.Clear();
    ClearSubPicker();
    ClearSoundPicker();
    ClearIdPicker();
    SetEditorStatus("Deleted selected key");
  }

  private static void AddDataKey()
  {
    if (string.IsNullOrEmpty(_dataNewKey)) return;
    var data = _selectedDataFile.Get();
    if (data == null) return;
    if (data.ContainsKey(_dataNewKey))
    {
      SetEditorStatus("Key already exists");
      return;
    }
    data[_dataNewKey] = _selectedDataFile.NewKeyTemplate != null ? _selectedDataFile.NewKeyTemplate() : new JObject();
    _dataSelectedKey = _dataNewKey;
    _dataNewKey = "";
    ClearSubPicker();
    ClearSoundPicker();
    SyncPropBuffers(data[_dataSelectedKey]);
    SetEditorStatus("Added key");
  }

  private static void SaveDataFile()
  {
    try
    {
      _selectedDataFile.Set(_selectedDataFile.Get());
      Plugin.SaveJsonFile(_selectedDataFile.Path(), _selectedDataFile.Get());
      SetEditorStatus("Saved " + _selectedDataFile.Name);
    }
    catch (Exception e)
    {
      SetEditorStatus("Error saving: " + e.Message);
    }
  }

  private static void SyncWindowBuffers()
  {
    foreach (var window in UiWindows)
    {
      if (!WindowBuffers.TryGetValue(window.Id, out var buffer))
      {
        buffer = new string[4];
        WindowBuffers[window.Id] = buffer;
      }
      var rect = GetWindowRect(window.Id);
      buffer[0] = rect.x.ToString("F0", CultureInfo.InvariantCulture);
      buffer[1] = rect.y.ToString("F0", CultureInfo.InvariantCulture);
      buffer[2] = rect.width.ToString("F0", CultureInfo.InvariantCulture);
      buffer[3] = rect.height.ToString("F0", CultureInfo.InvariantCulture);
    }
  }

  private static void UiManagerWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    GUILayout.Label("Position and size per window. Drag title bars to move, drag the bottom-right corner to resize.", GUILayout.ExpandWidth(true));
    GUILayout.Space(4f);

    foreach (var window in UiWindows)
    {
      if (!WindowBuffers.TryGetValue(window.Id, out var buffer))
      {
        buffer = new string[4];
        WindowBuffers[window.Id] = buffer;
      }
      // ReSharper disable once UnusedVariable
      var rect = GetWindowRect(window.Id);

      GUILayout.BeginHorizontal();
      GUILayout.Label(window.Name, GUILayout.Width(140f));
      buffer[0] = GUILayout.TextField(buffer[0], GUILayout.Width(42f));
      buffer[1] = GUILayout.TextField(buffer[1], GUILayout.Width(42f));
      buffer[2] = GUILayout.TextField(buffer[2], GUILayout.Width(42f));
      buffer[3] = GUILayout.TextField(buffer[3], GUILayout.Width(42f));
      if (GUILayout.Button("Apply", GUILayout.Width(55f)))
      {
        if (float.TryParse(buffer[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && float.TryParse(buffer[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            && float.TryParse(buffer[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            && float.TryParse(buffer[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
        {
          WindowRects[window.Id] = ClampWindowRect(new Rect(x, y, w, h));
        }
        else
        {
          SetStatus("Invalid numbers for " + window.Name);
        }
      }
      if (GUILayout.Button("Reset", GUILayout.Width(55f)))
      {
        WindowRects[window.Id] = DefaultRectFor(window.Id);
        var resetBuffer = new string[4];
        var defaultRect = WindowRects[window.Id];
        resetBuffer[0] = defaultRect.x.ToString("F0", CultureInfo.InvariantCulture);
        resetBuffer[1] = defaultRect.y.ToString("F0", CultureInfo.InvariantCulture);
        resetBuffer[2] = defaultRect.width.ToString("F0", CultureInfo.InvariantCulture);
        resetBuffer[3] = defaultRect.height.ToString("F0", CultureInfo.InvariantCulture);
        WindowBuffers[window.Id] = resetBuffer;
      }
      GUILayout.EndHorizontal();
    }

    GUILayout.Space(4f);
    if (!GUILayout.Button("Reset All", GUILayout.Width(100f))) return;
    WindowRects.Clear();
    WindowBuffers.Clear();
    SyncWindowBuffers();
    SetStatus("All windows reset to default");

  }

  private static CharacterEffects GetPlayerEffects()
  {
    if (!Player.Instance) return null;
    return Player.Instance.effects ? Player.Instance.effects : Player.Instance.GetComponent<CharacterEffects>();
  }

  private static void DrawEffectRow(CharacterEffectType type)
  {
    if (!EffectBuffers.TryGetValue(type, out var inputs))
    {
      inputs = new EffectInputs();
      EffectBuffers[type] = inputs;
    }

    var effects = GetPlayerEffects();
    var isActive = effects && effects.hasEffectType(type);

    try
    {
      GUILayout.BeginHorizontal();
      GUILayout.Label(type.ToString(), GUILayout.Width(140f));
      if (isActive)
      {
        GUI.color = new Color(0.6f, 1f, 0.6f);
        GUILayout.Label("Active", GUILayout.Width(46f));
        GUI.color = Color.white;
      }
      else
      {
        GUILayout.Label("", GUILayout.Width(46f));
      }

      GUILayout.Label("Dur", GUILayout.Width(24f));
      inputs.Duration = GUILayout.TextField(inputs.Duration, GUILayout.Width(50f));
      DrawEffectInfoButton("Duration", "duration");
      GUILayout.Label("Mod", GUILayout.Width(26f));
      inputs.Modifier = GUILayout.TextField(inputs.Modifier, GUILayout.Width(50f));
      DrawEffectInfoButton("Modifier", "modifier");
      GUILayout.Label("Int", GUILayout.Width(24f));
      inputs.Interval = GUILayout.TextField(inputs.Interval, GUILayout.Width(50f));
      DrawEffectInfoButton("Interval", "interval");

      if (GUILayout.Button("Apply", GUILayout.Width(60f)))
      {
        ApplyEffect(type, inputs);
      }
      if (GUILayout.Button("Remove", GUILayout.Width(70f)))
      {
        try
        {
          var currentEffects = GetPlayerEffects();
          if (currentEffects) currentEffects.deleteThisTypeOfEffect(type);
          SetStatus("Removed " + type);
        }
        catch (Exception e)
        {
          Plugin.Log.LogError(e);
          SetStatus("Error removing effect: " + e.Message);
        }
      }

      // Stored in the character effects config under the plain effect type
      var toggleEntry = GetEffectToggleEntry(type, false);
      var disabled = toggleEntry?["disable"]?.Value<bool>() ?? false;
      var reapply = toggleEntry?["reapplyOnLoss"]?.Value<bool>() ?? false;
      var newDisabled = GUILayout.Toggle(disabled, " Disable", GUILayout.Width(86f));
      if (newDisabled != disabled) SetEffectToggle(type, "disable", newDisabled, inputs);
      var newReapply = GUILayout.Toggle(reapply, " Re-apply on loss", GUILayout.Width(146f));
      if (newReapply != reapply) SetEffectToggle(type, "reapplyOnLoss", newReapply, inputs);
      GUILayout.EndHorizontal();
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
    }
  }

  // Effect manager toggles live in the character effects config under the plain effect type, next to the entries the game writes for each effect variant.
  private static JObject GetEffectToggleEntry(CharacterEffectType type, bool create)
  {
    var name = type.ToString();
    if (Plugin.CharacterEffects[name] is JObject existing) return existing;
    if (!create) return null;
    var created = new JObject();
    Plugin.CharacterEffects[name] = created;
    Plugin.SaveCharacterEffects = true;
    return created;
  }

  private static void SetEffectToggle(CharacterEffectType type, string key, bool value, EffectInputs inputs)
  {
    var entry = GetEffectToggleEntry(type, true);
    if (entry == null) return;
    entry[key] = value;
    if (key == "reapplyOnLoss")
    {
      // Remember the values from the row so a re-applied effect matches what the manager shows
      entry["duration"] = float.TryParse(inputs.Duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) ? duration : 0f;
      entry["modifier"] = float.TryParse(inputs.Modifier, NumberStyles.Float, CultureInfo.InvariantCulture, out var modifier) ? modifier : 1f;
      entry["interval"] = float.TryParse(inputs.Interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var interval) ? interval : 0f;
    }
    Plugin.SaveCharacterEffects = true;
    SetStatus((value ? "Enabled " : "Disabled ") + key + " for " + type);
  }

  private static void ApplyEffect(CharacterEffectType type, EffectInputs inputs)
  {    try
    {
      var effects = GetPlayerEffects();
      if (!effects)
      {
        SetStatus("Player effects not available yet");
        return;
      }
      var duration = float.TryParse(inputs.Duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0f;
      var modifier = float.TryParse(inputs.Modifier, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 1f;
      var interval = float.TryParse(inputs.Interval, NumberStyles.Float, CultureInfo.InvariantCulture, out var i) ? i : 0f;
      effects.deleteThisTypeOfEffect(type);
      effects.activate(type, duration, modifier, interval, 0f);
      SetStatus("Applied " + type);
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
      SetStatus("Error applying effect: " + e.Message);
    }
  }

  public static void SaveCursorPosition()
  {
    if (!Player.Instance) return;
    _savedCursorPos = Core.mouseWorldPosition(true);
    _hasSavedCursorPos = true;
    SetStatus("Saved cursor position " + _savedCursorPos.x.ToString("F0", CultureInfo.InvariantCulture) + ", " + _savedCursorPos.z.ToString("F0", CultureInfo.InvariantCulture));
    Plugin.Log.LogInfo("Saved cursor position " + _savedCursorPos);
  }

  public static string GiveItem(string type, int amount)
  {
    try
    {
      if (!Player.Instance) return "Player not available";
      if (string.IsNullOrEmpty(type)) return "No item ID given";
      if (amount <= 0) return "Amount must be at least 1";
      if (!ItemsDatabase.Instance.hasItem(type)) return "Unknown item: " + type;

      // An unstackable item only ever fills one slot, so giving more than one has to hand it over once per item instead
      if (amount > 1 && !IsStackable(type))
      {
        var given = 0;
        for (var i = 0; i < amount; i++)
        {
          if (Player.Instance.Inventory.addItemTypeToPlayer(type, 1, true) == null) break;
          given++;
        }
        Plugin.Log.LogInfo($"Gave unstackable item {type} {given} times via custom UI");
        return given == amount
          ? $"Gave {amount}x {type} (unstackable, {given} slots)"
          : $"Gave {given} of {amount}x {type}, ran out of room";
      }

      Plugin.Log.LogInfo($"Giving item {type} x{amount} via custom UI");
      var givenItem = Player.Instance.Inventory.addItemTypeToPlayer(type, amount, true);
      if (givenItem == null)
      {
        Plugin.Log.LogWarning($"Failed to give item {type} x{amount} via custom UI: no free inventory slot and dropping it on the ground failed as well");
        return $"No room for {amount}x {type} and it could not be dropped";
      }
      Plugin.Log.LogInfo($"Successfully gave item {type} x{amount} via custom UI");
      return $"Gave {amount}x {type}";
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
      return "Error: " + e.Message;
    }
  }

  // Whether an item stacks: the custom item config wins over the mod defaults, which win over the game's own value.
  private static bool IsStackable(string type)
  {
    if (Plugin.ItemsModification.Value)
    {
      if (Plugin.CustomItems[type] is JObject custom && custom["stackable"] != null) return custom["stackable"].Value<bool>();
      if (Plugin.DefaultCustomItems[type] is JObject defaults && defaults["stackable"] != null) return defaults["stackable"].Value<bool>();
    }
    var item = ItemsDatabase.Instance ? ItemsDatabase.Instance.getItem(type, false) : null;
    return item && item.stackable;
  }

  public static string SpawnCharacter(string type, float distance = 300f)
  {
    try
    {
      if (!Player.Instance) return "Player not available";
      if (!Singleton<CharacterSpawner>.Instance) return "Character spawner not available";
      if (string.IsNullOrEmpty(type)) return "No character ID given";
      var prefab = Resources.Load("Prefabs/Characters/" + type) as GameObject;
      if (!prefab) return "Unknown character: " + type;
      if (!prefab.GetComponent<Character>()) return type + " has no Character component, cannot spawn it this way";

      if (_spawnOnSavedPos)
      {
        return !_hasSavedCursorPos ? "No saved position yet, press the Save Cursor Position key (default Shift + F4) or Save now first" : SpawnCharacterAtPosition(type, _savedCursorPos);
      }

      var character = Singleton<CharacterSpawner>.Instance.spawnCharacterAround(Player.Instance.gameObject, Vector3.zero, distance, type, false);
      if (!character) return "Could not find a free spot for " + type;
      Plugin.Log.LogInfo($"Spawning character {type} via custom UI");
      return "Spawned " + type;
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
      return "Error: " + e.Message;
    }
  }

  private static string SpawnCharacterAtPosition(string type, Vector3 position)
  {
    try
    {
      if (!Core.withinWorldBounds(position))
      {
        return "Saved position is outside the world";
      }
      var spawner = Singleton<CharacterSpawner>.Instance;
      var spawned = Core.AddPrefab("Characters/" + type, position, Quaternion.Euler(90f, 0f, 0f), spawner.holder, true);
      var character = spawned.GetComponent<Character>();
      if (!character)
      {
        UnityEngine.Object.Destroy(spawned);
        return "Could not spawn " + type + " at the saved position";
      }
      if (Player.Instance.whereAmI && Player.Instance.whereAmI.bigLocation)
      {
        character.setWaypoints(Player.Instance.whereAmI.bigLocation.waypoints);
      }
      character.temporarySpawned = true;
      character.isActive = true;
      if (!Singleton<Dreams>.Instance.dreaming)
      {
        if (!spawner.spawnedCharacters.Contains(spawned))
        {
          spawner.spawnedCharacters.Add(spawned);
        }
        Core.addToSaveable(spawned, true);
      }
      Plugin.Log.LogInfo($"Spawning character {type} at saved position via custom UI");
      return "Spawned " + type + " at saved position";
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
      return "Error: " + e.Message;
    }
  }

  // Inventory editor (F8): three windows for hotbar, player inventory and the currently opened container.
  // Slots can be selected, edited, cleared, and slots can be added or removed.
  
  private static Inventory GetHotbarInventory()
  {
    return !Player.Instance ? null : Player.Instance.Hotbar;
  }

  private static Inventory GetPlayerInventory()
  {
    return !Player.Instance ? null : Player.Instance.Inventory;
  }

  private static Inventory GetContainerInventory()
  {
    if (!Player.Instance) return null;
    if (Player.Instance.openedItemInventory) return Player.Instance.openedItemInventory;
    if (Player.Instance.openedItemInventory2) return Player.Instance.openedItemInventory2;
    return null;
  }

  private static void HotbarWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    var inv = GetHotbarInventory();
    if (!inv)
    {
      GUILayout.Label("Player not available yet");

      return;
    }

    DrawInventoryEditor(9008, inv);
  }

  private static void PlayerInventoryWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    var inv = GetPlayerInventory();
    if (!inv)
    {
      GUILayout.Label("Player not available yet");

      return;
    }

    DrawInventoryEditor(9009, inv);
  }

  private static void ContainerWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

    var inv = GetContainerInventory();
    if (!inv)
    {
      GUILayout.Label("No container is open. Open a drawer, cabinet, corpse or trader and press F8.", GUILayout.ExpandWidth(true));

      return;
    }

    DrawInventoryEditor(9010, inv);
  }

  private static void DrawInventoryEditor(int windowId, Inventory inv)
  {
    GUILayout.BeginHorizontal();
    GUILayout.Label("Slots: " + inv.slots.Count, GUILayout.ExpandWidth(true));
    if (GUILayout.Button("+ Slot", GUILayout.Width(60f)))
    {
      inv.addSlot();
      inv.refresh();
    }
    if (GUILayout.Button("- Slot", GUILayout.Width(60f)))
    {
      if (inv.slots.Count > 1)
      {
        inv.removeSlot();
        inv.refresh();
      }
    }
    GUILayout.EndHorizontal();

    if (!InvScrolls.TryGetValue(windowId, out var scroll))
    {
      scroll = Vector2.zero;
    }
    scroll = GUILayout.BeginScrollView(scroll);
    InvScrolls[windowId] = scroll;

    for (var i = 0; i < inv.slots.Count; i++)
    {
      DrawInvSlotRow(windowId, inv, i);
    }

    GUILayout.EndScrollView();

    // Editor for the selected slot
    if (_selectedInvWindow == windowId && _selectedInvSlot >= 0 && _selectedInvSlot < inv.slots.Count)
    {
      DrawInvSlotEditor(windowId, inv);
    }
  }

  private static void DrawInvSlotRow(int windowId, Inventory inv, int index)
  {
    var slot = inv.slots[index];
    var hasItem = !InvItemClass.isNull(slot.invItem);
    var selected = _selectedInvWindow == windowId && _selectedInvSlot == index;

    GUILayout.BeginHorizontal();
    if (selected)
    {
      GUI.color = new Color(0.7f, 0.9f, 1f);
    }
    var label = hasItem ? slot.invItem.type + " x" + slot.invItem.amount : "(empty)";
    if (GUILayout.Button(label, GUILayout.ExpandWidth(true)))
    {
      _selectedInvWindow = windowId;
      _selectedInvSlot = index;
      _invSlotItemName = hasItem ? slot.invItem.type : "";
      _invSlotAmount = hasItem ? slot.invItem.amount.ToString(CultureInfo.InvariantCulture) : "1";
    }
    GUI.color = Color.white;

    if (hasItem && GUILayout.Button("Clear", GUILayout.Width(55f)))
    {
      slot.removeItem();
      if (_selectedInvWindow == windowId && _selectedInvSlot == index)
      {
        _selectedInvSlot = -1;
      }
      inv.refresh();
    }
    GUILayout.EndHorizontal();
  }

  // ReSharper disable once UnusedParameter.Local
  private static void DrawInvSlotEditor(int windowId, Inventory inv)
  {
    if (_selectedInvSlot >= inv.slots.Count)
    {
      _selectedInvSlot = -1;
      return;
    }
    var slot = inv.slots[_selectedInvSlot];

    GUILayout.BeginVertical("box");
    GUILayout.Label("Edit slot " + (_selectedInvSlot + 1), GUILayout.ExpandWidth(true));

    GUILayout.BeginHorizontal();
    GUILayout.Label("Item ID", GUILayout.Width(60f));
    _invSlotItemName = GUILayout.TextField(_invSlotItemName, GUILayout.Width(180f));
    GUILayout.EndHorizontal();

    GUILayout.BeginHorizontal();
    GUILayout.Label("Amount", GUILayout.Width(60f));
    _invSlotAmount = GUILayout.TextField(_invSlotAmount, GUILayout.Width(80f));
    GUILayout.EndHorizontal();

    GUILayout.BeginHorizontal();
    if (GUILayout.Button("Set", GUILayout.Width(60f)))
    {
      if (!ItemsDatabase.Instance.hasItem(_invSlotItemName))
      {
        SetStatus("Unknown item: " + _invSlotItemName);
      }
      else if (!int.TryParse(_invSlotAmount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 1)
      {
        SetStatus("Amount must be a positive number");
      }
      else
      {
        if (!InvItemClass.isNull(slot.invItem))
        {
          slot.removeItem();
        }
        slot.createItem(_invSlotItemName, amount);
        inv.refresh();
        SetStatus("Set slot to " + _invSlotItemName + " x" + amount);
      }
    }
    if (GUILayout.Button("Clear", GUILayout.Width(60f)))
    {
      slot.removeItem();
      inv.refresh();
      SetStatus("Cleared slot");
    }
    if (GUILayout.Button("Close", GUILayout.Width(60f)))
    {
      _selectedInvSlot = -1;
    }
    GUILayout.EndHorizontal();

    GUILayout.EndVertical();
  }

  // Item quickpick (F8): a wide bottom bar listing all items.
  // Clicking one inserts its ID into the currently focused slot editor text field.

  private static string _quickpickSearch = "";
  private static Vector2 _quickpickScroll;
  // The item picked in the quickpick is kept here so it survives slot switches, and the bar at
  // the bottom can set the selected slot to it without retyping anything.
  private static string _quickpickItem = "";
  private static string _quickpickAmount = "1";

  private static void QuickPickWindow(int id)
  {
    DrawWindowHeader();
    DrawQuickPickStatus();
    DrawStatus();

    if (!ItemsDatabase.Instance)
    {
      GUILayout.Label("Items database not available yet");
      return;
    }

    if (_allItems.Length == 0)
    {
      _allItems = [.. ItemsDatabase.Instance.itemsDict.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }

    // Bottom bar mirroring the slot editor: the picked item and an amount, kept between picks
    GUILayout.BeginHorizontal("box");
    GUILayout.Label("Item", GUILayout.Width(40f));
    _quickpickItem = GUILayout.TextField(_quickpickItem, GUILayout.Width(200f));
    GUILayout.Label("Amount", GUILayout.Width(55f));
    _quickpickAmount = GUILayout.TextField(_quickpickAmount, GUILayout.Width(60f));
    if (GUILayout.Button("Set to quickpicked item", GUILayout.Width(180f)))
    {
      SetQuickPickedItemToSlot();
    }
    GUILayout.FlexibleSpace();
    GUILayout.Label("Click an item to pick it", GUILayout.ExpandWidth(true));
    GUILayout.EndHorizontal();

    GUILayout.BeginHorizontal();
    GUILayout.Label("Search", GUILayout.Width(55f));
    _quickpickSearch = GUILayout.TextField(_quickpickSearch, GUILayout.Width(200f));
    GUILayout.FlexibleSpace();
    GUILayout.EndHorizontal();

    var filtered = string.IsNullOrEmpty(_quickpickSearch)
      ? _allItems
      : [.. _allItems.Where(x => x.IndexOf(_quickpickSearch, StringComparison.OrdinalIgnoreCase) >= 0)];

    // Fixed cells instead of auto sized buttons: every item name has a different width, so the old layout fitted a different number of them on every row and the list looked ragged.
    // The column count is derived from the window width and every cell gets the same size, so rows always hold exactly that many items and the columns line up.
    const float minCellWidth = 200f;
    const float cellSpacing = 4f;
    const float rowHeight = 22f;
    var available = Mathf.Max(GetWindowRect(9011).width - 52f, minCellWidth);
    var columns = Mathf.Max(1, Mathf.FloorToInt((available + cellSpacing) / (minCellWidth + cellSpacing)));
    var cellWidth = (available + cellSpacing) / columns - cellSpacing;

    _quickpickScroll = GUILayout.BeginScrollView(_quickpickScroll);
    for (var i = 0; i < filtered.Length; i += columns)
    {
      GUILayout.BeginHorizontal();
      for (var j = 0; j < columns; j++)
      {
        var index = i + j;
        if (index >= filtered.Length)
        {
          // Leave the empty cells of the last row blank so its buttons keep the columns of the rows above
          GUILayout.Space(cellWidth);
          continue;
        }
        var item = filtered[index];
        var isPicked = item == _quickpickItem;
        if (isPicked) GUI.color = new Color(0.7f, 0.9f, 1f);
        if (!GUILayout.Button(item, GUILayout.Width(cellWidth), GUILayout.Height(rowHeight)))
        {
          GUI.color = Color.white;
          continue;
        }
        GUI.color = Color.white;
        _quickpickItem = item;
        // Also fill the slot editor field for the old Set flow
        _invSlotItemName = item;
        SetQuickPickStatus("Picked " + item);
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();
  }

  private static void SetQuickPickedItemToSlot()
  {
    if (string.IsNullOrEmpty(_quickpickItem))
    {
      SetQuickPickStatus("Pick an item first");
      return;
    }
    if (!ItemsDatabase.Instance.hasItem(_quickpickItem))
    {
      SetQuickPickStatus("Unknown item: " + _quickpickItem);
      return;
    }
    if (!int.TryParse(_quickpickAmount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 1)
    {
      SetQuickPickStatus("Amount must be a positive number");
      return;
    }
    var inventory = GetSelectedInvSlotInventory(out var slot);
    if (!inventory || slot == null)
    {
      SetQuickPickStatus("Select a slot in one of the inventory windows first");
      return;
    }
    if (!InvItemClass.isNull(slot.invItem)) slot.removeItem();
    slot.createItem(_quickpickItem, amount);
    inventory.refresh();
    SetQuickPickStatus("Set slot to " + _quickpickItem + " x" + amount);
  }

  // Resolves the slot that is selected in the inventory editor windows
  private static Inventory GetSelectedInvSlotInventory(out InvSlot slot)
  {
    slot = null;
    if (_selectedInvWindow < 0 || _selectedInvSlot < 0) return null;
    var inventory = _selectedInvWindow switch
    {
      9008 => Player.Instance ? Player.Instance.Hotbar : null,
      9009 => Player.Instance ? Player.Instance.Inventory : null,
      9010 => GetContainerInventory(),
      _ => null
    };
    if (!inventory || _selectedInvSlot >= inventory.slots.Count) return null;
    slot = inventory.slots[_selectedInvSlot];
    return inventory;
  }
}
