using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Reflection;

namespace DarkwoodCustomizer;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInProcess("Darkwood.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static PluginInfo pluginInfo;
    public static ConfigFile ConfigFile;
    public static ConfigFile StacksConfigFile;
    public static ConfigFile LanternConfigFile;
    public static ConfigFile InventoriesConfigFile;
    public static ConfigFile PlayerConfigFile;
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "amione";
    public const string PluginName = "DarkwoodCustomizer";
    public const string PluginVersion = "1.1.0";
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;

    // Base Plugin Values
    public static ConfigEntry<bool> LogDebug;

    // StackResize Values
    public static ConfigEntry<int> StackResize;
    public static ConfigEntry<bool> ChangeStacks;
    public static ConfigEntry<bool> RepairLantern;
    public static ConfigEntry<string> LanternRepairConfig;
    public static ConfigEntry<int> LanternAmountRepairConfig;
    public static ConfigEntry<float> LanternDurabilityRepairConfig;
    public static ConfigEntry<bool> LogItems;

    // InventoryResize Values
    public static ConfigEntry<float> WorkbenchCraftingOffset;
    public static ConfigEntry<int> RightSlots;
    public static ConfigEntry<int> DownSlots;
    public static ConfigEntry<int> InventoryRightSlots;
    public static ConfigEntry<int> InventoryDownSlots;
    public static ConfigEntry<bool> InventorySlots;
    public static ConfigEntry<int> HotbarRightSlots;
    public static ConfigEntry<int> HotbarDownSlots;
    public static ConfigEntry<bool> HotbarSlots;
    public static ConfigEntry<bool> RemoveExcess;

    // Player Values
    public static ConfigEntry<bool> PlayerModification;
    public static ConfigEntry<float> PlayerFOV;

    // Player Stamina Values
    public static ConfigEntry<bool> PlayerStaminaModification;
    public static ConfigEntry<int> PlayerStaminaUpgrades;
    public static ConfigEntry<float> PlayerMaxStamina;
    public static ConfigEntry<float> PlayerStaminaRegenInterval;
    public static ConfigEntry<float> PlayerStaminaRegenValue;

    // Player Health Values
    public static ConfigEntry<bool> PlayerHealthModification;
    public static ConfigEntry<int> PlayerHealthUpgrades;
    public static ConfigEntry<float> PlayerMaxHealth;
    public static ConfigEntry<float> PlayerHealthRegenInterval;
    public static ConfigEntry<float> PlayerHealthRegenModifier;
    public static ConfigEntry<float> PlayerHealthRegenValue;

    // Player Speed Values
    public static ConfigEntry<bool> PlayerSpeedModification;
    public static ConfigEntry<float> PlayerWalkSpeed;
    public static ConfigEntry<float> PlayerRunSpeed;
    public static ConfigEntry<float> PlayerRunSpeedModifier;

    private void Awake()
    {
        pluginInfo = Info;
        Log = Logger;
        ConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);
        StacksConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Stacks.cfg"), true);
        LanternConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Lantern.cfg"), true);
        InventoriesConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Inventories.cfg"), true);
        PlayerConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".Player.cfg"), true);

        // Base Plugin config
        LogDebug = ConfigFile.Bind($"Logging", "Enable Debug Logs", true, "Whether to log debug messages, includes player information on load/change for now.");
        LogItems = ConfigFile.Bind($"Logging", "Enable Debug Logs for Items", false, "Whether to log every item the game is loading");

        // Stacks config
        ChangeStacks = StacksConfigFile.Bind($"Stack Sizes", "Enable Section", true, "Whether or not stack sizes will be changed by the mod. Requires reload of save (Return to Menu > Load Save)");
        StackResize = StacksConfigFile.Bind($"Stack Sizes", "Stack Resize", 50, "Number for all item stack sizes to be set to. Requires reload of save (Return to Menu > Load Save)");

        // Lantern config
        RepairLantern = LanternConfigFile.Bind($"Lantern", "Enable Section", true, "Whether or not lantern can be repaired using gasoline on the workbench");
        LanternRepairConfig = LanternConfigFile.Bind($"Lantern", "Lantern Repair Item", "gasoline", "What item will be used for repairing the lantern. (recommended: gasoline or molotov)");
        LanternAmountRepairConfig = LanternConfigFile.Bind($"Lantern", "Lantern Amount of Item Used", 1, "Item amount of item to use? Ex. 1 molotov to repair");
        LanternDurabilityRepairConfig = LanternConfigFile.Bind($"Lantern", "Lantern Durability of Item Used", 0.2f, "Item durability amount to use? Ex. 0.2 of a gasoline to repair");

        // Inventories config
        WorkbenchCraftingOffset = InventoriesConfigFile.Bind($"Workbench", "Workbench Crafting Offset", 1000f, "Pixels offset for the workbench crafting window, no longer requires restart, 1550 is the almost the edge of the screen on fullhd which looks nice");
        RightSlots = InventoriesConfigFile.Bind($"Workbench", "Storage Right Slots", 12, "Number that determines slots in workbench to the right, vanilla is 6");
        DownSlots = InventoriesConfigFile.Bind($"Workbench", "Storage Down Slots", 9, "Number that determines slots in workbench downward, vanilla is 8");
        InventorySlots = InventoriesConfigFile.Bind($"Inventory", "Enable Section", false, "This will circumvent the inventory progression and enable this section, disable to return to default Inventory slots");
        InventoryRightSlots = InventoriesConfigFile.Bind($"Inventory", "Inventory Right Slots", 2, "Number that determines slots in inventory to the right");
        InventoryDownSlots = InventoriesConfigFile.Bind($"Inventory", "Inventory Down Slots", 9, "Number that determines slots in inventory downward");
        HotbarSlots = InventoriesConfigFile.Bind($"Hotbar", "Enable Section", false, "This will circumvent the Hotbar progression and enable this section, disable to return to default Hotbar slots");
        HotbarRightSlots = InventoriesConfigFile.Bind($"Hotbar", "Hotbar Right Slots", 1, "Number that determines slots in Hotbar to the right, requires reload of save (Return to Menu > Load Save)");
        HotbarDownSlots = InventoriesConfigFile.Bind($"Hotbar", "Hotbar Down Slots", 6, "Number that determines slots in Hotbar downward, requires reload of save (Return to Menu > Load Save)");
        RemoveExcess = InventoriesConfigFile.Bind($"Inventories", "Remove Excess Slots", true, "Whether or not to remove slots that are outside the inventory you set. For example, you set your inventory to 9x9 (81 slots) but you had a previous mod do something bigger and you have something like 128 slots extra enabling this option will remove those excess slots and bring it down to 9x9 (81)");

        // Player values
        PlayerModification = PlayerConfigFile.Bind($"Player", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerFOV = PlayerConfigFile.Bind($"Player", "Player FoV", 90f, "Set your players' FoV (370 recommended, set to 720 if you want to always see everything)");

        // Player Stamina config
        PlayerStaminaModification = PlayerConfigFile.Bind($"Stamina", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerMaxStamina = PlayerConfigFile.Bind($"Stamina", "Max Stamina", 100f, "Set your max stamina");
        PlayerStaminaRegenInterval = PlayerConfigFile.Bind($"Stamina", "Stamina Regen Interval", 0.05f, "Interval in seconds between stamina regeneration ticks. I believe this is the rate at which your stamina will regenerate when you are not using stamina abilities. Lowering this value will make your stamina regenerate faster, raising it will make your stamina regenerate slower.");
        PlayerStaminaRegenValue = PlayerConfigFile.Bind($"Stamina", "Stamina Regen Value", 30f, "Amount of stamina regenerated per tick. I believe this is the amount of stamina you will gain each time your stamina regenerates. Raising this value will make your stamina regenerate more per tick, lowering it will make your stamina regenerate less per tick.");

        // Player Health config
        PlayerHealthModification = PlayerConfigFile.Bind($"Health", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerMaxHealth = PlayerConfigFile.Bind($"Health", "Max Health", 100f, "Set your max health");
        PlayerHealthRegenInterval = PlayerConfigFile.Bind($"Health", "Health Regen Interval", 5f, "Theoretically: Interval in seconds between health regeneration ticks, feel free to experiment I didn't test this out yet");
        PlayerHealthRegenModifier = PlayerConfigFile.Bind($"Health", "Health Regen Modifier", 1f, "Theoretically: Multiplier for health regen value, feel free to experiment I didn't test this out yet");
        PlayerHealthRegenValue = PlayerConfigFile.Bind($"Health", "Health Regen Value", 0f, "Theoretically: Amount of health regenerated per tick, feel free to experiment I didn't test this out yet");

        // Player Speed config
        PlayerSpeedModification = PlayerConfigFile.Bind($"Speed", "Enable Section", false, "Enable this section of the mod, This section does not require restarts");
        PlayerWalkSpeed = PlayerConfigFile.Bind($"Speed", "Walk Speed", 7.5f, "Set your walk speed");
        PlayerRunSpeed = PlayerConfigFile.Bind($"Speed", "Run Speed", 15f, "Set your run speed");
        PlayerRunSpeedModifier = PlayerConfigFile.Bind($"Speed", "Run Speed Modifier", 1f, "Multiplies your run speed by this value");

        Harmony Harmony = new Harmony($"{PluginGUID}");
        Harmony.PatchAll(typeof(InvItemClassPatch));
        Harmony.PatchAll(typeof(InventoryPatch));
        Harmony.PatchAll(typeof(CharacterPatch));
        Harmony.PatchAll(typeof(PlayerPatch));

        LogDivider();
        Log.LogInfo($"Plugin {PluginGUID} v{PluginVersion} is loaded!");
        LogDivider();

        fileWatcher = new FileSystemWatcher(Paths.ConfigPath, PluginGUID + "*.cfg");
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnFileChanged;
        fileWatcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        LogDivider();
        Log.LogInfo($"Reloaded configuration file.");
        LogDivider();
        switch (e.Name)
        {
            case PluginGUID + ".cfg":
                ConfigFile.Reload();
                break;
            case PluginGUID + ".Inventories.cfg":
                InventoriesConfigFile.Reload();
                break;
            case PluginGUID + ".Lantern.cfg":
                InvItemClassPatch.RefreshLantern = true;
                LanternConfigFile.Reload();
                break;
            case PluginGUID + ".Player.cfg":
                PlayerConfigFile.Reload();
                PlayerPatch.RefreshPlayer = true;
                break;
            case PluginGUID + ".Stacks.cfg":
                StacksConfigFile.Reload();
                break;
            default:
                // Handle unexpected file changes (if necessary)
                break;
        }
    }

    public static void LogDivider()
    {
        Log.LogInfo("");
        Log.LogInfo("--------------------------------------------------------------------------------");
        Log.LogInfo("");
    }
}
