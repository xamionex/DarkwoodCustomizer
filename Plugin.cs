using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;

namespace DarkwoodCustomizer;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInProcess("Darkwood.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static PluginInfo pluginInfo;
    public static ConfigFile ConfigFile;
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "amione";
    public const string PluginName = "DarkwoodCustomizer";
    public const string PluginVersion = "1.0.4";
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;

    // StackResize values
    public static ConfigEntry<int> StackResize;
    public static ConfigEntry<bool> ChangeStacks;
    public static ConfigEntry<bool> RepairLantern;
    public static ConfigEntry<string> LanternRepairConfig;
    public static ConfigEntry<int> LanternAmountRepairConfig;
    public static ConfigEntry<float> LanternDurabilityRepairConfig;
    public static ConfigEntry<bool> LogItems;

    // InventoryResize values
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

    private void Awake()
    {
        pluginInfo = Info;
        Log = Logger;
        ConfigFile = new ConfigFile(Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);

        // StackResize config
        StackResize = ConfigFile.Bind($"{PluginName}", "Stack Resize", 50, "Number for all item stack sizes to be set to. Requires reload of save (Return to Menu > Load Save)");
        ChangeStacks = ConfigFile.Bind($"{PluginName}", "Change Stack sizes", true, "Whether or not stack sizes will be changed by the mod (true/false). Requires reload of save (Return to Menu > Load Save)");
        RepairLantern = ConfigFile.Bind($"{PluginName}", "Repairable Lantern", true, "Whether or not lantern can be repaired using gasoline on the workbench (true/false)");
        LanternRepairConfig = ConfigFile.Bind($"{PluginName}", "Lantern Repair Item", "gasoline", "What item will be used for repairing the lantern. (recommended: gasoline or molotov)");
        LanternAmountRepairConfig = ConfigFile.Bind($"{PluginName}", "Lantern Amount of Item Used", 1, "Item amount of item to use? Ex. 1 molotov to repair");
        LanternDurabilityRepairConfig = ConfigFile.Bind($"{PluginName}", "Lantern Durability of Item Used", 0.2f, "Item durability amount to use? Ex. 0.2 of a gasoline to repair");
        LogItems = ConfigFile.Bind($"{PluginName}", "Log Items", false, "Whether or not to log every item (true/false)");

        // InventoryResize config
        WorkbenchCraftingOffset = ConfigFile.Bind($"{PluginName}", "Workbench Crafting Offset", 1000f, "Pixels offset for the workbench crafting window, no longer requires restart, 1550 is the almost the edge of the screen on fullhd which looks nice");
        RightSlots = ConfigFile.Bind($"{PluginName}", "Storage Right Slots", 12, "Number that determines slots in workbench to the right");
        DownSlots = ConfigFile.Bind($"{PluginName}", "Storage Down Slots", 9, "Number that determines slots in workbench downward");
        InventoryRightSlots = ConfigFile.Bind($"{PluginName}", "Inventory Right Slots", 2, "Number that determines slots in inventory to the right");
        InventoryDownSlots = ConfigFile.Bind($"{PluginName}", "Inventory Down Slots", 9, "Number that determines slots in inventory downward");
        InventorySlots = ConfigFile.Bind($"{PluginName}", "Enable Inventory Changing", false, "This will circumvent the inventory progression and enables the 2 settings above this one to take effect, disable to return to default Inventory slots");
        HotbarRightSlots = ConfigFile.Bind($"{PluginName}", "Hotbar Right Slots", 1, "Number that determines slots in Hotbar to the right, requires reload of save (Return to Menu > Load Save)");
        HotbarDownSlots = ConfigFile.Bind($"{PluginName}", "Hotbar Down Slots", 6, "Number that determines slots in Hotbar downward, requires reload of save (Return to Menu > Load Save)");
        HotbarSlots = ConfigFile.Bind($"{PluginName}", "Enable Hotbar Changing", false, "This will circumvent the Hotbar progression and enables the 2 settings above this one to take effect, disable to return to default Hotbar slots");
        RemoveExcess = ConfigFile.Bind($"{PluginName}", "Remove Excess Slots", true, "Whether or not to remove slots that are outside the inventory you set. For example, you set your inventory to 9x9 (81 slots) but you had a previous mod do something bigger and you have something like 128 slots extra enabling this option will remove those excess slots and bring it down to 9x9 (81)");

        Harmony Harmony = new Harmony($"{PluginGUID}");
        Harmony.PatchAll(typeof(InvItemClassPatch));
        Harmony.PatchAll(typeof(InventoryPatch));
        Log.LogInfo($"Plugin {PluginGUID} v{PluginVersion} is loaded!");

        fileWatcher = new FileSystemWatcher(Paths.ConfigPath, PluginGUID + ".cfg");
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnFileChanged;
        ConfigFile.ConfigReloaded += OnConfigReloaded;
        fileWatcher.EnableRaisingEvents = true;
    }

    private void OnConfigReloaded(object sender, EventArgs e)
    {
        Log.LogInfo($"Reloaded configuration file");
        InvItemClassPatch.ConfigReloaded = true;
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        ConfigFile.Reload();
    }
}
