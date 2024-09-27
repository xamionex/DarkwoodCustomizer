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
    public const string PluginVersion = "1.2.6";
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;
    public static FileSystemWatcher fileWatcherJson;

    // Base Plugin Values
    public static string ConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID);
    public static ConfigFile ConfigFile = new(Path.Combine(ConfigPath, "Logging.cfg"), true);
    public static ConfigEntry<string> ThankYouNote;
    public static ConfigEntry<bool> LogDebug;
    public static ConfigEntry<bool> LogItems;
    public static ConfigEntry<bool> LogCharacters;
    public static ConfigEntry<bool> LogWorkbench;

    // Stack Resize Values
    public static ConfigFile StacksConfigFile = new(Path.Combine(ConfigPath, "Stacks.cfg"), true);
    public static ConfigEntry<bool> ChangeStacks;
    public static ConfigEntry<bool> UseGlobalStackSize;
    public static ConfigEntry<int> StackResize;
    public static string CustomStacksPath => Path.Combine(ConfigPath, "CustomStacks.json");
    public static JObject CustomStacks;
    public static JObject DefaultCustomStacks = JObject.FromObject(new
    {
        nail = 500,
        wood = 500,
    });

    // Items Values
    public static ConfigFile ItemsConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Items.cfg"), true);
    public static ConfigEntry<bool> EnableItemsModification;
    public static ConfigEntry<bool> BearTrapRecovery;

    // Lantern Repair Values
    public static ConfigFile LanternConfigFile = new ConfigFile(Path.Combine(ConfigPath, "Lantern.cfg"), true);
    public static ConfigEntry<bool> RepairLantern;
    public static ConfigEntry<string> LanternRepairConfig;
    public static ConfigEntry<int> LanternAmountRepairConfig;
    public static ConfigEntry<float> LanternDurabilityRepairConfig;

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
    public static string CustomCharactersPath => Path.Combine(ConfigPath, "CustomCharacters.json");
    public static JObject CustomCharacters;
    public static JObject DefaultCustomCharacters = JObject.FromObject(new
    {
        Dog = new
        {
            health = 20f,
            walkspeed = 2f,
            runspeed = 10f,
            damage = 1f,
        },
        Rabbit = new
        {
            health = 1f,
            walkspeed = 2f,
            runspeed = 8f,
            damage = 1f,
        },
    });

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
    public static ConfigEntry<bool> WorkbenchSetLevel;
    public static ConfigEntry<int> WorkbenchLevel;
    public static string CustomCraftingRecipesPath => Path.Combine(ConfigPath, "CustomCraftingRecipes.json");
    public static JObject CustomCraftingRecipes;
    public static JObject DefaultCustomCraftingRecipes = JObject.FromObject(new
    {
        ammo_clip_mediumCal = new
        {
            requiredlevel = 3,
            icon = "inventoryitems/ammo/ammo_clip_mediumCal",
            requirements = new
            {
                ammo_single_mediumCal = 10,
                wire = 1,
                junk = 6,
            },
        },
        knife = new
        {
            requiredlevel = 3,
            icon = "inventoryitems/meleeweapons/knife",
            requirements = new
            {
                junk = 1,
                wood = 1,
                tape = 1,
                stone = 1,
            },
        },
    });

    private void Awake()
    {
        Log = Logger;

        // 1.1.9 migration check
        MigrateConfigFiles();

        // Base Plugin config
        ThankYouNote = ConfigFile.Bind($"Note", "Thank you", "", "Thank you for downloading my mod, every config is explained in it's description above it, if a config doesn't have comments above it, it's probably an old config that was in a previous version.");
        LogDebug = ConfigFile.Bind($"Logging", "Enable Debug Logs", true, "Whether to log debug messages, includes player information on load/change for now.");
        LogItems = ConfigFile.Bind($"Logging", "Enable Debug Logs for Items", false, "Whether to log every item, only called when the game is loading the specific item\nProtip: Enable on main menu, load your save, disable it, quit the game and open Bepinex/LogOutput.log, then you'll have all the items in the game listed\nYou can comment if you wish to know what the item's name you're looking for is too.");
        LogCharacters = ConfigFile.Bind($"Logging", "Enable Debug Logs for Characters", false, "Whether to log every character, called when the game is load the specific character\nRS=Run Speed, WS=Walk Speed\nRead the extended documentation in the Characters config");
        LogWorkbench = ConfigFile.Bind($"Logging", "Enable Debug Logs for Workbench", false, "Whether to log every time a custom recipe is added to the workbench");

        // Stacks config
        ChangeStacks = StacksConfigFile.Bind($"Stack Sizes", "Enable Section", false, "Whether or not stack sizes will be changed by the mod.");
        UseGlobalStackSize = StacksConfigFile.Bind($"Stack Sizes", "Enable Global Stack Size", true, "Whether to use a global stack size for all items.");
        StackResize = StacksConfigFile.Bind($"Stack Sizes", "Global Stack Resize", 50, "Number for all item stack sizes to be set to. Requires reload of save for most items to take effect (Return to Menu > Load Save)");
        CustomStacks = (JObject)GetJsonConfig(CustomStacksPath, DefaultCustomStacks);

        // Items config
        EnableItemsModification = ItemsConfigFile.Bind($"Items", "Enable Section", true, "Enable this section of the mod, This section does not require restarts");
        BearTrapRecovery = ItemsConfigFile.Bind($"Items", "BearTrap Recovery", true, "Whether or not you recover a beartrap when disarming it");

        // Lantern config
        RepairLantern = LanternConfigFile.Bind($"Lantern", "Enable Section", true, "Whether or not lantern can be repaired using gasoline on the workbench");
        LanternRepairConfig = LanternConfigFile.Bind($"Lantern", "Lantern Repair Item", "gasoline", "What item will be used for repairing the lantern. (recommended: gasoline or molotov)");
        LanternAmountRepairConfig = LanternConfigFile.Bind($"Lantern", "Lantern Amount of Item Used", 1, "Item amount of item to use? Ex. 1 molotov to repair");
        LanternDurabilityRepairConfig = LanternConfigFile.Bind($"Lantern", "Lantern Durability of Item Used", 0.2f, "Item durability amount to use? Ex. 0.2 of a gasoline to repair");

        // Inventories config
        RemoveExcess = InventoriesConfigFile.Bind($"Inventories", "Remove Excess Slots", true, "Whether or not to remove slots that are outside the inventory you set. For example, you set your inventory to 9x9 (81 slots) but you had a previous mod do something bigger and you have something like 128 slots extra enabling this option will remove those excess slots and bring it down to 9x9 (81)");

        // Workbench
        WorkbenchInventoryModification = InventoriesConfigFile.Bind($"Workbench", "Enable Section", false, "Enables this section of the mod, warning: disabling will not return the workbench to vanilla, you have to do it with this config");
        WorkbenchCraftingOffset = InventoriesConfigFile.Bind($"Workbench", "Workbench Crafting Offset", 1000f, "Pixels offset for the workbench crafting window, no longer requires restart, 1550 is the almost the edge of the screen on fullhd which looks nice");
        RightSlots = InventoriesConfigFile.Bind($"Workbench", "Storage Right Slots", 12, "Number that determines slots in workbench to the right, vanilla is 6");
        DownSlots = InventoriesConfigFile.Bind($"Workbench", "Storage Down Slots", 9, "Number that determines slots in workbench downward, vanilla is 8");

        // Inventory
        InventorySlots = InventoriesConfigFile.Bind($"Inventory", "Enable Section", false, "This will circumvent the inventory progression and enable this section, disable to return to default Inventory slots");
        InventoryRightSlots = InventoriesConfigFile.Bind($"Inventory", "Inventory Right Slots", 2, "Number that determines slots in inventory to the right");
        InventoryDownSlots = InventoriesConfigFile.Bind($"Inventory", "Inventory Down Slots", 9, "Number that determines slots in inventory downward");

        // Hotbar
        HotbarSlots = InventoriesConfigFile.Bind($"Hotbar", "Enable Section", false, "This will circumvent the Hotbar progression and enable this section, disable to return to default Hotbar slots");
        HotbarRightSlots = InventoriesConfigFile.Bind($"Hotbar", "Hotbar Right Slots", 1, "Number that determines slots in Hotbar to the right, requires reload of save (Return to Menu > Load Save)");
        HotbarDownSlots = InventoriesConfigFile.Bind($"Hotbar", "Hotbar Down Slots", 6, "Number that determines slots in Hotbar downward, requires reload of save (Return to Menu > Load Save)");

        // Character values
        CharacterModification = CharacterConfigFile.Bind($"Characters", "Enable Section", false, "Enable this section of the mod, This section updates when a character spawns");
        CharacterConfigFile.Bind($"Characters", "Note", "", "Damage is a modifier and the rest are values, its much harder to modify damage value than just to have a modifier");
        CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, DefaultCustomCharacters);

        // Player values
        PlayerModification = PlayerConfigFile.Bind($"Player", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerFOV = PlayerConfigFile.Bind($"Player", "Player FoV", 90f, "Set your players' FoV (370 recommended, set to 720 if you want to always see everything)");
        PlayerCantGetInterrupted = PlayerConfigFile.Bind($"Player", "Cant Get Interrupted", true, "If set to true you can't get stunned, your cursor will reset color but remember that you're still charged, it just doesn't show it");

        // Player Stamina config
        PlayerStaminaModification = PlayerConfigFile.Bind($"Stamina", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerMaxStamina = PlayerConfigFile.Bind($"Stamina", "Max Stamina", 100f, "Set your max stamina");
        PlayerStaminaRegenInterval = PlayerConfigFile.Bind($"Stamina", "Stamina Regen Interval", 0.05f, "Interval in seconds between stamina regeneration ticks. I believe this is the rate at which your stamina will regenerate when you are not using stamina abilities. Lowering this value will make your stamina regenerate faster, raising it will make your stamina regenerate slower.");
        PlayerStaminaRegenValue = PlayerConfigFile.Bind($"Stamina", "Stamina Regen Value", 30f, "Amount of stamina regenerated per tick. I believe this is the amount of stamina you will gain each time your stamina regenerates. Raising this value will make your stamina regenerate more per tick, lowering it will make your stamina regenerate less per tick.");
        PlayerInfiniteStamina = PlayerConfigFile.Bind($"Stamina", "Infinite Stamina", false, "On every update makes your stamina maximized");
        PlayerInfiniteStaminaEffect = PlayerConfigFile.Bind($"Stamina", "Infinite Stamina Effect", false, "Whether to draw the infinite stamina effect");

        // Player Health config
        PlayerHealthModification = PlayerConfigFile.Bind($"Health", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerMaxHealth = PlayerConfigFile.Bind($"Health", "Max Health", 100f, "Set your max health");
        PlayerHealthRegenInterval = PlayerConfigFile.Bind($"Health", "Health Regen Interval", 5f, "Theoretically: Interval in seconds between health regeneration ticks, feel free to experiment I didn't test this out yet");
        PlayerHealthRegenModifier = PlayerConfigFile.Bind($"Health", "Health Regen Modifier", 1f, "Theoretically: Multiplier for health regen value, feel free to experiment I didn't test this out yet");
        PlayerHealthRegenValue = PlayerConfigFile.Bind($"Health", "Health Regen Value", 0f, "Theoretically: Amount of health regenerated per tick, feel free to experiment I didn't test this out yet");
        PlayerGodmode = PlayerConfigFile.Bind($"Health", "Enable Godmode", false, "Makes you invulnerable and on every update makes your health maximized");

        // Player Speed config
        PlayerSpeedModification = PlayerConfigFile.Bind($"Speed", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerWalkSpeed = PlayerConfigFile.Bind($"Speed", "Walk Speed", 7.5f, "Set your walk speed");
        PlayerRunSpeed = PlayerConfigFile.Bind($"Speed", "Run Speed", 15f, "Set your run speed");
        PlayerRunSpeedModifier = PlayerConfigFile.Bind($"Speed", "Run Speed Modifier", 1f, "Multiplies your run speed by this value");

        // Time config
        TimeModification = TimeConfigFile.Bind($"Time", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        DaytimeFlow = TimeConfigFile.Bind($"Time", "Daytime Flow", 1f, "Set the day time interval. Lower values make time pass faster, higher values make time pass slower. Be cautious: very high values can cause these options to update extremely slowly.");
        NighttimeFlow = TimeConfigFile.Bind($"Time", "Nighttime Flow", 0.75f, "Set the day time interval. Lower values make time pass faster, higher values make time pass slower. Be cautious: very high values can cause these options to update extremely slowly.");
        UseCurrentTime = TimeConfigFile.Bind($"Time", "Set Time", false, "Enable this to use the config for time of day below");
        CurrentTime = TimeConfigFile.Bind($"Time", "Set Current Time", 1, "(1) is day (8:01), (900) is (18:00), (1440) is end of night (set it to 1439 and then disable set time)");
        ResetWell = TimeConfigFile.Bind($"Time", "Reset Well", false, "Whether or not to reset well constantly");

        // Generator config
        GeneratorModification = GeneratorConfigFile.Bind($"Generator", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        GeneratorModifier = GeneratorConfigFile.Bind($"Generator", "Generator Modifier", 1f, "2x is twice as fast drainage, 1x is as fast as normal, 0.5x is half as fast");
        GeneratorInfiniteFuel = GeneratorConfigFile.Bind($"Generator", "Generator Infinte Fuel", false, "Enable this to make the generator have infinite fuel");

        // Camera config
        CameraModification = CameraConfigFile.Bind($"Camera", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        CameraFoV = CameraConfigFile.Bind($"Camera", "Camera Zoom Factor", 1f, "Changes the zoom factor of the camera, lower values is zoomed out, higher values is zoomed in");

        // Workbench config
        WorkbenchModification = WorkbenchConfigFile.Bind($"Workbench", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        WorkbenchSetLevel = WorkbenchConfigFile.Bind($"Workbench", "Workbench Set Level", false, "Enable this to set the level of the workbench you use to the current value below");
        WorkbenchLevel = WorkbenchConfigFile.Bind($"Workbench", "Workbench Level", 0, "Sets the level of the workbench you're using, Level you want-1, so for example Level 8 is 7");
        CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, DefaultCustomCraftingRecipes);

        string DefaultsConfigPath = Path.Combine(Paths.ConfigPath, PluginGUID, "defaults");
        if (!Directory.Exists(DefaultsConfigPath))
            Directory.CreateDirectory(DefaultsConfigPath);

        string DefaultsReadMePath = Path.Combine(DefaultsConfigPath, "ReadMe.txt");
        string ReadMeString = "This folder is a readonly folder for you to use as a template when creating your own custom characters, recipes, etc.\nYou are not meant to edit this, just to read\nThe custom json files you can edit are in the plugin config folder, not in the default folder";
        if (File.Exists(DefaultsReadMePath) && !File.ReadAllText(DefaultsReadMePath).Equals(ReadMeString))
            File.Delete(DefaultsReadMePath);
        if (!File.Exists(DefaultsReadMePath))
            File.WriteAllText(DefaultsReadMePath, ReadMeString);

        string DefaultsCustomCraftingRecipesPath = Path.Combine(DefaultsConfigPath, "CustomCraftingRecipes.json");
        if (File.Exists(DefaultsCustomCraftingRecipesPath) && !File.ReadAllText(DefaultsCustomCraftingRecipesPath).Equals(JsonConvert.SerializeObject(DefaultCustomCraftingRecipes, Formatting.Indented)))
            File.Delete(DefaultsCustomCraftingRecipesPath);
        if (!File.Exists(DefaultsCustomCraftingRecipesPath))
            File.WriteAllText(DefaultsCustomCraftingRecipesPath, JsonConvert.SerializeObject(DefaultCustomCraftingRecipes, Formatting.Indented));

        string DefaultsCustomStacksPath = Path.Combine(DefaultsConfigPath, "CustomStacks.json");
        if (File.Exists(DefaultsCustomStacksPath) && !File.ReadAllText(DefaultsCustomStacksPath).Equals(JsonConvert.SerializeObject(DefaultCustomStacks, Formatting.Indented)))
            File.Delete(DefaultsCustomStacksPath);
        if (!File.Exists(DefaultsCustomStacksPath))
            File.WriteAllText(DefaultsCustomStacksPath, JsonConvert.SerializeObject(DefaultCustomStacks, Formatting.Indented));

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

        Log.LogInfo($"[{PluginGUID} v{PluginVersion}] has fully loaded!");
        LogDivider();

        fileWatcher = new FileSystemWatcher(ConfigPath, "*.cfg");
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnFileChanged;
        fileWatcher.EnableRaisingEvents = true;

        fileWatcherJson = new FileSystemWatcher(ConfigPath, "*.json");
        fileWatcherJson.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcherJson.Changed += OnFileChanged;
        fileWatcherJson.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var UnknownFile = false;
        LogDivider();
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
            case "Lantern.cfg":
                InvItemClassPatch.RefreshLantern = true;
                LanternConfigFile.Reload();
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
            case "Stacks.cfg":
                StacksConfigFile.Reload();
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
            case "CustomStacks.json":
                CustomStacks = (JObject)GetJsonConfig(CustomStacksPath, DefaultCustomStacks);
                break;
            case "CustomCharacters.json":
                CustomCharacters = (JObject)GetJsonConfig(CustomCharactersPath, DefaultCustomCharacters);
                break;
            case "CustomCraftingRecipes.json":
                CustomCraftingRecipes = (JObject)GetJsonConfig(CustomCraftingRecipesPath, DefaultCustomCraftingRecipes);
                break;
            default:
                Log.LogInfo($"Unknown file with the PluginGUID was reloaded.");
                UnknownFile = true;
                break;
        }
        if (!UnknownFile) Log.LogInfo($"{e.Name} was reloaded!");
        LogDivider();
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
        var i = 0;
        var NewFilePath = FilePath;
        if (File.Exists(FilePath))
        {
            while (File.Exists(NewFilePath))
            {
                NewFilePath = $"{Path.GetFileNameWithoutExtension(FilePath)}_error_{i++}{Path.GetExtension(FilePath)}";
            }
            File.Move(FilePath, NewFilePath);
            File.WriteAllText(FilePath, JsonConvert.SerializeObject(DefaultJson, Formatting.Indented));
            Log.LogInfo($"Renamed {FilePath} to {NewFilePath} and created new default config because it was erroring out.");
        }
        else
        {
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
            Log.LogError($"Error loading JSON file: {ex.Message}");
            return null;
        }
    }


    private void MigrateConfigFiles()
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
        LanternConfigFile.Reload();
        PlayerConfigFile.Reload();
        CharacterConfigFile.Reload();
        CharacterConfigFile.Reload();
        StacksConfigFile.Reload();
        TimeConfigFile.Reload();
        GeneratorConfigFile.Reload();
        CameraConfigFile.Reload();
        Log.LogInfo("Reloaded all configs!");
    }
}
