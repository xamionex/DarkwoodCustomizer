using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.IO;

namespace StackResizer;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
[BepInProcess("Darkwood.exe")]
public class Plugin : BaseUnityPlugin
{
    internal static PluginInfo pluginInfo;
    public static ConfigFile ConfigFile;
    public const string PluginGUID = PluginAuthor + "." + PluginName;
    public const string PluginAuthor = "amione";
    public const string PluginName = "StackResizer";
    public const string PluginVersion = "1.0.2";
    public static ManualLogSource Log;
    public static FileSystemWatcher fileWatcher;
    public static ConfigEntry<int> StackResize;
    public static ConfigEntry<bool> RepairLantern;
    public static ConfigEntry<string> LanternRepairConfig;
    public static ConfigEntry<int> LanternAmountRepairConfig;
    public static ConfigEntry<float> LanternDurabilityRepairConfig;
    public static ConfigEntry<bool> LogItems;

    private void Awake()
    {
        pluginInfo = Info;
        Log = Logger;
        ConfigFile = new ConfigFile(System.IO.Path.Combine(Paths.ConfigPath, PluginGUID + ".cfg"), true);

        StackResize = ConfigFile.Bind($"{PluginName}", "Stack Resize", 50, "Number for all item stack sizes to be set to");
        RepairLantern = ConfigFile.Bind($"{PluginName}", "Repairable Lantern", true, "Whether or not lantern can be repaired using gasoline on the workbench (true/false)");
        LanternRepairConfig = ConfigFile.Bind($"{PluginName}", "Lantern Repair Item", "gasoline", "What item will be used for repairing the lantern. (recommended: gasoline or molotov)");
        LanternAmountRepairConfig = ConfigFile.Bind($"{PluginName}", "Lantern Amount of Item Used", 1, "Item amount of item to use? Ex. 1 molotov to repair");
        LanternDurabilityRepairConfig = ConfigFile.Bind($"{PluginName}", "Lantern Durability of Item Used", 0.2f, "Item durability amount to use? Ex. 0.2 of a gasoline to repair");
        LogItems = ConfigFile.Bind($"{PluginName}", "Log Items", false, "Whether or not to log every item (true/false)");

        Harmony Patch = new Harmony($"{PluginGUID}");
        Patch.PatchAll(typeof(InvItemClassPatch));
        Log.LogInfo($"Plugin {PluginGUID} v{PluginVersion} is loaded!");

        fileWatcher = new FileSystemWatcher(Paths.ConfigPath, PluginGUID + ".cfg");
        fileWatcher.NotifyFilter = NotifyFilters.LastWrite;
        fileWatcher.Changed += OnFileChanged;
        fileWatcher.EnableRaisingEvents = true;
    }
    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        ConfigFile.Reload();
        Log.LogInfo($"Reloaded configuration file");
    }
}
