using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace DarkwoodCustomizer;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInProcess("Darkwood.exe")]
public class Plugin : BaseUnityPlugin
{
    public const string PluginAuthor = "amione";
    public const string PluginName = "DarkwoodCustomizer";
    public const string PluginVersion = "1.2.9";
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;
    public static FileSystemWatcher fileWatcherJson;
    public static FileSystemWatcher fileWatcherDefaults;

    // Base Plugin Values
    public static string ConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID);
    public static string JsonConfigPath = Path.Combine(ConfigPath, "Customs");
    public static string DefaultsConfigPath = Path.Combine(ConfigPath, "ModDefaults");
    public static ConfigFile ConfigFile = new(Path.Combine(ConfigPath, "Logging.cfg"), true);
    public static ConfigEntry<bool> LogDebug;
    public static ConfigEntry<bool> LogItems;
    public static ConfigEntry<bool> LogCharacters;
    public static ConfigEntry<bool> LogWorkbench;

    // Items Values
    public static ConfigFile ItemsConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Items.cfg"), true);
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
            name = "Flamethrower",
            description = "It's a flamethrower!",
            iconType = "weapon_flamethrower_military_01",
            fireMode = "fullAuto",
            hasAmmo = false,
            canBeReloaded = true,
            ammoReloadType = "magazine",
            ammoType = "gasoline",
            hasDurability = true,
            maxDurability = 200f,
            ignoreDurabilityInValue = true,
            repairable = true,
            requirements = new
            {
                gasoline = 1,
            },
            damage = 60,
            flamethrowerdrag = 0.4f,
            flamethrowercontactDamage = 20,
            clipSize = 1000,
            value = 100,
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
    public static ConfigFile InventoriesConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Inventories.cfg"), true);
    public static ConfigEntry<bool> RemoveExcess;

    // Workbench
    public static ConfigEntry<bool> WorkbenchInventoryModification;
    public static ConfigEntry<float> WorkbenchCraftingOffset;
    public static ConfigEntry<int> RightSlots;
    public static ConfigEntry<int> DownSlots;

    // Inventory
    public static ConfigEntry<int> InventoryRightSlots;
    public static ConfigEntry<int> InventoryDownSlots;
    public static ConfigEntry<bool> InventorySlots;

    // Hotbar
    public static ConfigEntry<int> HotbarRightSlots;
    public static ConfigEntry<int> HotbarDownSlots;
    public static ConfigEntry<bool> HotbarSlots;

    // Character Values
    public static ConfigFile CharacterConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Characters.cfg"), true);
    public static ConfigEntry<bool> CharacterModification;
    public static string CustomCharactersPath => Path.Combine(JsonConfigPath, "CustomCharacters.json");
    public static JObject CustomCharacters;

    // Player Values
    public static ConfigFile PlayerConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Player.cfg"), true);
    public static ConfigEntry<bool> PlayerModification;
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
    public static ConfigFile TimeConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Time.cfg"), true);
    public static ConfigEntry<bool> TimeModification;
    public static ConfigEntry<float> DaytimeFlow;
    public static ConfigEntry<float> NighttimeFlow;
    public static ConfigEntry<bool> UseCurrentTime;
    public static ConfigEntry<int> CurrentTime;
    public static ConfigEntry<bool> ResetWell;

    // Generator values
    public static ConfigFile GeneratorConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Generator.cfg"), true);
    public static ConfigEntry<bool> GeneratorModification;
    public static ConfigEntry<float> GeneratorModifier;
    public static ConfigEntry<bool> GeneratorInfiniteFuel;

    // Camera values
    public static ConfigFile CameraConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Camera.cfg"), true);
    public static ConfigEntry<bool> CameraModification;
    public static ConfigEntry<float> CameraFoV;

    // Workbench values
    public static ConfigFile WorkbenchConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Workbench.cfg"), true);
    public static ConfigEntry<bool> WorkbenchModification;
    public static ConfigEntry<bool> CustomCraftingRecipesUseDefaults;
    public static ConfigEntry<bool> WorkbenchUnusedContinue;
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
    private void BepinexBindings()
    {
        // Base Plugin config
        ConfigFile CategoryConfigFile = ConfigFile;
        CategoryConfigFile.Bind($"Note", "Thank you", "no prob!", "Thank you for downloading my mod, every config is explained in it's description above it, if a config doesn't have comments above it, it's probably an old config that was in a previous version.");
        LogDebug = CategoryConfigFile.Bind($"Logging", "Enable Debug Logs", true, "Whether to log debug messages, includes player information on load/change for now.");
        LogItems = CategoryConfigFile.Bind($"Logging", "Enable Debug Logs for Items", false, "Whether to log every item, only called when the game is loading the specific item\nProtip: Enable on main menu, load your save, disable it, quit the game and open Bepinex/LogOutput.log, then you'll have all the items in the game listed\nYou can comment if you wish to know what the item's name you're looking for is too.");
        LogCharacters = CategoryConfigFile.Bind($"Logging", "Enable Debug Logs for Characters", false, "Whether to log every character, called when the game is load the specific character\nRS=Run Speed, WS=Walk Speed\nRead the extended documentation in the Characters config");
        LogWorkbench = CategoryConfigFile.Bind($"Logging", "Enable Debug Logs for Workbench", false, "Whether to log every time a custom recipe is added to the workbench");

        // Items config
        CategoryConfigFile = ItemsConfigFile;
        ItemsModification = CategoryConfigFile.Bind($"Items", "Enable Section", true, "Enable this section of the mod, This section does require save reloads for everything except trap recoveries");
        BearTrapRecovery = CategoryConfigFile.Bind($"Items", "BearTrap Recovery", true, "Whether or not you recover a beartrap when disarming it");
        BearTrapRecoverySwitch = CategoryConfigFile.Bind($"Items", "BearTrap Recover Items", true, "false = beartrap disarm gives a beartrap\ntrue = beartrap disarm gives 3 scrap metal");
        ChainTrapRecovery = CategoryConfigFile.Bind($"Items", "ChainTrap Recovery", true, "Whether or not you recover a chaintrap when disarming it");
        ChainTrapRecoverySwitch = CategoryConfigFile.Bind($"Items", "ChainTrap Recover Items", true, "false = chaintrap disarm gives a chaintrap\ntrue = chaintrap disarm gives 2 scrap metal");
        UseGlobalStackSize = CategoryConfigFile.Bind($"Stack Sizes", "Enable Global Stack Size", true, "Whether to use a global stack size for all items.");
        StackResize = CategoryConfigFile.Bind($"Stack Sizes", "Global Stack Resize", 50, "Number for all item stack sizes to be set to.");
        CustomItems = (JObject)GetJsonConfig(CustomItemsPath, new JObject { });
        CustomItemsUseDefaults = CategoryConfigFile.Bind($"Items", "Load Mod Defaults First", true, "Whether or not to load mod defaults first and then customs you have\nDon't worry about duplicates, they will be overwritten");

        // Inventories config
        CategoryConfigFile = InventoriesConfigFile;
        RemoveExcess = CategoryConfigFile.Bind($"Inventories", "Remove Excess Slots", true, "Whether or not to remove slots that are outside the inventory you set. For example, you set your inventory to 9x9 (81 slots) but you had a previous mod do something bigger and you have something like 128 slots extra enabling this option will remove those excess slots and bring it down to 9x9 (81)");

        // Workbench
        WorkbenchInventoryModification = CategoryConfigFile.Bind($"Workbench", "Enable Section", false, "Enables this section of the mod, warning: disabling will not return the workbench to vanilla, you have to do it with this config");
        WorkbenchCraftingOffset = CategoryConfigFile.Bind($"Workbench", "Workbench Crafting Offset", 1000f, "Pixels offset for the workbench crafting window, no longer requires restart, 1550 is the almost the edge of the screen on fullhd which looks nice");
        RightSlots = CategoryConfigFile.Bind($"Workbench", "Storage Right Slots", 12, "Number that determines slots in workbench to the right, vanilla is 6");
        DownSlots = CategoryConfigFile.Bind($"Workbench", "Storage Down Slots", 9, "Number that determines slots in workbench downward, vanilla is 8");

        // Inventory
        InventorySlots = CategoryConfigFile.Bind($"Inventory", "Enable Section", false, "This will circumvent the inventory progression and enable this section, disable to return to default Inventory slots");
        InventoryRightSlots = CategoryConfigFile.Bind($"Inventory", "Inventory Right Slots", 2, "Number that determines slots in inventory to the right");
        InventoryDownSlots = CategoryConfigFile.Bind($"Inventory", "Inventory Down Slots", 9, "Number that determines slots in inventory downward");

        // Hotbar
        HotbarSlots = CategoryConfigFile.Bind($"Hotbar", "Enable Section", false, "This will circumvent the Hotbar progression and enable this section, disable to return to default Hotbar slots");
        HotbarRightSlots = CategoryConfigFile.Bind($"Hotbar", "Hotbar Right Slots", 1, "Number that determines slots in Hotbar to the right.\nRequires reload of save to take effect (Return to Menu > Load Save)");
        HotbarDownSlots = CategoryConfigFile.Bind($"Hotbar", "Hotbar Down Slots", 6, "Number that determines slots in Hotbar downward.\nRequires reload of save to take effect (Return to Menu > Load Save)");

        // Character values
        CategoryConfigFile = CharacterConfigFile;
        CharacterModification = CategoryConfigFile.Bind($"Characters", "Enable Section", false, "Enable this section of the mod, you can edit the characters in Customs/CustomCharacters.json");
        CategoryConfigFile.Bind($"Characters", "Note", "ReadMePlease", "Launch a save once to generate the config\nDo not add more attacks than what was added by default, it'll cancel the damage modification since it'll cause an indexing error");
        CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, new JObject { });

        // Player values
        CategoryConfigFile = PlayerConfigFile;
        PlayerModification = CategoryConfigFile.Bind($"Player", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerFOV = CategoryConfigFile.Bind($"Player", "Player FoV", 90f, "Set your players' FoV (370 recommended, set to 720 if you want to always see everything)");
        PlayerCantGetInterrupted = CategoryConfigFile.Bind($"Player", "Cant Get Interrupted", true, "If set to true you can't get stunned, your cursor will reset color but remember that you're still charged, it just doesn't show it");

        // Player Stamina config
        PlayerStaminaModification = CategoryConfigFile.Bind($"Stamina", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerMaxStamina = CategoryConfigFile.Bind($"Stamina", "Max Stamina", 100f, "Set your max stamina");
        PlayerStaminaRegenInterval = CategoryConfigFile.Bind($"Stamina", "Stamina Regen Interval", 0.05f, "Interval in seconds between stamina regeneration ticks. I believe this is the rate at which your stamina will regenerate when you are not using stamina abilities. Lowering this value will make your stamina regenerate faster, raising it will make your stamina regenerate slower.");
        PlayerStaminaRegenValue = CategoryConfigFile.Bind($"Stamina", "Stamina Regen Value", 30f, "Amount of stamina regenerated per tick. I believe this is the amount of stamina you will gain each time your stamina regenerates. Raising this value will make your stamina regenerate more per tick, lowering it will make your stamina regenerate less per tick.");
        PlayerInfiniteStamina = CategoryConfigFile.Bind($"Stamina", "Infinite Stamina", false, "On every update makes your stamina maximized");
        PlayerInfiniteStaminaEffect = CategoryConfigFile.Bind($"Stamina", "Infinite Stamina Effect", false, "Whether to draw the infinite stamina effect");

        // Player Health config
        PlayerHealthModification = CategoryConfigFile.Bind($"Health", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerMaxHealth = CategoryConfigFile.Bind($"Health", "Max Health", 100f, "Set your max health");
        PlayerHealthRegenInterval = CategoryConfigFile.Bind($"Health", "Health Regen Interval", 5f, "Theoretically: Interval in seconds between health regeneration ticks, feel free to experiment I didn't test this out yet");
        PlayerHealthRegenModifier = CategoryConfigFile.Bind($"Health", "Health Regen Modifier", 1f, "Theoretically: Multiplier for health regen value, feel free to experiment I didn't test this out yet");
        PlayerHealthRegenValue = CategoryConfigFile.Bind($"Health", "Health Regen Value", 0f, "Theoretically: Amount of health regenerated per tick, feel free to experiment I didn't test this out yet");
        PlayerGodmode = CategoryConfigFile.Bind($"Health", "Enable Godmode", false, "Makes you invulnerable and on every update makes your health maximized");

        // Player Speed config
        PlayerSpeedModification = CategoryConfigFile.Bind($"Speed", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerWalkSpeed = CategoryConfigFile.Bind($"Speed", "Walk Speed", 7.5f, "Set your walk speed");
        PlayerRunSpeed = CategoryConfigFile.Bind($"Speed", "Run Speed", 15f, "Set your run speed");
        PlayerRunSpeedModifier = CategoryConfigFile.Bind($"Speed", "Run Speed Modifier", 1f, "Multiplies your run speed by this value");

        // Time config
        CategoryConfigFile = TimeConfigFile;
        TimeModification = CategoryConfigFile.Bind($"Time", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        DaytimeFlow = CategoryConfigFile.Bind($"Time", "Daytime Flow", 1f, "Set the day time interval. Lower values make time pass faster, higher values make time pass slower. Be cautious: very high values can cause these options to update extremely slowly.");
        NighttimeFlow = CategoryConfigFile.Bind($"Time", "Nighttime Flow", 0.75f, "Set the day time interval. Lower values make time pass faster, higher values make time pass slower. Be cautious: very high values can cause these options to update extremely slowly.");
        UseCurrentTime = CategoryConfigFile.Bind($"Time", "Set Time", false, "Enable this to use the config for time of day below");
        CurrentTime = CategoryConfigFile.Bind($"Time", "Set Current Time", 1, "(1) is day (8:01), (900) is (18:00), (1440) is end of night (set it to 1439 and then disable set time)");
        ResetWell = CategoryConfigFile.Bind($"Time", "Reset Well", false, "Whether or not to reset well constantly");

        // Generator config
        CategoryConfigFile = GeneratorConfigFile;
        GeneratorModification = CategoryConfigFile.Bind($"Generator", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        GeneratorModifier = CategoryConfigFile.Bind($"Generator", "Generator Modifier", 1f, "2x is twice as fast drainage, 1x is as fast as normal, 0.5x is half as fast");
        GeneratorInfiniteFuel = CategoryConfigFile.Bind($"Generator", "Generator Infinte Fuel", false, "Enable this to make the generator have infinite fuel");

        // Camera config
        CategoryConfigFile = CameraConfigFile;
        CameraModification = CategoryConfigFile.Bind($"Camera", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        CameraFoV = CategoryConfigFile.Bind($"Camera", "Camera Zoom Factor", 1f, "Changes the zoom factor of the camera, lower values is zoomed out, higher values is zoomed in");

        // Workbench config
        CategoryConfigFile = WorkbenchConfigFile;
        WorkbenchModification = CategoryConfigFile.Bind($"Workbench", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        CategoryConfigFile.Bind($"Workbench", "Note", "okay", "givesamount only works for some items, not sure what dictates this.");
        CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, new JObject { });
        CustomCraftingRecipesUseDefaults = CategoryConfigFile.Bind($"Workbench", "Load Mod Defaults First", true, "Whether or not to load mod defaults first and then customs you have\nDon't worry about duplicates, they will be overwritten");
        WorkbenchUnusedContinue = CategoryConfigFile.Bind($"Workbench", "Try to load unused items", false, "Try to load unused items anyway\nWARNING: This will most likely result in your workbench breaking, use at your own risk");
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

        // 1.1.9 migration check
        Migrate119Configs();
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

        fileWatcher = new FileSystemWatcher(ConfigPath, "*.cfg");
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
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var UnknownFile = false;
        switch (e.Name)
        {
            case "Logging.cfg":
                ConfigFile.Reload();
                break;
            case "Inventories.cfg":
                InventoriesConfigFile.Reload();
                break;
            case "Items.cfg":
                ItemsConfigFile.Reload();
                break;
            case "Player.cfg":
                PlayerPatch.RefreshPlayer = true;
                PlayerConfigFile.Reload();
                break;
            case "Characters.cfg":
                CharacterConfigFile.Reload();
                break;
            case "Characters.json":
                CharacterConfigFile.Reload();
                break;
            case "Time.cfg":
                TimeConfigFile.Reload();
                break;
            case "Generator.cfg":
                GeneratorPatch.RefreshGenerator = true;
                GeneratorConfigFile.Reload();
                break;
            case "Camera.cfg":
                CameraConfigFile.Reload();
                break;
            case "Workbench.cfg":
                WorkbenchConfigFile.Reload();
                break;
            case "CustomCharacters.json":
                CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, new JObject { });
                break;
            case "CustomCraftingRecipes.json":
                CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, new JObject { });
                break;
            case "CustomItems.json":
                CustomItems = (JObject)GetJsonConfig(CustomItemsPath, new JObject { });
                break;
            default:
                Log.LogInfo($"Unknown file was edited.");
                UnknownFile = true;
                break;
        }
        if (!UnknownFile) Log.LogInfo($"{e.Name} was reloaded!");
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


    private void Migrate119Configs()
    {
        // 1.1.9 migration check
        string loggingConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg");
        string[] oldConfigFiles = Directory.GetFiles(Paths.ConfigPath, PluginGUID + ".*.cfg");

        // Special case for logging config
        if (File.Exists(loggingConfigPath))
        {
            string newLoggingConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID, "Logging.cfg");
            if (File.Exists(newLoggingConfigPath)) File.Delete(newLoggingConfigPath);
            File.Move(loggingConfigPath, newLoggingConfigPath);
            Log.LogInfo($"Moved old logging config file from {loggingConfigPath} to {newLoggingConfigPath}");
            ConfigFile.Reload();
        }

        if (oldConfigFiles.Length == 0) return;

        // Move other config files
        foreach (string oldConfigFile in oldConfigFiles)
        {
            string configFileCategory = Path.GetFileNameWithoutExtension(oldConfigFile).Replace(PluginGUID + ".", string.Empty).Replace(".cfg", string.Empty);
            string newConfigFile = Path.Combine(Paths.ConfigPath, PluginGUID, configFileCategory + ".cfg");
            if (File.Exists(newConfigFile)) File.Delete(newConfigFile);
            File.Move(oldConfigFile, newConfigFile);
            Log.LogInfo($"Moved old config file from {oldConfigFile} to {newConfigFile}");
        }
        InventoriesConfigFile.Reload();
        ItemsConfigFile.Reload();
        PlayerConfigFile.Reload();
        CharacterConfigFile.Reload();
        CharacterConfigFile.Reload();
        TimeConfigFile.Reload();
        GeneratorConfigFile.Reload();
        CameraConfigFile.Reload();
        Log.LogInfo("Reloaded all configs!");
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
        string[] OldDefaultsFolders = [Path.Combine(ConfigPath, "defaults"), Path.Combine(ConfigPath, "VanillaDefaults")];
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
        string OldStacksConfig = Path.Combine(ConfigPath, "CustomStacks.cfg");
        if (File.Exists(OldStacksConfig))
        {
            NewPathConfig = Path.Combine(ConfigPath, "CustomStacks_Unused_From_128_DeletePlease.cfg");
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
}
