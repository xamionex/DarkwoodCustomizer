using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace DarkwoodCustomizer;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInProcess("Darkwood.exe")]
public class Plugin : BaseUnityPlugin
{
    public const string PluginAuthor = "amione";
    public const string PluginName = "DarkwoodCustomizer";
    public const string PluginVersion = "1.1.8";
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;

    // Base Plugin Values
    public static ConfigFile ConfigFile;
    public static ConfigEntry<bool> LogDebug;
    public static ConfigEntry<bool> LogItems;
    public static ConfigEntry<bool> LogCharacters;

    // Stack Resize Values
    public static ConfigFile StacksConfigFile;
    public static ConfigEntry<bool> ChangeStacks;
    public static ConfigEntry<bool> UseGlobalStackSize;
    public static ConfigEntry<int> StackResize;
    public static ConfigEntry<string> jsonStacks;
    public static Dictionary<string, int> CustomStacks;

    // Items Values
    public static ConfigFile ItemsConfigFile;
    public static ConfigEntry<bool> EnableItemsModification;
    public static ConfigEntry<bool> BearTrapRecovery;

    // Lantern Repair Values
    public static ConfigFile LanternConfigFile;
    public static ConfigEntry<bool> RepairLantern;
    public static ConfigEntry<string> LanternRepairConfig;
    public static ConfigEntry<int> LanternAmountRepairConfig;
    public static ConfigEntry<float> LanternDurabilityRepairConfig;

    // Inventory Resize Values
    public static ConfigFile InventoriesConfigFile;
    public static ConfigEntry<bool> RemoveExcess;

    // Workbench
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
    public static ConfigFile CharacterConfigFile;
    public static ConfigEntry<bool> CharacterModification;
    public static ConfigEntry<string> jsonCharacters;
    public static Dictionary<string, Dictionary<string, float>> CustomCharacters;

    // Player Values
    public static ConfigFile PlayerConfigFile;
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
    public static ConfigFile TimeConfigFile;
    public static ConfigEntry<bool> TimeModification;
    public static ConfigEntry<float> DaytimeFlow;
    public static ConfigEntry<float> NighttimeFlow;
    public static ConfigEntry<bool> UseCurrentTime;
    public static ConfigEntry<int> CurrentTime;
    public static ConfigEntry<bool> ResetWell;

    // Generator values
    public static ConfigFile GeneratorConfigFile;
    public static ConfigEntry<bool> GeneratorModification;
    public static ConfigEntry<float> GeneratorModifier;
    public static ConfigEntry<bool> GeneratorInfiniteFuel;

    // Camera values
    public static ConfigFile CameraConfigFile;
    public static ConfigEntry<bool> CameraModification;
    public static ConfigEntry<float> CameraFoV;

    private void Awake()
    {
        Log = Logger;
        ConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
        StacksConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Stacks.cfg"), true);
        ItemsConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Items.cfg"), true);
        LanternConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Lantern.cfg"), true);
        InventoriesConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Inventories.cfg"), true);
        CharacterConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Characters.cfg"), true);
        PlayerConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Player.cfg"), true);
        TimeConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Time.cfg"), true);
        GeneratorConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Generator.cfg"), true);
        CameraConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Camera.cfg"), true);

        // Base Plugin config
        LogDebug = ConfigFile.Bind($"Logging", "Enable Debug Logs", true, "Whether to log debug messages, includes player information on load/change for now.");
        LogItems = ConfigFile.Bind($"Logging", "Enable Debug Logs for Items", false, "Whether to log every item, only called when the game is loading the specific item\nProtip: Enable on main menu, load your save, disable it, quit the game and open Bepinex/LogOutput.log, then you'll have all the items in the game listed\nYou can comment if you wish to know what the item's name you're looking for is too.");
        LogCharacters = ConfigFile.Bind($"Logging", "Enable Debug Logs for Characters", false, "Whether to log every character, called when the game is updating the specific character\nRead the extended documentation in the Characters config");

        // Stacks config
        ChangeStacks = StacksConfigFile.Bind($"Stack Sizes", "Enable Section", false, "Whether or not stack sizes will be changed by the mod.");
        UseGlobalStackSize = StacksConfigFile.Bind($"Stack Sizes", "Enable Global Stack Size", true, "Whether to use a global stack size for all items.");
        StackResize = StacksConfigFile.Bind($"Stack Sizes", "Global Stack Resize", 50, "Number for all item stack sizes to be set to. Requires reload of save for most items to take effect (Return to Menu > Load Save)");
        var jsonStacks = StacksConfigFile.Bind($"Stack Sizes", "Custom Stacks", "{\"nail\":500,\"wood\":500}", "Enable the logs for items in the main config. Requires reload of save for most items to take effect (Return to Menu > Load Save)");
        CustomStacks = JsonConvert.DeserializeObject<Dictionary<string, int>>(jsonStacks.Value);

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
        CharacterModification = CharacterConfigFile.Bind($"Characters", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        var jsonCharacters = CharacterConfigFile.Bind($"Characters", "Custom Characters", "{\"Dog\":{\"health\":20,\"speed\":1,\"damage\":0},\"Rabbit\":{\"health\":1,\"speed\":1,\"damage\":0}}", "Warning: Enable character logs in main config. Be cautious with numbers, but name errors are harmless. Health is static, Speed is a modifier, and Damage is not yet implemented.\nIf you don't know which enemy is which download RuntimeUnityEditor and place it in the plugins folder, it will allow you to search the names in the object browser and see their textures (aka how they look)");
        CustomCharacters = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, float>>>(jsonCharacters.Value);

        // Player values
        PlayerModification = PlayerConfigFile.Bind($"Player", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerFOV = PlayerConfigFile.Bind($"Player", "Player FoV", 90f, "Set your players' FoV (370 recommended, set to 720 if you want to always see everything)");
        PlayerCantGetInterrupted = PlayerConfigFile.Bind($"Player", "Cant Get Interrupted", true, "If set to true you can't get stunned, shoddy implementation please report if it doesn't work correctly");

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
        Log.LogInfo($"Patching in SaveManagerPatch! (Loading values)");
        Harmony.PatchAll(typeof(SaveManagerPatch));

        Log.LogInfo($"[{PluginGUID} v{PluginVersion}] has fully loaded!");
        LogDivider();

        fileWatcher = new FileSystemWatcher(Paths.ConfigPath, PluginGUID + "*.cfg");
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnFileChanged;
        fileWatcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        LogDivider();
        switch (e.Name)
        {
            case PluginGUID + ".cfg":
                Log.LogInfo($"{PluginGUID}.cfg was reloaded.");
                ConfigFile.Reload();
                break;
            case PluginGUID + ".Inventories.cfg":
                Log.LogInfo($"{PluginGUID}.Inventories.cfg was reloaded.");
                InventoriesConfigFile.Reload();
                break;
            case PluginGUID + ".Items.cfg":
                Log.LogInfo($"{PluginGUID}.Items.cfg was reloaded.");
                ItemsConfigFile.Reload();
                break;
            case PluginGUID + ".Lantern.cfg":
                Log.LogInfo($"{PluginGUID}.Lantern.cfg was reloaded.");
                InvItemClassPatch.RefreshLantern = true;
                LanternConfigFile.Reload();
                break;
            case PluginGUID + ".Player.cfg":
                Log.LogInfo($"{PluginGUID}.Player.cfg was reloaded.");
                PlayerPatch.RefreshPlayer = true;
                PlayerConfigFile.Reload();
                break;
            case PluginGUID + ".Characters.cfg":
                Log.LogInfo($"{PluginGUID}.Characters.cfg was reloaded.");
                CharacterConfigFile.Reload();
                break;
            case PluginGUID + ".Stacks.cfg":
                Log.LogInfo($"{PluginGUID}.Stacks.cfg was reloaded.");
                StacksConfigFile.Reload();
                break;
            case PluginGUID + ".Time.cfg":
                Log.LogInfo($"{PluginGUID}.Time.cfg was reloaded.");
                TimeConfigFile.Reload();
                break;
            case PluginGUID + ".Generator.cfg":
                Log.LogInfo($"{PluginGUID}.Generator.cfg was reloaded.");
                GeneratorPatch.RefreshGenerator = true;
                GeneratorConfigFile.Reload();
                break;
            case PluginGUID + ".Camera.cfg":
                Log.LogInfo($"{PluginGUID}.Camera.cfg was reloaded.");
                CameraConfigFile.Reload();
                break;
            default:
                Log.LogInfo($"Unknown file with the PluginGUID was reloaded.");
                break;
        }
        LogDivider();
    }

    public static void LogDivider()
    {
        Log.LogInfo("");
        Log.LogInfo("--------------------------------------------------------------------------------");
        Log.LogInfo("");
    }
}
