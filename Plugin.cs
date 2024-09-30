using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInProcess("Darkwood.exe")]
public class Plugin : BaseUnityPlugin
{
    public const string PluginAuthor = "amione";
    public const string PluginName = "DarkwoodCustomizer";
    public const string PluginVersion = "1.3.4";
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;
    public static FileSystemWatcher fileWatcherJson;
    public static FileSystemWatcher fileWatcherDefaults;

    // Base Plugin Values
    public static string JsonConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID, "Customs");
    public static string DefaultsConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID, "ModDefaults");
    public static ConfigEntry<string> ModVersion;
    public static ConfigEntry<bool> LogDebug;
    public static ConfigEntry<bool> LogItems;
    public static ConfigEntry<bool> LogCharacters;
    public static ConfigEntry<bool> LogWorkbench;

    // Items Values
    public static ConfigEntry<bool> UseGlobalStackSize;
    public static ConfigEntry<int> StackResize;
    public static ConfigEntry<bool> ItemsModification;
    public static ConfigEntry<bool> BearTrapRecovery;
    public static ConfigEntry<bool> BearTrapRecoverySwitch;
    public static ConfigEntry<bool> ChainTrapRecovery;
    public static ConfigEntry<bool> ChainTrapRecoverySwitch;
    public static ConfigEntry<bool> CustomItemsUseDefaults;

    public static string CustomItemsPath => Path.Combine(JsonConfigPath, "CustomItems.json");
    public static string DefaultsCustomItemsPath = Path.Combine(DefaultsConfigPath, "CustomItems.json");
    public static JObject CustomItems;
    public static JObject DefaultCustomItems = JObject.FromObject(new
    {
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
    public static ConfigEntry<float> CraftingXOffset;
    public static ConfigEntry<float> CraftingZOffset;
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

    // Crafting values
    public static ConfigEntry<bool> CraftingModification;
    public static ConfigEntry<int> CraftingRightSlots;
    public static ConfigEntry<int> CraftingDownSlots;
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
    });

    // Character Values
    public static ConfigEntry<bool> CharacterModification;
    public static string CustomCharactersPath => Path.Combine(JsonConfigPath, "CustomCharacters.json");
    public static JObject CustomCharacters;

    // Player Values
    public static ConfigEntry<bool> PlayerModification;
    public static ConfigEntry<bool> PlayerFOVModification;
    public static ConfigEntry<float> PlayerFOV;
    public static ConfigEntry<bool> PlayerCantGetInterrupted;

    // Player Stamina Values
    public static ConfigEntry<bool> PlayerStaminaModification;
    public static ConfigEntry<int> PlayerStaminaUpgrades;
    public static ConfigEntry<float> PlayerMaxStamina;
    public static ConfigEntry<float> PlayerStaminaRegenInterval;
    public static ConfigEntry<float> PlayerStaminaRegenValue;
    public static ConfigEntry<bool> PlayerInfiniteStamina;
    public static ConfigEntry<bool> PlayerInfiniteStaminaEffect;

    // Player Health Values
    public static ConfigEntry<bool> PlayerHealthModification;
    public static ConfigEntry<int> PlayerHealthUpgrades;
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

    // Keybinds values
    public static ConfigEntry<KeyboardShortcut> KeybindGodmode;
    public static ConfigEntry<KeyboardShortcut> KeybindStamina;
    public static ConfigEntry<KeyboardShortcut> KeybindTime;

    private void BepinexBindings()
    {
        // Base Plugin
        Config.Bind($"!Mod", "Thank you", "no prob!", new ConfigDescription("Thank you for downloading my mod, every config is explained in it's description above it, if a config doesn't have comments above it, it's probably an old config that was in a previous version.", null, new ConfigurationManagerAttributes { Order = 5 }));
        ModVersion = Config.Bind($"!Mod", "Version", PluginVersion, new ConfigDescription("The mods' version, read only value for you", null, new ConfigurationManagerAttributes { Order = 4 }));
        LogDebug = Config.Bind($"!Mod", "Enable Debug Logs", true, new ConfigDescription("Whether to log debug messages, includes player information on load/change for now.", null, new ConfigurationManagerAttributes { Order = 3 }));
        LogItems = Config.Bind($"!Mod", "Enable Debug Logs for Items", false, new ConfigDescription("Whether to log every item, only called when the game is loading the specific item\nWhen enabled logs will be saved in ItemLog.log", null, new ConfigurationManagerAttributes { Order = 2 }));
        LogCharacters = Config.Bind($"!Mod", "Enable Debug Logs for Characters", false, new ConfigDescription("Whether to log every character, called when the game is load the specific character\nRS=Run Speed, WS=Walk Speed\nRead the extended documentation in the Characters config", null, new ConfigurationManagerAttributes { Order = 1 }));
        LogWorkbench = Config.Bind($"!Mod", "Enable Debug Logs for Workbench", false, new ConfigDescription("Whether to log every time a custom recipe is added to the workbench", null, new ConfigurationManagerAttributes { Order = 0 }));
        ModVersion.Value = PluginVersion;
        Config.Save();

        // Items
        ItemsModification = Config.Bind($"Items", "Enable Section", true, new ConfigDescription("Enable this section of the mod, This section does require save reloads for everything except trap recoveries", null, new ConfigurationManagerAttributes { Order = 8 }));
        BearTrapRecovery = Config.Bind($"Items", "BearTrap Recovery", true, new ConfigDescription("Enables the option below", null, new ConfigurationManagerAttributes { Order = 7 }));
        BearTrapRecoverySwitch = Config.Bind($"Items", "BearTrap Recover Items", true, new ConfigDescription("false = beartrap disarm gives a beartrap\ntrue = beartrap disarm gives 3 scrap metal", null, new ConfigurationManagerAttributes { Order = 6 }));
        ChainTrapRecovery = Config.Bind($"Items", "ChainTrap Recovery", true, new ConfigDescription("Enables the option below", null, new ConfigurationManagerAttributes { Order = 5 }));
        ChainTrapRecoverySwitch = Config.Bind($"Items", "ChainTrap Recover Items", true, new ConfigDescription("false = chaintrap disarm gives a chaintrap\ntrue = chaintrap disarm gives 2 scrap metal", null, new ConfigurationManagerAttributes { Order = 4 }));
        UseGlobalStackSize = Config.Bind($"Items", "Enable Global Stack Size", false, new ConfigDescription("Whether to use a global stack size for all items.", null, new ConfigurationManagerAttributes { Order = 3 }));
        StackResize = Config.Bind($"Items", "Global Stack Resize", 50, new ConfigDescription("Number for all item stack sizes to be set to.", null, new ConfigurationManagerAttributes { Order = 2 }));
        CustomItems = (JObject)GetJsonConfig(CustomItemsPath, new JObject { });
        CustomItemsUseDefaults = Config.Bind($"Items", "Load Mod Defaults First", true, new ConfigDescription("Whether or not to load mod defaults first and then customs you have\nDon't worry about duplicates, they will be overwritten", null, new ConfigurationManagerAttributes { Order = 0 }));

        // Inventories
        WorkbenchInventoryModification = Config.Bind($"Inventories", "Enable Workbench Modification", false, new ConfigDescription("Enable Workbench Modification.", null, new ConfigurationManagerAttributes { Order = 14 }));
        RemoveExcess = Config.Bind($"Inventories", "Remove Excess Slots", true, new ConfigDescription("Whether or not to remove slots that are outside the inventory you set. For example, you set your inventory to 9x9 (81 slots) but you had a previous mod do something bigger and you have something like 128 slots extra enabling this option will remove those excess slots and bring it down to 9x9 (81)", null, new ConfigurationManagerAttributes { Order = 13 }));

        // Workbench
        RightSlots = Config.Bind($"Inventories", "Workbench Right Slots", 12, new ConfigDescription("Number that determines slots in workbench to the right", null, new ConfigurationManagerAttributes { Order = 12 }));
        DownSlots = Config.Bind($"Inventories", "Workbench Down Slots", 9, new ConfigDescription("Number that determines slots in workbench downward", null, new ConfigurationManagerAttributes { Order = 11 }));

        // Inventory
        InventorySlots = Config.Bind($"Inventories", "Enable Inventory Modification", false, new ConfigDescription("Enable Inventory Modification", null, new ConfigurationManagerAttributes { Order = 10 }));
        InventoryRightSlots = Config.Bind($"Inventories", "Inventory Right Slots", 2, new ConfigDescription("Number that determines slots in inventory to the right", null, new ConfigurationManagerAttributes { Order = 9 }));
        InventoryDownSlots = Config.Bind($"Inventories", "Inventory Down Slots", 9, new ConfigDescription("Number that determines slots in inventory downward", null, new ConfigurationManagerAttributes { Order = 8 }));

        // Hotbar
        HotbarSlots = Config.Bind($"Inventories", "Enable Hotbar Modification", false, new ConfigDescription("Enable Hotbar Modification\nRequires reload of save to take effect (Return to Menu > Load Save)", null, new ConfigurationManagerAttributes { Order = 7 }));
        HotbarRightSlots = Config.Bind($"Inventories", "Hotbar Right Slots", 1, new ConfigDescription("Number that determines slots in Hotbar to the right.\nRequires reload of save to take effect (Return to Menu > Load Save)", null, new ConfigurationManagerAttributes { Order = 6 }));
        HotbarDownSlots = Config.Bind($"Inventories", "Hotbar Down Slots", 6, new ConfigDescription("Number that determines slots in Hotbar downward.\nRequires reload of save to take effect (Return to Menu > Load Save)", null, new ConfigurationManagerAttributes { Order = 5 }));

        // Crafting
        CraftingModification = Config.Bind($"Inventories", "Enable Crafting Modification", false, new ConfigDescription("Enable Crafting Modification", null, new ConfigurationManagerAttributes { Order = 4 }));
        CraftingXOffset = Config.Bind($"Inventories", "Crafting Window X Offset", 1000f, new ConfigDescription("Pixels offset on the X axis (left negative/right positive) for the workbench crafting window", null, new ConfigurationManagerAttributes { Order = 3 }));
        CraftingZOffset = Config.Bind($"Inventories", "Crafting Window Z Offset", -100f, new ConfigDescription("Pixels offset on the Z axis (up positive/down negative) for the workbench crafting window", null, new ConfigurationManagerAttributes { Order = 2 }));
        CraftingRightSlots = Config.Bind($"Inventories", "Crafting Window Right Slots", 7, new ConfigDescription("Number that determines slots in Crafting window to the right.", null, new ConfigurationManagerAttributes { Order = 1 }));
        CraftingDownSlots = Config.Bind($"Inventories", "Crafting Window Down Slots", 7, new ConfigDescription("Number that determines slots in Crafting window downward.", null, new ConfigurationManagerAttributes { Order = 0 }));
        CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, new JObject { });
        CustomCraftingRecipesUseDefaults = Config.Bind($"Crafting", "Load Mod Defaults First", true, new ConfigDescription("Whether or not to load mod defaults first and then customs you have\nDon't worry about duplicates, they will be overwritten", null, new ConfigurationManagerAttributes { Order = 1 }));
        CraftingUnusedContinue = Config.Bind($"Crafting", "Try to load unused items", false, new ConfigDescription("Try to load unused items anyway\nWARNING: This will most likely result in your workbench breaking, use at your own risk", null, new ConfigurationManagerAttributes { Order = 0 }));
        Config.Bind($"Crafting", "Note1", "okay", new ConfigDescription("This section is responsible for custom recipes.", null, new ConfigurationManagerAttributes { Order = 2 }));
        Config.Bind($"Crafting", "Note2", "okay", new ConfigDescription("givesamount only works for some items, not sure what dictates this.", null, new ConfigurationManagerAttributes { Order = 2 }));

        // Character
        CharacterModification = Config.Bind($"Characters", "Enable Section", false, new ConfigDescription("Enable this section of the mod, you can edit the characters in Customs/CustomCharacters.json", null, new ConfigurationManagerAttributes { Order = 1 }));
        Config.Bind($"Characters", "Note", "ReadMePlease", new ConfigDescription("Launch a save once to generate the config\nDo not add more attacks than what was added by default, it'll cancel the damage modification since it'll cause an indexing error\nBarricadeDamage is restricted from being loaded when the value is 0\nin the case of a character having barricadedamage but my plugin cant read it, it will assign 0\nso if anything goes wrong with barricadedamage this makes sure something like a dog will still be able to deal damage to barricades", null, new ConfigurationManagerAttributes { Order = 0 }));
        CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, new JObject { });

        // Player
        PlayerModification = Config.Bind($"Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 18 }));
        PlayerFOVModification = Config.Bind($"Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 17 }));
        PlayerStaminaModification = Config.Bind($"Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 17 }));
        PlayerHealthModification = Config.Bind($"Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 16 }));
        PlayerSpeedModification = Config.Bind($"Player", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 15 }));
        PlayerFOV = Config.Bind($"Player", "Player FoV", 90f, new ConfigDescription("Set your players' FoV (370 recommended, set to 720 if you want to always see everything)", null, new ConfigurationManagerAttributes { Order = 14 }));
        PlayerCantGetInterrupted = Config.Bind($"Player", "Cant Get Interrupted", true, new ConfigDescription("If set to true you can't get stunned, your cursor will reset color but remember that you're still charged, it just doesn't show it", null, new ConfigurationManagerAttributes { Order = 13 }));

        // Player Stamina
        PlayerMaxStamina = Config.Bind($"Player", "Max Stamina", 100f, new ConfigDescription("Set your max stamina", null, new ConfigurationManagerAttributes { Order = 12 }));
        PlayerStaminaRegenInterval = Config.Bind($"Player", "Stamina Regen Interval", 0.05f, new ConfigDescription("Interval in seconds between stamina regeneration ticks. I believe this is the rate at which your stamina will regenerate when you are not using stamina abilities. Lowering this value will make your stamina regenerate faster, raising it will make your stamina regenerate slower.", null, new ConfigurationManagerAttributes { Order = 11 }));
        PlayerStaminaRegenValue = Config.Bind($"Player", "Stamina Regen Value", 30f, new ConfigDescription("Amount of stamina regenerated per tick. I believe this is the amount of stamina you will gain each time your stamina regenerates. Raising this value will make your stamina regenerate more per tick, lowering it will make your stamina regenerate less per tick.", null, new ConfigurationManagerAttributes { Order = 10 }));
        PlayerInfiniteStamina = Config.Bind($"Player", "Infinite Stamina", false, new ConfigDescription("On every update makes your stamina maximized", null, new ConfigurationManagerAttributes { Order = 9 }));
        PlayerInfiniteStaminaEffect = Config.Bind($"Player", "Infinite Stamina Effect", false, new ConfigDescription("Whether to draw the infinite stamina effect", null, new ConfigurationManagerAttributes { Order = 8 }));

        // Player Health
        PlayerMaxHealth = Config.Bind($"Player", "Max Health", 100f, new ConfigDescription("Set your max health", null, new ConfigurationManagerAttributes { Order = 7 }));
        PlayerHealthRegenInterval = Config.Bind($"Player", "Health Regen Interval", 5f, new ConfigDescription("Theoretically: Interval in seconds between health regeneration ticks, feel free to experiment I didn't test this out yet", null, new ConfigurationManagerAttributes { Order = 6 }));
        PlayerHealthRegenModifier = Config.Bind($"Player", "Health Regen Modifier", 1f, new ConfigDescription("Theoretically: Multiplier for health regen value, feel free to experiment I didn't test this out yet", null, new ConfigurationManagerAttributes { Order = 5 }));
        PlayerHealthRegenValue = Config.Bind($"Player", "Health Regen Value", 0f, new ConfigDescription("Theoretically: Amount of health regenerated per tick, feel free to experiment I didn't test this out yet", null, new ConfigurationManagerAttributes { Order = 4 }));
        PlayerGodmode = Config.Bind($"Player", "Enable Godmode", false, new ConfigDescription("Makes you invulnerable and on every update makes your health maximized", null, new ConfigurationManagerAttributes { Order = 3 }));

        // Player Speed
        PlayerWalkSpeed = Config.Bind($"Player", "Walk Speed", 7.5f, new ConfigDescription("Set your walk speed", null, new ConfigurationManagerAttributes { Order = 2 }));
        PlayerRunSpeed = Config.Bind($"Player", "Run Speed", 15f, new ConfigDescription("Set your run speed", null, new ConfigurationManagerAttributes { Order = 1 }));
        PlayerRunSpeedModifier = Config.Bind($"Player", "Run Speed Modifier", 1f, new ConfigDescription("Multiplies your run speed by this value", null, new ConfigurationManagerAttributes { Order = 0 }));

        // Time
        TimeModification = Config.Bind($"Time", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 5 }));
        DaytimeFlow = Config.Bind($"Time", "Daytime Flow", 1f, new ConfigDescription("Set the day time interval. Lower values make time pass faster, higher values make time pass slower. Be cautious: very high values can cause these options to update extremely slowly.", null, new ConfigurationManagerAttributes { Order = 4 }));
        NighttimeFlow = Config.Bind($"Time", "Nighttime Flow", 0.75f, new ConfigDescription("Set the day time interval. Lower values make time pass faster, higher values make time pass slower. Be cautious: very high values can cause these options to update extremely slowly.", null, new ConfigurationManagerAttributes { Order = 3 }));
        UseCurrentTime = Config.Bind($"Time", "Set Time", false, new ConfigDescription("Enable this to use the config for time of day below", null, new ConfigurationManagerAttributes { Order = 2 }));
        CurrentTime = Config.Bind($"Time", "Set Current Time", 1, new ConfigDescription("(1) is day (8:01), (900) is (18:00), (1440) is end of night (set it to 1439 and then disable set time)", null, new ConfigurationManagerAttributes { Order = 1 }));
        TimeStop = Config.Bind($"Time", "Stop Time", false, new ConfigDescription("Doesn't let time get any higher", null, new ConfigurationManagerAttributes { Order = 1 }));
        ResetWell = Config.Bind($"Time", "Reset Well", false, new ConfigDescription("Whether or not to reset well constantly", null, new ConfigurationManagerAttributes { Order = 0 }));

        // Generator
        GeneratorModification = Config.Bind($"Generator", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 2 }));
        GeneratorModifier = Config.Bind($"Generator", "Generator Modifier", 1f, new ConfigDescription("2x is twice as fast drainage, 1x is as fast as normal, 0.5x is half as fast", null, new ConfigurationManagerAttributes { Order = 1 }));
        GeneratorInfiniteFuel = Config.Bind($"Generator", "Generator Infinte Fuel", false, new ConfigDescription("Enable this to make the generator have infinite fuel", null, new ConfigurationManagerAttributes { Order = 0 }));

        // Camera
        CameraModification = Config.Bind($"Camera", "Enable Section", false, new ConfigDescription("Enable this section of the mod, This section does not require restarts", null, new ConfigurationManagerAttributes { Order = 1 }));
        CameraFoV = Config.Bind($"Camera", "Camera Zoom Factor", 1f, new ConfigDescription("Changes the zoom factor of the camera, lower values is zoomed out, higher values is zoomed in", null, new ConfigurationManagerAttributes { Order = 0 }));

        // Keybinds
        KeybindGodmode = Config.Bind("Hotkeys", "Toggle Godmode", new KeyboardShortcut(KeyCode.G, KeyCode.LeftShift));
        KeybindStamina = Config.Bind("Hotkeys", "Toggle Infinite Stamina", new KeyboardShortcut(KeyCode.H, KeyCode.LeftShift));
        KeybindTime = Config.Bind("Hotkeys", "Toggle Time Stop", new KeyboardShortcut(KeyCode.T, KeyCode.LeftShift));
    }

    private void MakeDefaults()
    {
        if (!Directory.Exists(DefaultsConfigPath))
            Directory.CreateDirectory(DefaultsConfigPath);

        string DefaultsReadMePath = Path.Combine(DefaultsConfigPath, "ReadMe.txt");
        string ReadMeString = "This folder is a readonly folder for you to use as a template when creating your own custom things.\nYou are not meant to edit this, just to read\nThe custom json files you can edit are in the DarkwoodCustomizer/Customs folder, not in the ModDefaults folder\nI recommend copying these files and replacing the default values with your own custom ones";
        if (File.Exists(DefaultsReadMePath) && !File.ReadAllText(DefaultsReadMePath).Equals(ReadMeString))
            File.Delete(DefaultsReadMePath);
        if (!File.Exists(DefaultsReadMePath))
            File.WriteAllText(DefaultsReadMePath, ReadMeString);

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

        // 1.2.6 migration check
        Migrate126Configs();
        // 1.2.8 migration check
        Migrate128Configs();

        BepinexBindings();
        MakeDefaults();

        LogDivider();

        Harmony Harmony = new Harmony($"{PluginGUID}");
        Log.LogInfo($"Patching in ItemPatch! (beartrap disarm)");
        Harmony.PatchAll(typeof(ItemPatch));
        Log.LogInfo($"Patching in InvItemClassPatch! (Items)");
        Harmony.PatchAll(typeof(InvItemClassPatch));
        Log.LogInfo($"Patching in InventoryPatch! (Storage)");
        Harmony.PatchAll(typeof(InventoryPatch));
        Log.LogInfo($"Patching in CharacterPatch! (Soontm)");
        Harmony.PatchAll(typeof(CharacterPatch));
        Log.LogInfo($"Patching in PlayerPatch! (Player Update)");
        Harmony.PatchAll(typeof(PlayerPatch));
        Log.LogInfo($"Patching in ControllerPatch! (Time)");
        Harmony.PatchAll(typeof(ControllerPatch));
        Log.LogInfo($"Patching in GeneratorPatch! (Generator Fuel)");
        Harmony.PatchAll(typeof(GeneratorPatch));
        Log.LogInfo($"Patching in CamMainPatch! (Camera)");
        Harmony.PatchAll(typeof(CamMainPatch));
        Log.LogInfo($"Patching in WorkbenchPatch! (Recipes)");
        Harmony.PatchAll(typeof(WorkbenchPatch));
        Log.LogInfo($"Patching in LanguagePatch! (Item Names)");
        Harmony.PatchAll(typeof(LanguagePatch));

        Log.LogInfo($"[{PluginGUID} v{PluginVersion}] has fully loaded!");
        LogDivider();

        fileWatcher = new FileSystemWatcher(Paths.ConfigPath, "*.cfg");
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        fileWatcher.Changed += OnFileChanged;
        fileWatcher.Deleted += OnFileDeleted;
        fileWatcher.EnableRaisingEvents = true;

        fileWatcherDefaults = new FileSystemWatcher(DefaultsConfigPath, "*.json");
        fileWatcherDefaults.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        fileWatcherDefaults.Changed += OnFileChangedDefaults;
        fileWatcherDefaults.Deleted += OnFileDeleted;
        fileWatcherDefaults.EnableRaisingEvents = true;

        fileWatcherJson = new FileSystemWatcher(JsonConfigPath, "*.json");
        fileWatcherJson.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;
        fileWatcherJson.Changed += OnFileChanged;
        fileWatcherJson.Deleted += OnFileDeleted;
        fileWatcherJson.EnableRaisingEvents = true;

        // 1.3.3 migration check
        Migrate133Configs();
    }

    public void Update()
    {
        if (KeybindGodmode.Value.IsDown())
        {
            PlayerGodmode.Value = !PlayerGodmode.Value;
            Log.LogInfo($"Player Godmode toggled!");
        }
        if (KeybindStamina.Value.IsDown())
        {
            PlayerInfiniteStamina.Value = !PlayerInfiniteStamina.Value;
            Log.LogInfo($"Player Infinite Stamina toggled!");
        }
        if (KeybindTime.Value.IsDown())
        {
            TimeStop.Value = !TimeStop.Value;
            Log.LogInfo($"Time Stop toggled!");
        }
    }
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        switch (e.Name)
        {
            case "amione.DarkwoodCustomizer.cfg":
                Config.Reload();
                GeneratorPatch.RefreshGenerator = true;
                Log.LogInfo($"{e.Name} was reloaded!");
                break;
            case "CustomCharacters.json":
                CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, new JObject { });
                Log.LogInfo($"{e.Name} was reloaded!");
                break;
            case "CustomCraftingRecipes.json":
                CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, new JObject { });
                Log.LogInfo($"{e.Name} was reloaded!");
                break;
            case "CustomItems.json":
                CustomItems = (JObject)GetJsonConfig(CustomItemsPath, new JObject { });
                Log.LogInfo($"{e.Name} was reloaded!");
                break;
            default:
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

    public static object GetJsonConfig(string FilePath, JObject DefaultJson)
    {
        try
        {
            // Try to load the existing file
            var jsonData = GetJsonFile(FilePath);

            if (jsonData == null) return CreateNewJsonFile(FilePath, DefaultJson);
            return jsonData;
        }
        catch (Exception)
        {
            // If the file exists and it errors out, rename it to the same name with an increment and make a new default config
            return CreateNewJsonFile(FilePath, DefaultJson);
        }
    }

    public static JObject CreateNewJsonFile(string FilePath, JObject DefaultJson)
    {
        if (File.Exists(FilePath))
        {
            var i = 0;
            var NewFilePath = FilePath;
            while (File.Exists(NewFilePath))
            {
                NewFilePath = Path.Combine(Path.GetDirectoryName(FilePath), $"{Path.GetFileNameWithoutExtension(FilePath)}_error_{i++}{Path.GetExtension(FilePath)}");
            }
            File.Move(FilePath, NewFilePath);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(DefaultJson, Formatting.Indented));
            Log.LogInfo($"Renamed {FilePath} to {NewFilePath} and created new default config because it was erroring out.");
        }
        else
        {
            var directory = Path.GetDirectoryName(FilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(DefaultJson, Formatting.Indented));
            Log.LogInfo($"Created {FilePath} with default config because it didnt exist.");
        }
        return DefaultJson;
    }

    public static JObject GetJsonFile(string JsonPath)
    {
        try
        {
            // Read the file contents
            var fileContents = File.ReadAllText(JsonPath);

            // Parse the cleaned JSON string
            JObject jsonObject = JObject.Parse(fileContents);

            return jsonObject;
        }
        catch (Exception ex)
        {
            Log.LogInfo($"Error loading JSON file: {ex.Message}");
            return null;
        }
    }

    private void Migrate126Configs()
    {
        // 1.2.6 migration check
        // Move JSON files
        string oldJsonPath = Path.Combine(Paths.ConfigPath, PluginGUID);
        string newJsonPath = Path.Combine(Paths.ConfigPath, PluginGUID, "Customs");
        if (!Directory.Exists(newJsonPath)) Directory.CreateDirectory(newJsonPath);
        foreach (string jsonFile in Directory.GetFiles(oldJsonPath, "*.json"))
        {
            string fileName = Path.GetFileName(jsonFile);
            string newJsonFile = Path.Combine(newJsonPath, fileName == "CustomCharacters.json" ? fileName + "_ThisIsYourOldConfigFrom1.2.6.json" : fileName);
            if (File.Exists(newJsonFile)) File.Delete(newJsonFile);
            File.Move(jsonFile, newJsonFile);
            Log.LogInfo($"Moved and renamed old JSON file from {jsonFile} to {newJsonFile}");
        }

        // Remove old defaults folders
        string[] OldDefaultsFolders = [Path.Combine(Paths.ConfigPath, PluginGUID, "defaults"), Path.Combine(Paths.ConfigPath, PluginGUID, "VanillaDefaults")];
        foreach (string OldDefaultsFolder in OldDefaultsFolders)
        {
            if (Directory.Exists(OldDefaultsFolder))
            {
                Directory.Delete(OldDefaultsFolder, true);
                Log.LogInfo($"Deleted old defaults folder at \"{OldDefaultsFolder}\"");
            }
        }
    }


    private void Migrate128Configs()
    {
        // 1.2.8 migration check
        string OldLanternConfig = Path.Combine(Paths.ConfigPath, PluginGUID, "Lantern.cfg");
        if (File.Exists(OldLanternConfig))
        {
            File.Delete(OldLanternConfig);
            Log.LogInfo($"Deleted old Lantern config file at {OldLanternConfig}");
        }
        string OldDefaultStacksJson = Path.Combine(DefaultsConfigPath, "CustomStacks.json");
        if (File.Exists(OldDefaultStacksJson))
        {
            File.Delete(OldDefaultStacksJson);
            Log.LogInfo($"Old Default CustomStacks Json was deleted at {OldDefaultStacksJson}");
        }
        string NewPathConfig;
        string OldStacksConfig = Path.Combine(Paths.ConfigPath, PluginGUID, "CustomStacks.cfg");
        if (File.Exists(OldStacksConfig))
        {
            NewPathConfig = Path.Combine(Paths.ConfigPath, PluginGUID, "CustomStacks_Unused_From_128_DeletePlease.cfg");
            File.Move(OldStacksConfig, NewPathConfig);
            Log.LogInfo($"Old CustomStacks config can be found at at {NewPathConfig}");
        }
        string OldStacksJson = Path.Combine(JsonConfigPath, "CustomStacks.json");
        if (File.Exists(OldStacksJson))
        {
            NewPathConfig = Path.Combine(JsonConfigPath, "CustomStacks_Unused_From_128_DeletePlease.json");
            File.Move(OldStacksJson, NewPathConfig);
            Log.LogInfo($"Old CustomStacks Json file can be found at {NewPathConfig}");
        }
    }

    private void Migrate133Configs()
    {
        List<string> ConfigFiles = new List<string>{
            Path.Combine(Paths.ConfigPath, PluginGUID, "Logging.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Items.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Inventories.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Characters.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Player.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Time.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Generator.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Camera.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Workbench.cfg"),
        };
        List<string> ConfigFilesPreviousVersions = new List<string>{
            Path.Combine(Paths.ConfigPath, PluginGUID, "Logging.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Items.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Inventories.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Characters.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Player.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Time.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Generator.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Camera.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Workbench.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Repairs.cfg"),
            Path.Combine(Paths.ConfigPath, PluginGUID, "Stacks.cfg"),
        };
        string newConfig = "";
        bool changed = false;
        if (ConfigFiles.Count == 9)
        {
            foreach (string ConfigFile in ConfigFiles)
            {
                if (File.Exists(ConfigFile))
                {
                    string category = Path.GetFileNameWithoutExtension(ConfigFile);
                    string[] lines = File.ReadAllLines(ConfigFile);
                    if (category == "Logging") category = "!Mod";
                    if (category == "Workbench") category = "Crafting";
                    newConfig += $"[{category}]\n";
                    foreach (string line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                        {
                            string newLine = line.Trim();
                            if (newLine.Contains("="))
                            {
                                string key = newLine.Split('=')[0].Trim();
                                string value = newLine.Split('=')[1].Trim();
                                newConfig += $"\n{key} = {value}\n";
                                changed = true;
                            }
                        }
                    }
                    Log.LogInfo($"Merged old config file at {ConfigFile} into {PluginGUID}.cfg");
                }
            }
        }
        if (ConfigFilesPreviousVersions.Count > 0)
        {
            foreach (string ConfigPreviousVersion in ConfigFilesPreviousVersions)
            {
                string oldConfigsFolder = Path.Combine(Paths.ConfigPath, PluginGUID, "YourOldConfigs");
                if (!Directory.Exists(oldConfigsFolder)) Directory.CreateDirectory(oldConfigsFolder);
                string newConfigFile = Path.Combine(oldConfigsFolder, $"{Path.GetFileNameWithoutExtension(ConfigPreviousVersion)}.cfg");
                if (File.Exists(ConfigPreviousVersion))
                {
                    File.Move(ConfigPreviousVersion, newConfigFile);
                    Log.LogInfo($"Old config file at {ConfigPreviousVersion} was moved to {newConfigFile} as its not used anymore");
                }
            }
        }
        if (changed) File.WriteAllText(Path.Combine(Paths.ConfigPath, $"{PluginGUID}.cfg"), newConfig);
    }
}
