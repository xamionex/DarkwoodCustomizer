using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DarkwoodCustomizer;

[BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
[BepInProcess("Darkwood.exe")]
internal class Plugin : BaseUnityPlugin
{
  public static float LastItemsSaveTime;
  public static bool SaveItems;
  public static float SavedItemsCooldown;
  public static float LastCharactersSaveTime;
  public static bool SaveCharacters;
  public static float SavedCharactersCooldown;
  public static float LastRandomInventoriesSaveTime;
  public static bool SaveRandomInventories;
  public static float SavedRandomInventoriesCooldown;
  public static float LastCharacterEffectsSaveTime;
  public static bool SaveCharacterEffects;
  public static float SavedCharacterEffectsCooldown;
  public static float LastLootSaveTime;
  public static bool SaveLoot;
  public static float SavedLootCooldown;
  public static ManualLogSource Log;
  private static FileSystemWatcher _fileWatcher;
  private static FileSystemWatcher _fileWatcherJson;
  private static FileSystemWatcher _fileWatcherDefaults;

  // Base Plugin Values
  private static readonly string JsonConfigPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Customs");
  private static readonly string DefaultsConfigPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "ModDefaults");
  private static ConfigEntry<string> _modVersion;
  public static ConfigEntry<bool> LogDebug;
  public static ConfigEntry<bool> LogWorkbench;

  // Items Values
  public static ConfigEntry<bool> UseGlobalStackSize;
  public static ConfigEntry<int> StackResize;
  public static ConfigEntry<bool> UseGlobalMaxDurability;
  public static ConfigEntry<int> MaxDurability;
  public static ConfigEntry<bool> ItemsModification;
  public static ConfigEntry<bool> BearTrapRecovery;
  public static ConfigEntry<bool> BearTrapRecoverySwitch;
  public static ConfigEntry<bool> ChainTrapRecovery;
  public static ConfigEntry<bool> ChainTrapRecoverySwitch;
  public static ConfigEntry<bool> CustomItemsUseDefaults;

  public static string CustomItemsPath => Path.Combine(JsonConfigPath, "CustomItems.json");
  private static readonly string DefaultsCustomItemsPath = Path.Combine(DefaultsConfigPath, "CustomItems.json");
  public static JObject CustomItems;
  public static readonly JObject DefaultCustomItems = JObject.FromObject(new
  {
    exampleItem = new
    {
      note = "This data entry shows all available data of an item you can change",
      note2 = "I recommend reading the wiki on github for more variables you can change",
      hasAmmo = false,
      canBeReloaded = false,
      ammoReloadType = "magazine",
      ammoType = "semi/burst/fullauto/auto/single",
      hasDurability = false,
      maxDurability = 10.0f,
      ignoreDurabilityInValue = true,
      repairable = true,
      requirements = new
      {
        someitemid = 1,
      },
      flamethrowerdrag = 0.4f,
      flamethrowercontactDamage = 20,
      damage = 10,
      clipSize = 100,
      value = 100,
      maxAmount = 100,
      stackable = true,
      ExpValue = 100,
      IsExpItem = false,
      rottenItemNote = "This is for mushrooms, as they have different types for their rotten counterpart",
      rottenItem = "mushroom_rotten",
      rottenItemMaxAmount = 100,
      rottenItemStackable = true,
      rottenItemValue = 100,
      rottenItemExpValue = 100,
      rottenItemIsExpItem = true,
    },
    weapon_flamethrower_homeMade = new
    {
      ReadMePlease = "Welcome to the default CustomItems.json I recommend editing these kind of files with something that has linting, which will tell you if you've made a mistake somewhere, I personally use VSCode.. because that's what I use to code this plugin but you can also use Notepad++",
      note = "This is an example for you, you don't need to add this many data types but you can, if you want",
      note2 = "Copy this whole file to Customs/CustomItems.json and change the values, they will take priority over the defaults, note that if defaults are enabled, these will still take effect and yours will just override them",
      note3 = "Continuing off of last note, if you want to change just the name, make a copy of this in Customs/CustomItems.json and remove everything except the name and description, and then change them, you'll see that the item still works despite your json having basically nothing in it, that is if defaults are enabled",
      note4 = "If you want to fully disable defaults, do that in Items.cfg",
      note5 = "anyway, flamethrower is a bit finnicky and I've made it use ammo instead of durability but it still likes to be funky and will always say 'This item is broken'",
      name = "Flamethrower",
      description = "Makeshift flamethrower made with some kind of gun scraps and two tanks strapped to it, I better be careful with this.",
      descriptionandnameNote = "The above values override the default game name and description of the item this is assigned to, so you can literally change it to whatever you want",
      descriptionandnameNote2 = "You can try this by adding these values to the lantern item and seeing that it will change its name and description",
      iconType = "weapon_flamethrower_military_01",
      iconTypeNote = "You can look at ItemLog.log and get icons from items and put them here, if something breaks while making the item, know that it might be this, if nothing works just set this to the items' name",
      fireMode = "fullAuto",
      fireModeNote = "Fire modes: burst, single, semi, auto/fullauto. Single is for singleuse items",
      hasAmmo = true,
      canBeReloaded = true,
      ammoReloadType = "magazine",
      ammoReloadTypeNote = "This dictates which type of reload this item does, single or magazine, magazine restores the items max ammo, single restores only 1 ammo in the item",
      ammoType = "gasoline",
      hasDurability = false,
      maxDurability = 100f,
      ignoreDurabilityInValue = true,
      ignoreDurabilityInValueNote = "Traders reduce the value of the item if it has a lower durability, this will disable that if it's set to true",
      repairable = true,
      repairableNote = "Whether or not the item can be repaired, in this case, the flamethrower",
      requirements = new
      {
        gasoline = 1,
      },
      requirementsNote = "This says which items should be used for repairing this one, and how many of those items, and yes, it can have multiple repair requirements, just add a comma and another item below",
      damage = 5,
      flamethrowerdrag = 0.4f,
      flamethrowercontactDamage = 20,
      flamethrowerNote = "The above two values are only for flamethrowers, dont expect them to work for other items",
      clipSize = 100,
      clipSizeNote = "The above value sets how much max ammo this item can have",
      value = 100,
      drainDurabilityOnShot = false,
      drainAmmoOnShot = true,
      drainNotes = "The two above values dictate whether or not my mod will drain the ammo/durability of this item when its fired, only enable this when the item doesnt do that by default",
      InfiniteAmmo = false,
      InfiniteDurability = false,
    },
    weapon_rifle_02_boltAction = new
    {
      iconType = "weapon_rifle_01_boltAction",
      InfiniteAmmo = false,
    },
    lantern = new
    {
      hasDurability = true,
      maxDurability = 1200f,
      repairable = true,
      requirements = new
      {
        gasoline = 0.2,
      },
    },
    nail = new
    {
      isStackable = true,
      maxAmount = 50,
    },
    wood = new
    {
      isStackable = true,
      maxAmount = 50,
    },
  });

  // Inventory Resize Values
  public static ConfigEntry<bool> RemoveExcess;

  // Workbench values
  public static ConfigEntry<bool> WorkbenchInventoryModification;
  public static ConfigEntry<float> StorageXOffset;
  public static ConfigEntry<float> StorageZOffset;
  public static ConfigEntry<int> RightSlots;
  public static ConfigEntry<int> DownSlots;

  // Inventory values
  public static ConfigEntry<int> InventoryRightSlots;
  public static ConfigEntry<int> InventoryDownSlots;
  public static ConfigEntry<bool> InventorySlots;

  // Hotbar values
  public static ConfigEntry<int> HotbarRightSlots;
  public static ConfigEntry<int> HotbarDownSlots;
  public static ConfigEntry<bool> HotbarSlots;
  
  // Trader values
  public static ConfigEntry<bool> TraderSlots;
  public static ConfigEntry<int> TraderRightSlots;
  public static ConfigEntry<int> TraderDownSlots;
  public static ConfigEntry<float> TraderInventoryXOffset;
  public static ConfigEntry<float> TraderInventoryZOffset;
  public static ConfigEntry<float> TraderSellXOffset;
  public static ConfigEntry<float> TraderSellZOffset;
  public static ConfigEntry<float> TraderBuyXOffset;
  public static ConfigEntry<float> TraderBuyZOffset;
  public static ConfigEntry<float> TraderCloseXOffset;
  public static ConfigEntry<float> TraderCloseZOffset;

  // Crafting values
  public static ConfigEntry<bool> CraftingModification;
  public static ConfigEntry<float> CraftingXOffset;
  public static ConfigEntry<float> CraftingZOffset;
  public static ConfigEntry<int> CraftingRightSlots;
  public static ConfigEntry<int> CraftingDownSlots;
  public static ConfigEntry<bool> CraftingRecipesModification;
  public static ConfigEntry<bool> CustomCraftingRecipesUseDefaults;
  public static ConfigEntry<bool> CraftingUnusedContinue;
  public static string CustomCraftingRecipesPath => Path.Combine(JsonConfigPath, "CustomCraftingRecipes.json");
  public static string DefaultsCustomCraftingRecipesPath = Path.Combine(DefaultsConfigPath, "CustomCraftingRecipes.json");
  public static JObject CustomCraftingRecipes;
  public static JObject DefaultCustomCraftingRecipes = JObject.FromObject(new
  {
    spring = new
    {
      requiredlevel = 2,
      resource = "spring",
      givesamount = 1,
      requirements = new
      {
        junk = 1,
        wire = 2,
      },
    },
    ammo_clip_mediumCal = new
    {
      requiredlevel = 3,
      resource = "ammo_clip_mediumCal",
      givesamount = 1,
      requirements = new
      {
        ammo_single_mediumCal = 10,
        spring = 1,
        junk = 2,
      },
    },
    ammo_clip_smallCal = new
    {
      requiredlevel = 3,
      resource = "ammo_clip_smallCal",
      givesamount = 1,
      requirements = new
      {
        ammo_single_mediumCal = 2,
        spring = 1,
        junk = 5,
      },
    },
    ammo_single_mediumCal = new
    {
      requiredlevel = 4,
      resource = "ammo_single_mediumCal",
      givesamount = 2,
      requirements = new
      {
        pipe_metal = 1,
        junk = 2,
      },
    },
    knife = new
    {
      requiredlevel = 2,
      resource = "knife",
      givesamount = 1,
      requirements = new
      {
        junk = 1,
        wood = 1,
        stone = 1,
        nail = 1,
      },
    },
    weapon_flamethrower_homeMade = new
    {
      requiredlevel = 4,
      resource = "weapon_flamethrower_homeMade",
      givesamount = 1,
      requirements = new
      {
        firecraft_weaponParts = 2,
        gasoline = 2,
      },
    },
    weapon_submachine_01_full = new
    {
      requiredlevel = 4,
      resource = "weapon_submachine_01_full",
      givesamount = 1,
      requirements = new
      {
        pipe_metal = 1,
        firecraft_weaponParts = 2,
        ammo_clip_smallCal = 1,
      },
    },
    weapon_pistol_01_pellet = new
    {
      requiredlevel = 3,
      resource = "weapon_pistol_01_pellet",
      givesamount = 1,
      requirements = new
      {
        pipe_metal = 1,
        firecraft_weaponParts = 1,
        ammo_single_pellet = 1,
      },
    },
    weapon_assault_01_burst = new
    {
      requiredlevel = 4,
      resource = "weapon_assault_01_burst",
      givesamount = 1,
      requirements = new
      {
        pipe_metal = 2,
        firecraft_weaponParts = 2,
        ammo_clip_mediumCal = 1,
      },
    },
    ammo_single_pellet = new
    {
      requiredlevel = 4,
      resource = "ammo_single_pellet",
      givesamount = 8,
      requirements = new
      {
        ammo_clip_smallCal = 1,
      },
    },
  });

  // Character Values
  public static ConfigEntry<bool> CharacterModification;
  public static string CustomCharactersPath => Path.Combine(JsonConfigPath, "CustomCharacters.json");
  public static JObject CustomCharacters;

  // Character Effects
  public static ConfigEntry<bool> CharacterEffectsModification;
  public static string CharacterEffectsPath => Path.Combine(JsonConfigPath, "CustomCharacterEffects.json");
  public static JObject CharacterEffects;

  // Player Values
  public static ConfigEntry<bool> PlayerModification;
  public static ConfigEntry<bool> PlayerFOVModification;
  public static ConfigEntry<float> PlayerFOV;
  public static ConfigEntry<bool> PlayerCantGetInterrupted;

  // Player Stamina Values
  public static ConfigEntry<bool> PlayerStaminaModification;
  public static ConfigEntry<float> PlayerMaxStamina;
  public static ConfigEntry<float> PlayerStaminaRegenInterval;
  public static ConfigEntry<float> PlayerStaminaRegenValue;
  public static ConfigEntry<bool> PlayerInfiniteStamina;
  public static ConfigEntry<bool> PlayerInfiniteStaminaEffect;

  // Player Health Values
  public static ConfigEntry<bool> PlayerHealthModification;
  public static ConfigEntry<float> PlayerMaxHealth;
  public static ConfigEntry<float> PlayerHealthRegenInterval;
  public static ConfigEntry<float> PlayerHealthRegenModifier;
  public static ConfigEntry<float> PlayerHealthRegenValue;
  public static ConfigEntry<bool> PlayerGodmode;

  // Player Speed Values
  public static ConfigEntry<bool> PlayerSpeedModification;
  public static ConfigEntry<float> PlayerWalkSpeed;
  public static ConfigEntry<float> PlayerRunSpeed;
  public static ConfigEntry<float> PlayerRunSpeedModifier;

  // Time values
  public static ConfigEntry<bool> TimeModification;
  public static ConfigEntry<float> DaytimeFlow;
  public static ConfigEntry<float> NighttimeFlow;
  public static ConfigEntry<bool> UseCurrentTime;
  public static ConfigEntry<int> CurrentTime;
  public static ConfigEntry<bool> TimeStop;
  public static ConfigEntry<bool> ResetWell;

  // Generator values
  public static ConfigEntry<bool> GeneratorModification;
  public static ConfigEntry<float> GeneratorModifier;
  public static ConfigEntry<bool> GeneratorInfiniteFuel;

  // Camera values
  public static ConfigEntry<bool> CameraModification;
  public static ConfigEntry<float> CameraFoV;
  public static ConfigEntry<bool> CameraDisablePostFX;
  public static ConfigEntry<bool> CameraDisableVignette;
  
  // Cheats
  public static ConfigEntry<bool> Cheats;
  public static ConfigEntry<string> CheatsGiveItemName;
  public static ConfigEntry<int> CheatsGiveItemAmount;
  public static bool CheatsGiveItem;
  public static ConfigEntry<KeyboardShortcut> CheatsGiveItemKeybind;

  // UI values
  public static ConfigEntry<bool> UIModification;
  public static ConfigEntry<bool> UIDisabled;
  public static ConfigEntry<bool> UIDisabledHealthBar;
  public static ConfigEntry<bool> UIDisabledLives;
  public static ConfigEntry<bool> UIDisabledStaminaBar;
  public static ConfigEntry<bool> UIDisabledSkillbar;

  // MISC
  public static ConfigEntry<bool> DisableWormSpawn;

  // Keybinds values
  public static ConfigEntry<KeyboardShortcut> KeybindGodmode;
  public static ConfigEntry<KeyboardShortcut> KeybindStamina;
  public static ConfigEntry<KeyboardShortcut> KeybindTime;
  public static ConfigEntry<KeyboardShortcut> KeybindHud;

  // Random Inventories Values
  public static ConfigEntry<bool> RandomInventoriesModification;
  public static string CustomRandomInventoriesPath => Path.Combine(JsonConfigPath, "CustomRandomInventories.json");
  public static JObject CustomRandomInventories;

  // Loot Values
  public static ConfigEntry<bool> LootModification;
  public static string CustomLootPath => Path.Combine(JsonConfigPath, "CustomLoot.json");
  public static JObject CustomLoot;

  private void BepinexBindings()
  {
    var i = 255;
    // Base Plugin
    Config.Bind("!Mod", "Thank you", "<3", new ConfigDescription("Thank you for downloading my mod, every config is explained in it's description above it.\nIf a config doesn't have comments above it, it's probably an old config that was in a previous version.\nAdditionally the wiki can be found on the github for help using the custom x (json) features.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    _modVersion = Config.Bind("!Mod", "Version", PluginInfo.PluginVersion, new ConfigDescription("The mods' version, read only value for you", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    LogDebug = Config.Bind("!Mod", "Enable Debug Logs", true, new ConfigDescription("Whether to log debug messages, includes player information on load/change for now.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("!Mod", "Enable Json Reload Messages", false, new ConfigDescription("Whether to log debug messages for when a json file is reloaded", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("!Mod", "Enable Debug Logs for Items", false, new ConfigDescription("Whether to log every item, only called when the game is loading the specific item\nItems loaded by the game are saved to ItemLog.log and any items the mod changes are also logged to the bepinex log", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("!Mod", "Enable Debug Logs for Characters", false, new ConfigDescription("Whether to log every character, called when the game is load the specific character\nRS=Run Speed, WS=Walk Speed\nRead the extended documentation in the Characters config", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    LogWorkbench = Config.Bind("!Mod", "Enable Debug Logs for Workbench", false, new ConfigDescription("Whether to log every time a custom recipe is added to the workbench", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    _modVersion.Value = PluginInfo.PluginVersion;
    Config.Save();

    // Items
    ItemsModification = Config.Bind("Items", "Enable Section", true, new ConfigDescription("Enable this section of the mod, This section does require save reloads for everything except trap recoveries", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    BearTrapRecovery = Config.Bind("Items", "BearTrap Recovery", true, new ConfigDescription("Enables the option below", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    BearTrapRecoverySwitch = Config.Bind("Items", "BearTrap Recover Items", true, new ConfigDescription("false = beartrap disarm gives a beartrap\ntrue = beartrap disarm gives 3 scrap metal", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    ChainTrapRecovery = Config.Bind("Items", "ChainTrap Recovery", true, new ConfigDescription("Enables the option below", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    ChainTrapRecoverySwitch = Config.Bind("Items", "ChainTrap Recover Items", true, new ConfigDescription("false = chaintrap disarm gives a chaintrap\ntrue = chaintrap disarm gives 2 scrap metal", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UseGlobalStackSize = Config.Bind("Items", "Enable Global Stack Size", false, new ConfigDescription("Whether to use a global stack size for all items.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    StackResize = Config.Bind("Items", "Global Stack Resize", 50, new ConfigDescription("Number for all item stack sizes to be set to.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UseGlobalMaxDurability = Config.Bind("Items", "Enable Global Max Durability", false, new ConfigDescription("Whether to use a global max durability for all items.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    MaxDurability = Config.Bind("Items", "Global Max Durability", 100, new ConfigDescription("Number for all item max durability to be set to.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CustomItems = (JObject)GetJsonConfig(CustomItemsPath, new JObject());
    CustomItemsUseDefaults = Config.Bind("Items", "Load Mod Defaults First", true, new ConfigDescription("Whether or not to load mod defaults first and then customs you have\nDon't worry about duplicates, they will be overwritten", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Inventories
    WorkbenchInventoryModification = Config.Bind("Inventories", "Enable Workbench Modification", false, new ConfigDescription("Enable Workbench Modification.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    RemoveExcess = Config.Bind("Inventories", "Remove Excess Slots", true, new ConfigDescription("Whether or not to remove slots that are outside the inventory you set. For example, you set your inventory to 9x9 (81 slots) but you had a previous mod do something bigger and you have something like 128 slots extra enabling this option will remove those excess slots and bring it down to 9x9 (81)", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Workbench
    StorageXOffset = Config.Bind("Inventories", "Storage X Offset", 150f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the workbench storage", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    StorageZOffset = Config.Bind("Inventories", "Storage Z Offset", -100f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the workbench storage", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    RightSlots = Config.Bind("Inventories", "Workbench Right Slots", 12, new ConfigDescription("Number that determines slots in workbench to the right", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    DownSlots = Config.Bind("Inventories", "Workbench Down Slots", 9, new ConfigDescription("Number that determines slots in workbench downward", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Inventory
    InventorySlots = Config.Bind("Inventories", "Enable Inventory Modification", false, new ConfigDescription("Enable Inventory Modification", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    InventoryRightSlots = Config.Bind("Inventories", "Inventory Right Slots", 2, new ConfigDescription("Number that determines slots in inventory to the right", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    InventoryDownSlots = Config.Bind("Inventories", "Inventory Down Slots", 9, new ConfigDescription("Number that determines slots in inventory downward", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Hotbar
    HotbarSlots = Config.Bind("Inventories", "Enable Hotbar Modification", false, new ConfigDescription("Enable Hotbar Modification\nRequires reload of save to take effect (Return to Menu > Load Save)", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    HotbarRightSlots = Config.Bind("Inventories", "Hotbar Right Slots", 1, new ConfigDescription("Number that determines slots in Hotbar to the right.\nRequires reload of save to take effect (Return to Menu > Load Save)", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    HotbarDownSlots = Config.Bind("Inventories", "Hotbar Down Slots", 6, new ConfigDescription("Number that determines slots in Hotbar downward.\nRequires reload of save to take effect (Return to Menu > Load Save)", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Trader
    TraderSlots = Config.Bind("Inventories", "Enable Trader Modification", false, new ConfigDescription("Enable Traders Modification", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderRightSlots = Config.Bind("Inventories", "Trader Right Slots", 6, new ConfigDescription("Number that determines slots in Traders to the right.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderDownSlots = Config.Bind("Inventories", "Trader Down Slots", 7, new ConfigDescription("Number that determines slots in Traders downward.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderInventoryXOffset = Config.Bind("Inventories", "TraderInventory Window X Offset", 0f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the trader inventory window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderInventoryZOffset = Config.Bind("Inventories", "TraderInventory Window Z Offset", 0f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the trader inventory window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderSellXOffset = Config.Bind("Inventories", "TraderSell Window X Offset", 0f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the sell window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderSellZOffset = Config.Bind("Inventories", "TraderSell Window Z Offset", 0f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the sell window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderBuyXOffset = Config.Bind("Inventories", "TraderBuy Window X Offset", 0f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the buy window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderBuyZOffset = Config.Bind("Inventories", "TraderBuy Window Z Offset", 0f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the buy window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderCloseXOffset = Config.Bind("Inventories", "TraderClose Button X Offset", 0f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the close button", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TraderCloseZOffset = Config.Bind("Inventories", "TraderClose Button Z Offset", 0f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the close button", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    
    // Crafting
    CraftingModification = Config.Bind("Inventories", "Enable Crafting Modification", false, new ConfigDescription("Enable Crafting Modification", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CraftingXOffset = Config.Bind("Inventories", "Crafting Window X Offset", 1000f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the workbench crafting window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CraftingZOffset = Config.Bind("Inventories", "Crafting Window Z Offset", -100f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the workbench crafting window", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CraftingRightSlots = Config.Bind("Inventories", "Crafting Window Right Slots", 7, new ConfigDescription("Number that determines slots in Crafting window to the right.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CraftingDownSlots = Config.Bind("Inventories", "Crafting Window Down Slots", 7, new ConfigDescription("Number that determines slots in Crafting window downward.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, new JObject());
    CraftingRecipesModification = Config.Bind("Crafting", "Enable Crafting Recipes Modification", true, new ConfigDescription("Enable Crafting Modification", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CustomCraftingRecipesUseDefaults = Config.Bind("Crafting", "Load Mod Defaults First", true, new ConfigDescription("Whether or not to load mod defaults first and then customs you have\nDon't worry about duplicates, they will be overwritten", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CraftingUnusedContinue = Config.Bind("Crafting", "Try to load unused items", false, new ConfigDescription("Try to load unused items anyway\nWARNING: This will most likely result in your workbench breaking, use at your own risk", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("Crafting", "Note1", "okay", new ConfigDescription("This section is responsible for custom recipes. If you're in chapter 2 keep in mind that some recipes might load on 2nd opening, it's because of how the game loads items", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("Crafting", "Note2", "okay", new ConfigDescription("givesamount only works for some items, not sure what dictates this.", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Character
    CharacterModification = Config.Bind("Characters", "Enable Section", false, new ConfigDescription("Enable this section of the mod, you can edit the characters in Customs/CustomCharacters.json", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("Characters", "Note", "ReadMePlease", new ConfigDescription("Launch a save once to generate the config\nDo not add more attacks than what was added by default, it'll cancel the damage modification since it'll cause an indexing error\nBarricadeDamage is restricted from being loaded when the value is 0\nin the case of a character having barricadedamage but my plugin cant read it, it will assign 0\nso if anything goes wrong with barricadedamage this makes sure something like a dog will still be able to deal damage to barricades", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, new JObject());
    
    // Character Effects
    CharacterEffectsModification = Config.Bind("Effects", "Enable Effects Modification", true, new ConfigDescription("Enable Effects Modification", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("Effects", "Note", "ReadMePlease", new ConfigDescription("Effects will be saved to your custom config as you get them", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CharacterEffects = (JObject)GetJsonConfig(CharacterEffectsPath, new JObject());

    // Player
    PlayerModification = Config.Bind("Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerFOVModification = Config.Bind("Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerStaminaModification = Config.Bind("Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerHealthModification = Config.Bind("Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerSpeedModification = Config.Bind("Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerFOV = Config.Bind("Player", "Player FoV", 90f, new ConfigDescription("Set your players' FoV (370 recommended, set to 720 if you want to always see everything)", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerCantGetInterrupted = Config.Bind("Player", "Cant Get Interrupted", true, new ConfigDescription("If set to true you can't get stunned, your cursor will reset color but remember that you're still charged, it just doesn't show it", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Player Stamina
    PlayerMaxStamina = Config.Bind("Player", "Max Stamina", 100f, new ConfigDescription("Set your max stamina", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerStaminaRegenInterval = Config.Bind("Player", "Stamina Regen Interval", 0.05f, new ConfigDescription("Interval in seconds between stamina regeneration ticks. I believe this is the rate at which your stamina will regenerate when you are not using stamina abilities. Lowering this value will make your stamina regenerate faster, raising it will make your stamina regenerate slower.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerStaminaRegenValue = Config.Bind("Player", "Stamina Regen Value", 30f, new ConfigDescription("Amount of stamina regenerated per tick. I believe this is the amount of stamina you will gain each time your stamina regenerates. Raising this value will make your stamina regenerate more per tick, lowering it will make your stamina regenerate less per tick.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerInfiniteStamina = Config.Bind("Player", "Infinite Stamina", false, new ConfigDescription("On every update makes your stamina maximized", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerInfiniteStaminaEffect = Config.Bind("Player", "Infinite Stamina Effect", false, new ConfigDescription("Whether to draw the infinite stamina effect", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Player Health
    PlayerMaxHealth = Config.Bind("Player", "Max Health", 100f, new ConfigDescription("Set your max health", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerHealthRegenInterval = Config.Bind("Player", "Health Regen Interval", 5f, new ConfigDescription("Theoretically: Interval in seconds between health regeneration ticks, feel free to experiment I didn't test this out yet", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerHealthRegenModifier = Config.Bind("Player", "Health Regen Modifier", 1f, new ConfigDescription("Theoretically: Multiplier for health regen value, feel free to experiment I didn't test this out yet", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerHealthRegenValue = Config.Bind("Player", "Health Regen Value", 0f, new ConfigDescription("Theoretically: Amount of health regenerated per tick, feel free to experiment I didn't test this out yet", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerGodmode = Config.Bind("Player", "Enable Godmode", false, new ConfigDescription("Makes you invulnerable and on every update makes your health maximized", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Player Speed
    PlayerWalkSpeed = Config.Bind("Player", "Walk Speed", 7.5f, new ConfigDescription("Set your walk speed", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerRunSpeed = Config.Bind("Player", "Run Speed", 15f, new ConfigDescription("Set your run speed", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    PlayerRunSpeedModifier = Config.Bind("Player", "Run Speed Modifier", 1f, new ConfigDescription("Multiplies your run speed by this value", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Time
    TimeModification = Config.Bind("Time", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    DaytimeFlow = Config.Bind("Time", "Daytime Flow", 1f, new ConfigDescription("Set the day time interval in seconds, aka how many seconds to wait before updating the time.\nLower values make time pass faster, higher values make time pass slower.\nBe cautious: very high values make this option update very slowly, higher values make time update slower.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    NighttimeFlow = Config.Bind("Time", "Nighttime Flow", 0.75f, new ConfigDescription("Set the night time interval in seconds, aka how many seconds to wait before updating the time.\nLower values make time pass faster, higher values make time pass slower.\nBe cautious: very high values make this option update very slowly, higher values make time update slower, which includes calculating when the night starts.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UseCurrentTime = Config.Bind("Time", "Set Time", false, new ConfigDescription("Enable this to use the config for time of day below", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CurrentTime = Config.Bind("Time", "Set Current Time", 1, new ConfigDescription("(1) is day (8:01), (900) is (18:00), (1440) is end of night (set it to 1439 and then disable set time)", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    TimeStop = Config.Bind("Time", "Stop Time", false, new ConfigDescription("Doesn't let time get any higher", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    ResetWell = Config.Bind("Time", "Reset Well", false, new ConfigDescription("Whether or not to reset well constantly", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Generator
    GeneratorModification = Config.Bind("Generator", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    GeneratorModifier = Config.Bind("Generator", "Generator Modifier", 1f, new ConfigDescription("2x is twice as fast drainage, 1x is as fast as normal, 0.5x is half as fast", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    GeneratorInfiniteFuel = Config.Bind("Generator", "Generator Infinite Fuel", false, new ConfigDescription("Enable this to make the generator have infinite fuel", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Camera
    CameraModification = Config.Bind("Camera", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CameraFoV = Config.Bind("Camera", "Camera Zoom Factor", 1f, new ConfigDescription("Changes the zoom factor of the camera, lower values is zoomed out, higher values is zoomed in", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CameraDisablePostFX = Config.Bind("Camera", "Disable Post FX", false, new ConfigDescription("Disables Postprocess FX for the camera", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CameraDisableVignette = Config.Bind("Camera", "Disable Vignette", false, new ConfigDescription("Disables the vignette around your vision", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    
    // Cheats
    Cheats = Config.Bind("Cheats", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CheatsGiveItemName = Config.Bind("Cheats", "Give Item with Name", "", new ConfigDescription("Gives this item ID when enabling the below toggle", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CheatsGiveItemAmount = Config.Bind("Cheats", "Give Item Amount", 1, new ConfigDescription("Gives the above item with this specified amount", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CheatsGiveItemKeybind = Config.Bind("Cheats", "Give Item Button", new KeyboardShortcut(KeyCode.G, KeyCode.LeftControl));

    // UI
    UIModification = Config.Bind("UI", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UIDisabled = Config.Bind("UI", "Disable UI/HUD", false, new ConfigDescription("Disables all HUD/UI", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UIDisabledHealthBar = Config.Bind("UI", "Disable Healthbar", false, new ConfigDescription("Disables the healthbar for immersion", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UIDisabledLives = Config.Bind("UI", "Disable Lives", false, new ConfigDescription("Disables the lives for immersion", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UIDisabledStaminaBar = Config.Bind("UI", "Disable Staminabar", false, new ConfigDescription("Disables the stamina bar for immersion", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    UIDisabledSkillbar = Config.Bind("UI", "Disable Skillbar (current effects)", false, new ConfigDescription("Disables the skill bar (current effects) for immersion", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    
    // Misc
    DisableWormSpawn = Config.Bind("MISC", "Disable Night Floor Gore (Requires Save Reload)", false, new ConfigDescription("Disables the floor gore that spawns and kills you at night if not in a shelter. Requires Save Reload", null, new ConfigurationManagerAttributes { Order = i-=1 }));

    // Keybinds
    KeybindGodmode = Config.Bind("Hotkeys", "Toggle Godmode", new KeyboardShortcut(KeyCode.G, KeyCode.LeftShift));
    KeybindStamina = Config.Bind("Hotkeys", "Toggle Infinite Stamina", new KeyboardShortcut(KeyCode.H, KeyCode.LeftShift));
    KeybindTime = Config.Bind("Hotkeys", "Toggle Time Stop", new KeyboardShortcut(KeyCode.T, KeyCode.LeftShift));
    KeybindHud = Config.Bind("Hotkeys", "Toggle HUD/UI", new KeyboardShortcut(KeyCode.P));

    // CustomRandomInventories
    RandomInventoriesModification = Config.Bind("RandomInventories", "Enable Section", false, new ConfigDescription("Enable this section of the mod, you can edit the RandomInventories in Customs/CustomRandomInventories.json", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("RandomInventories", "Note", "ReadMePlease", new ConfigDescription("Launch a save once to generate the config\nThis section also dictates the items traders have\nNew objects will be added when they load, ex. wolfman wont be added until the game loads their randominventory\nAdditional Note: You won't see changes until the next appearance of that type of inventory, I recommend starting a new save with 0.01 time scaling to generate NightTrader and WolfMan trades", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CustomRandomInventories = (JObject)GetJsonConfig(CustomRandomInventoriesPath, new JObject());

    // CustomLoot
    LootModification = Config.Bind("Loot", "Enable Section", false, new ConfigDescription("Enable this section of the mod, you can edit the Loot in Customs/CustomLoot.json", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    Config.Bind("Loot", "Note", "ReadMePlease", new ConfigDescription("Launch a save once to generate the config\nThis section dictates which items will be added to the loot tables of inventories, set replace to true to remove the base game items.", null, new ConfigurationManagerAttributes { Order = i-=1 }));
    CustomLoot = (JObject)GetJsonConfig(CustomLootPath, new JObject());
  }

  private void MakeDefaults()
  {
    if (!Directory.Exists(DefaultsConfigPath))
      Directory.CreateDirectory(DefaultsConfigPath);

    var defaultsReadMePath = Path.Combine(DefaultsConfigPath, "ReadMe.txt");
    var readMeString = "This folder is a readonly folder for you to use as a template when creating your own custom things.\nYou are not meant to edit this, just to read\nThe custom json files you can edit are in the DarkwoodCustomizer/Customs folder, not in the ModDefaults folder\nI recommend copying these files and replacing the default values with your own custom ones";
    if (File.Exists(defaultsReadMePath) && !File.ReadAllText(defaultsReadMePath).Equals(readMeString))
      File.Delete(defaultsReadMePath);
    if (!File.Exists(defaultsReadMePath))
      File.WriteAllText(defaultsReadMePath, readMeString);

    if (File.Exists(DefaultsCustomCraftingRecipesPath) && !File.ReadAllText(DefaultsCustomCraftingRecipesPath).Equals(JsonConvert.SerializeObject(DefaultCustomCraftingRecipes, Formatting.Indented)))
      File.Delete(DefaultsCustomCraftingRecipesPath);
    if (!File.Exists(DefaultsCustomCraftingRecipesPath))
      File.WriteAllText(DefaultsCustomCraftingRecipesPath, JsonConvert.SerializeObject(DefaultCustomCraftingRecipes, Formatting.Indented));
    
    if (File.Exists(DefaultsCustomItemsPath) && !File.ReadAllText(DefaultsCustomItemsPath).Equals(JsonConvert.SerializeObject(DefaultCustomItems, Formatting.Indented)))
      File.Delete(DefaultsCustomItemsPath);
    if (!File.Exists(DefaultsCustomItemsPath))
      File.WriteAllText(DefaultsCustomItemsPath, JsonConvert.SerializeObject(DefaultCustomItems, Formatting.Indented));
  }

  private void Awake()
  {
    Log = Logger;

    var logItemsPath = Path.Combine(Paths.ConfigPath, "ItemLog.log");
    if (File.Exists(logItemsPath))
      File.Delete(logItemsPath);

    // 1.2.6 migration check
    Migrate126Configs();
    // 1.2.8 migration check
    Migrate128Configs();

    BepinexBindings();
    MakeDefaults();

    LogDivider();

    var harmony = new Harmony($"{PluginInfo.PluginGuid}");
    harmony.PatchAll(typeof(CamMainPatch));
    Log.LogInfo("Patching in CamMainPatch! (Camera)");
    harmony.PatchAll(typeof(UIPatch));
    Log.LogInfo("Patching in UIPatch! (UI)");
    harmony.PatchAll(typeof(CharacterSpawnerPatch));
    Log.LogInfo("Patching in CharacterSpawnerPatch! (Floor Gore)");
    harmony.PatchAll(typeof(CharacterPatch));
    Log.LogInfo("Patching in CharacterPatch! (Soon™️)");
    harmony.PatchAll(typeof(CharacterEffectsPatch));
    Log.LogInfo("Patching in CharacterEffectsPatch! (Eagle Eye, etc.)");
    harmony.PatchAll(typeof(ControllerPatch));
    Log.LogInfo("Patching in ControllerPatch! (Time)");
    harmony.PatchAll(typeof(GeneratorPatch));
    Log.LogInfo("Patching in GeneratorPatch! (Generator Fuel)");
    harmony.PatchAll(typeof(InventoryPatch));
    Log.LogInfo("Patching in InventoryPatch! (Storage)");
    harmony.PatchAll(typeof(InventoryRandomizePatch));
    Log.LogInfo("Patching in InventoryRandomizePatch! (Traders and Loot)");
    harmony.PatchAll(typeof(InvItemClassPatch));
    Log.LogInfo("Patching in InvItemClassPatch! (Items)");
    harmony.PatchAll(typeof(ItemPatch));
    Log.LogInfo("Patching in ItemPatch! (beartrap disarm)");
    harmony.PatchAll(typeof(LanguagePatch));
    Log.LogInfo("Patching in LanguagePatch! (Item Names)");
    harmony.PatchAll(typeof(PlayerPatch));
    Log.LogInfo("Patching in PlayerPatch! (Player Update)");
    harmony.PatchAll(typeof(UpgradeItemMenuPatch));
    Log.LogInfo("Patching in UpgradeItemMenuPatch! (Upgrade Menu)");
    harmony.PatchAll(typeof(WorkbenchPatch));
    Log.LogInfo("Patching in WorkbenchPatch! (Recipes)");
    harmony.PatchAll(typeof(DialogueWindowPatch));
    Log.LogInfo("Patching in DialogueWindowPatch! (Trader windows)");
    harmony.PatchAll(typeof(LevelingMenuPatch));
    Log.LogInfo("Patching in LevelingMenuPatch! (Inventory in cooking menu)");
    harmony.PatchAll(typeof(WorldGeneratorPatch));
    Log.LogInfo("Patching in WorldGeneratorPatch! (Bool when game loads)");

    Log.LogInfo($"[{PluginInfo.PluginGuid} v{PluginInfo.PluginVersion}] has fully loaded!");
    LogDivider();

    _fileWatcher = new FileSystemWatcher(Paths.ConfigPath, "*.cfg");
    _fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
    _fileWatcher.Changed += OnFileChanged;
    _fileWatcher.Deleted += OnFileDeleted;
    _fileWatcher.EnableRaisingEvents = true;

    _fileWatcherDefaults = new FileSystemWatcher(DefaultsConfigPath, "*.json");
    _fileWatcherDefaults.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
    _fileWatcherDefaults.Changed += OnFileChangedDefaults;
    _fileWatcherDefaults.Deleted += OnFileDeleted;
    _fileWatcherDefaults.EnableRaisingEvents = true;

    _fileWatcherJson = new FileSystemWatcher(JsonConfigPath, "*.json");
    _fileWatcherJson.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
    _fileWatcherJson.Changed += OnFileChanged;
    _fileWatcherJson.Deleted += OnFileDeleted;
    _fileWatcherJson.EnableRaisingEvents = true;

    // 1.3.3 migration check
    Migrate133Configs();
  }

  public void Update()
  {
    if (KeybindGodmode.Value.IsDown())
    {
      PlayerGodmode.Value = !PlayerGodmode.Value;
      Log.LogInfo("Player Godmode toggled!");
    }
    if (KeybindStamina.Value.IsDown())
    {
      PlayerInfiniteStamina.Value = !PlayerInfiniteStamina.Value;
      Log.LogInfo("Player Infinite Stamina toggled!");
    }
    if (KeybindTime.Value.IsDown())
    {
      TimeStop.Value = !TimeStop.Value;
      Log.LogInfo("Time Stop toggled!");
    }
    if (KeybindHud.Value.IsDown())
    {
      UIDisabled.Value = !UIDisabled.Value;
      Log.LogInfo("Hud toggled!");
    }
    if (CheatsGiveItemKeybind.Value.IsDown())
    {
      CheatsGiveItem = true;
    }
  }

  public void FixedUpdate()
  {
    if (SavedItemsCooldown < 0f) SavedItemsCooldown = 0f;
    else SavedItemsCooldown -= 0.1f;
    if (SavedCharactersCooldown < 0f) SavedCharactersCooldown = 0f;
    else SavedCharactersCooldown -= 0.1f;
    if (SavedRandomInventoriesCooldown < 0f) SavedRandomInventoriesCooldown = 0f;
    else SavedRandomInventoriesCooldown -= 0.1f;
    if (SavedCharacterEffectsCooldown < 0f) SavedCharacterEffectsCooldown = 0f;
    else SavedCharacterEffectsCooldown -= 0.1f;
    if (SavedLootCooldown < 0f) SavedLootCooldown = 0f;
    else SavedLootCooldown -= 0.1f;
    if (Time.time - LastItemsSaveTime > 0.8f && SaveItems)
    {
      SaveJsonFile(CustomItemsPath, CustomItems);
      LastItemsSaveTime = Time.time;
      SavedItemsCooldown += 1f;
      SaveItems = false;
    }
    if (Time.time - LastRandomInventoriesSaveTime > 0.8f && SaveRandomInventories)
    {
      SaveJsonFile(CustomRandomInventoriesPath, CustomRandomInventories);
      LastRandomInventoriesSaveTime = Time.time;
      SavedRandomInventoriesCooldown += 1f;
      SaveRandomInventories = false;
    }
    if (Time.time - LastCharactersSaveTime > 0.8f && SaveCharacters)
    {
      SaveJsonFile(CustomCharactersPath, CustomCharacters);
      LastCharactersSaveTime = Time.time;
      SavedCharactersCooldown += 1f;
      SaveCharacters = false;
    }
    if (Time.time - LastCharacterEffectsSaveTime > 0.8f && SaveCharacterEffects)
    {
      SaveJsonFile(CharacterEffectsPath, CharacterEffects);
      LastCharacterEffectsSaveTime = Time.time;
      SavedCharacterEffectsCooldown += 1f;
      SaveCharacterEffects = false;
    }
    if (Time.time - LastLootSaveTime > 0.8f && SaveLoot)
    {
      SaveJsonFile(CustomLootPath, CustomLoot);
      LastLootSaveTime = Time.time;
      SavedLootCooldown += 1f;
      SaveLoot = false;
    }
  }

  private void OnFileChanged(object sender, FileSystemEventArgs e)
  {
    //Log.LogInfo($"Trying to reload {e.Name}.");
    switch (e.Name)
    {
      case $"{PluginInfo.PluginGuid}.cfg":
        Config.Reload();
        GeneratorPatch.RefreshGenerator = true;
        PlayerPatch.RefreshPlayer = true;
        break;
      case "CustomCharacters.json":
        if (SavedCharactersCooldown <= 0f)
        {
          CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, new JObject());
        }
        break;
      case "CustomCharacterEffects.json":
        if (SavedCharactersCooldown <= 0f)
        {
          CharacterEffects = (JObject)GetJsonConfig(CharacterEffectsPath, new JObject());
        }
        break;
      case "CustomCraftingRecipes.json":
        CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, new JObject());
        break;
      case "CustomItems.json":
        if (SavedItemsCooldown <= 0f)
        {
          CustomItems = (JObject)GetJsonConfig(CustomItemsPath, new JObject());
        }
        break;
      case "CustomRandomInventories.json":
        if (SavedRandomInventoriesCooldown <= 0f)
        {
          CustomRandomInventories = (JObject)GetJsonConfig(CustomRandomInventoriesPath, new JObject());
        }
        break;
      case "CustomLoot.json":
        if (SavedLootCooldown <= 0f)
        {
          CustomLoot = (JObject)GetJsonConfig(CustomLootPath, new JObject());
        }
        break;
    }
  }

  private void OnFileDeleted(object sender, FileSystemEventArgs e)
  {
    if (e.FullPath.EndsWith(".json"))
      MakeDefaults();
    else if (e.FullPath.EndsWith(".cfg"))
      BepinexBindings();
  }

  private void OnFileChangedDefaults(object sender, FileSystemEventArgs e)
  {
    MakeDefaults();
  }

  public static void LogDivider()
  {
    Log.LogInfo("");
    Log.LogInfo("--------------------------------------------------------------------------------");
    Log.LogInfo("");
  }

  public static object GetJsonConfig(string filePath, JObject defaultJson)
  {
    return CreateNewJsonFile(filePath, defaultJson);
  }

  public static JObject CreateNewJsonFile(string filePath, JObject defaultJson)
  {
    if (File.Exists(filePath))
    {
      try
      {
        var fileContents = File.ReadAllText(filePath);
        var jsonObject = JObject.Parse(fileContents);

        return jsonObject;
      }
      catch (Exception ex)
      {
        var i = 0;
        var newFilePath = filePath;
        while (File.Exists(newFilePath))
        {
          newFilePath = Path.Combine(Path.GetDirectoryName(filePath), $"{Path.GetFileNameWithoutExtension(filePath)}_error_{i++}{Path.GetExtension(filePath)}");
        }
        File.Move(filePath, newFilePath);
        Log.LogInfo($"Error loading JSON file: {ex.Message}, using default config, your config has been renamed to {Path.GetFileName(newFilePath)}");
        return defaultJson;
      }
    }
    else
    {
      var directory = Path.GetDirectoryName(filePath);
      if (!Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }
      File.WriteAllText(filePath, JsonConvert.SerializeObject(defaultJson, Formatting.Indented));
      Log.LogInfo($"Created {filePath} with default config because it didnt exist.");
    }
    return defaultJson;
  }

  public static void SaveJsonFile(string jsonPath, JObject jsonData)
  {
    var directory = Path.GetDirectoryName(jsonPath);
    if (!Directory.Exists(directory))
    {
      Directory.CreateDirectory(directory);
    }
    File.WriteAllText(jsonPath, JsonConvert.SerializeObject(jsonData, Formatting.Indented));
  }

  private void Migrate126Configs()
  {
    // 1.2.6 migration check
    // Move JSON files
    var oldJsonPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid);
    var newJsonPath = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Customs");
    if (!Directory.Exists(newJsonPath)) Directory.CreateDirectory(newJsonPath);
    foreach (var jsonFile in Directory.GetFiles(oldJsonPath, "*.json"))
    {
      var fileName = Path.GetFileName(jsonFile);
      var newJsonFile = Path.Combine(newJsonPath, fileName == "CustomCharacters.json" ? fileName + "_ThisIsYourOldConfigFrom1.2.6.json" : fileName);
      if (File.Exists(newJsonFile)) File.Delete(newJsonFile);
      File.Move(jsonFile, newJsonFile);
      Log.LogInfo($"Moved and renamed old JSON file from {jsonFile} to {newJsonFile}");
    }

    // Remove old defaults folders
    string[] oldDefaultsFolders = [Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "defaults"), Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "VanillaDefaults")];
    foreach (var oldDefaultsFolder in oldDefaultsFolders)
    {
      if (Directory.Exists(oldDefaultsFolder))
      {
        Directory.Delete(oldDefaultsFolder, true);
        Log.LogInfo($"Deleted old defaults folder at \"{oldDefaultsFolder}\"");
      }
    }
  }


  private void Migrate128Configs()
  {
    // 1.2.8 migration check
    var oldLanternConfig = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Lantern.cfg");
    if (File.Exists(oldLanternConfig))
    {
      File.Delete(oldLanternConfig);
      Log.LogInfo($"Deleted old Lantern config file at {oldLanternConfig}");
    }
    var oldDefaultStacksJson = Path.Combine(DefaultsConfigPath, "CustomStacks.json");
    if (File.Exists(oldDefaultStacksJson))
    {
      File.Delete(oldDefaultStacksJson);
      Log.LogInfo($"Old Default CustomStacks Json was deleted at {oldDefaultStacksJson}");
    }
    string newPathConfig;
    var oldStacksConfig = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "CustomStacks.cfg");
    if (File.Exists(oldStacksConfig))
    {
      newPathConfig = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "CustomStacks_Unused_From_128_DeletePlease.cfg");
      File.Move(oldStacksConfig, newPathConfig);
      Log.LogInfo($"Old CustomStacks config can be found at at {newPathConfig}");
    }
    var oldStacksJson = Path.Combine(JsonConfigPath, "CustomStacks.json");
    if (File.Exists(oldStacksJson))
    {
      newPathConfig = Path.Combine(JsonConfigPath, "CustomStacks_Unused_From_128_DeletePlease.json");
      File.Move(oldStacksJson, newPathConfig);
      Log.LogInfo($"Old CustomStacks Json file can be found at {newPathConfig}");
    }
  }

  private void Migrate133Configs()
  {
    List<string> configFiles =
    [
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Logging.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Items.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Inventories.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Characters.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Player.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Time.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Generator.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Camera.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Workbench.cfg"),
    ];
    List<string> configFilesPreviousVersions =
    [
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Logging.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Items.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Inventories.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Characters.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Player.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Time.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Generator.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Camera.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Workbench.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Repairs.cfg"),
      Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "Stacks.cfg"),
    ];
    var newConfig = "";
    var changed = false;
    if (configFiles.Count == 9)
    {
      foreach (var configFile in configFiles)
      {
        if (File.Exists(configFile))
        {
          var category = Path.GetFileNameWithoutExtension(configFile);
          var lines = File.ReadAllLines(configFile);
          if (category == "Logging") category = "!Mod";
          if (category == "Workbench") category = "Crafting";
          newConfig += $"[{category}]\n";
          foreach (var line in lines)
          {
            if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
            {
              var newLine = line.Trim();
              if (newLine.Contains("="))
              {
                var key = newLine.Split('=')[0].Trim();
                var value = newLine.Split('=')[1].Trim();
                newConfig += $"\n{key} = {value}\n";
                changed = true;
              }
            }
          }
          Log.LogInfo($"Merged old config file at {configFile} into {PluginInfo.PluginGuid}.cfg");
        }
      }
    }
    foreach (var configPreviousVersion in configFilesPreviousVersions)
    {
      if (File.Exists(configPreviousVersion))
      {
        var oldConfigsFolder = Path.Combine(Paths.ConfigPath, PluginInfo.PluginGuid, "YourOldConfigs");
        if (!Directory.Exists(oldConfigsFolder)) Directory.CreateDirectory(oldConfigsFolder);
        var newConfigFile = Path.Combine(oldConfigsFolder, $"{Path.GetFileNameWithoutExtension(configPreviousVersion)}.cfg");
        File.Move(configPreviousVersion, newConfigFile);
        Log.LogInfo($"Old config file at {configPreviousVersion} was moved to {newConfigFile} as its not used anymore");
      }
    }
    if (changed) File.WriteAllText(Path.Combine(Paths.ConfigPath, $"{PluginInfo.PluginGuid}.cfg"), newConfig);
  }
}
