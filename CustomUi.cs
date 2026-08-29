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

  private static readonly List<string> Sections = [];
  private static int _tab;
  private static Vector2 _tabScroll;
  private static readonly Dictionary<string, string> EditBuffers = new();

  private static string _itemSearch = "";
  private static string _itemName = "";
  private static string _itemAmount = "1";
  private static Vector2 _itemScroll;
  private static string[] _allItems = [];

  private static string _enemyName = "";
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

  private static ConfigEntryBase _editingHotkey;
  private static HotkeyCapture _capture;
  private static bool _wasForbidInputs;
  // Stays true across window switches (F2 -> F3), only reset when the last window closes.
  // Without this, switching windows would re-capture the input state while it is already modified and closing would restore a frozen game.
  private static bool _sessionActive;

  private static string _status = "";
  private static float _statusUntil;
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
    Func<JToken> newKeyTemplate = null)
  {
    public readonly string Name = name;
    public readonly Func<string> Path = path;
    public readonly Func<JObject> Get = get;
    public readonly Action<JObject> Set = set;
    // Template for a new top level key, per file type
    public readonly Func<JToken> NewKeyTemplate = newKeyTemplate;
  }

  private static readonly DataFile[] DataFiles =
  [
    new("Custom Items", () => Plugin.CustomItemsPath, () => Plugin.CustomItems, o => Plugin.CustomItems = o,
      () => new JObject { { "name", "New Item" }, { "description", "" } }),
    new("Custom Crafting Recipes", () => Plugin.CustomCraftingRecipesPath, () => Plugin.CustomCraftingRecipes, o => Plugin.CustomCraftingRecipes = o,
      () => new JObject { { "requiredlevel", 1 }, { "resource", "" }, { "givesamount", 1 }, { "requirements", new JObject() } }),
    new("Custom Characters", () => Plugin.CustomCharactersPath, () => Plugin.CustomCharacters, o => Plugin.CustomCharacters = o,
      () => new JObject { { "Health", 100 }, { "WalkSpeed", 1 }, { "RunSpeed", 1 }, { "Attacks", new JArray() } }),
    new("Custom Character Effects", () => Plugin.CharacterEffectsPath, () => Plugin.CharacterEffects, o => Plugin.CharacterEffects = o,
      () => new JObject { { "duration", 0 }, { "modifier", 1 }, { "interval", 0 } }),
    new("Custom Random Inventories", () => Plugin.CustomRandomInventoriesPath, () => Plugin.CustomRandomInventories, o => Plugin.CustomRandomInventories = o,
      () => new JObject { { "presets", new JObject() } }),
    new("Custom Loot", () => Plugin.CustomLootPath, () => Plugin.CustomLoot, o => Plugin.CustomLoot = o,
      () => new JObject { { "enabled", false }, { "replace", false }, { "items", new JArray() } }),
  ];

  private static DataFile _selectedDataFile;
  private static Vector2 _itemKeysScroll;

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

  // Sound picker state for the Custom Character Effects editor
  private static string _soundPickerFor = "";
  private static string _soundPickerSearch = "";
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

  // Effect manager default: 610px wide, 15% taller than the standard menu
  private static Rect EffectManagerRect()
  {
    var height = Mathf.Max(Screen.height * 0.9f, 460f);
    return new Rect((Screen.width - 610f) / 2f, (Screen.height - height) / 2f, 610f, height);
  }

  private static Rect DefaultRectFor(int windowId)
  {
    switch (windowId)
    {
      case 9001:
      case 9002:
      case 9003:
        return MenuRect();
      case 9004:
        return EffectManagerRect();
      case 9005:
      {
        // Custom data files: 45% height, anchored top left, dynamic margins
        var top = Screen.height * 0.15f;
        return new Rect(60f, top, 320f, Screen.height * 0.45f);
      }
      case 9006:
      {
        // Custom data editor: fills the rest of the space from where the file window ends to the screen edge. The margins were 15% on all sides,
        var margin = 0.085f;
        var top = Screen.height * margin;
        var bottom = Screen.height * (1f - margin);
        var left = 60f + 320f + Screen.width * margin;
        var right = Screen.width * (1f - margin);
        return new Rect(left, top, Mathf.Max(right - left, 400f), Mathf.Max(bottom - top, 400f));
      }
      case 9007:
        return new Rect(80f, 80f, 570f, 480f);
      case 9008:
      case 9009:
      case 9010:
      {
        // Workspace layout: three inventory windows side by side, each taking
        // 45% of the screen height, centered as a row
        var h = Screen.height * 0.45f;
        var y = Screen.height * 0.05f;
        var w = 460f;
        var total = w * 3f + 48f;
        var x0 = (Screen.width - total) / 2f;
        var idx = windowId - 9008;
        return new Rect(x0 + idx * (w + 24f), y, w, h);
      }
      case 9011:
        // Wide quickpick bar at the bottom, 45% height, centered
        return new Rect((Screen.width - 1200f) / 2f, Screen.height * 0.52f, 1200f, Screen.height * 0.45f);
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
      // Keep the cursor visible for a few seconds after closing so it does
      // not pop out from under the player
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

      // Poll the real cursor while a resize or drag is active. Wine/Proton
      // drops MouseDrag events during slow movement, so the IMGUI event stream
      // cannot be trusted for tracking; the raw input position always can.
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
  private static Rect DrawWindow(int windowId, GUI.WindowFunction func, string title)
  {
    var rect = GetWindowRect(windowId);
    var result = GUI.Window(windowId, rect, func, title);
    if (_resizingWindow == windowId || _draggingWindow == windowId)
    {
      return rect;
    }
    return ClampWindowRect(result);
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

  // Same Wine/Proton workaround as UpdateResizeFromMouse: poll the real cursor
  // position every frame instead of relying on coalesced MouseDrag events.
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

    if (Sections.Count == 0)
    {
      foreach (var entry in GetEntries())
      {
        if (entry.Definition.Section == "!Mod" && entry.Definition.Key == "Version") continue;
        if (entry.Definition.Section == "Cheats") continue;
        if (!Sections.Contains(entry.Definition.Section))
        {
          Sections.Add(entry.Definition.Section);
        }
      }
      if (_tab >= Sections.Count) _tab = 0;
    }

    if (Sections.Count == 0)
    {
      GUILayout.Label("No config sections found");
      return;
    }

    if (_tab >= Sections.Count) _tab = 0;

    // All section tabs visible at once, no scrolling
    _tab = GUILayout.SelectionGrid(_tab, [.. Sections], 3);

    var section = Sections[_tab];
    _tabScroll = GUILayout.BeginScrollView(_tabScroll);
    var entries = GetEntries().Where(e => e.Definition.Section == section).ToList();
    foreach (var entry in entries)
    {
      DrawConfigEntry(entry);
    }
    GUILayout.EndScrollView();

    // Setting descriptions are shown in a fixed bar at the bottom instead of
    // hover tooltips. IMGUI hover tooltips rely on mouse move events that
    // Wine/Proton drops, so clicking the ? button is reliable everywhere.
    DrawInfoBar();
  }

  private static void DrawInfoBar()
  {
    GUILayout.Box("", GUILayout.Height(4f));
    _infoStyle ??= new GUIStyle(GUI.skin.box)
    {
      alignment = TextAnchor.UpperLeft,
      wordWrap = true,
      fontSize = 11,
    };
    if (string.IsNullOrEmpty(_infoText))
    {
      GUILayout.Box("Click the ? button next to any setting to see its description here.", _infoStyle, GUILayout.Height(64f));
    }
    else
    {
      GUILayout.Box(_infoText, _infoStyle, GUILayout.Height(64f));
    }
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
    GUILayout.Label(entry.Definition.Key, GUILayout.Width(200f));
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
        var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(120f));
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
        var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(120f));
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
        var newValue = GUILayout.TextField(value, GUILayout.Width(280f));
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
    GUILayout.EndHorizontal();
  }

  private static void DrawHotkeyEditor(ConfigEntryBase entry)
  {
    if (_editingHotkey == entry)
    {
      IsCapturingHotkey = true;
      // Drop text field focus so the key press is not consumed by an editor
      GUIUtility.keyboardControl = -1;
      var preview = _capture != null && _capture.Preview.Length > 0 ? " (" + _capture.Preview + ")" : "";
      GUILayout.Label("Press keys..." + preview + " (Esc cancels)", GUILayout.Width(220f));
    }
    else
    {
      GUILayout.Label(entry.GetSerializedValue(), GUILayout.Width(150f));
      if (!GUILayout.Button("Change", GUILayout.Width(70f))) return;
      _editingHotkey = entry;
      _capture = new HotkeyCapture();
      IsCapturingHotkey = true;
    }
  }

  // Polls the key capture every frame from Plugin.Update. Returns true when the
  // capture finished (committed or cancelled).
  public static bool PollHotkeyCapture()
  {
    if (_capture == null) return true;
    if (!_capture.Poll()) return false;
    _capture = null;
    return true;
  }

  private static readonly KeyCode[] AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

  // Keyboard keys only. KeyCode values below 323 are the keyboard range
  // (letters, numbers, function keys, modifiers), 323+ are mouse and joystick.
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

      // Prefer a real modifier regardless of seen order, so a same-frame
      // Ctrl+A is not inverted (Input.GetKey iteration is keycode-ordered)
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
    GUILayout.Label("Quick spawn (click a name)");
    // No fixed height: fills the remaining window space like the other windows
    _enemyScroll = GUILayout.BeginScrollView(_enemyScroll);
    const int cols = 3;
    for (var i = 0; i < _enemyNames.Length; i += cols)
    {
      GUILayout.BeginHorizontal();
      for (var j = 0; j < cols && i + j < _enemyNames.Length; j++)
      {
        var name = _enemyNames[i + j];
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
    GUILayout.Label("Effects are applied to the player. Duration 0 means permanent.", GUILayout.ExpandWidth(true));
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

  }

  private static void CustomDataFilesWindow(int id)
  {
    DrawWindowHeader();
    DrawStatus();

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
    PropBuffers.Clear();
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

    var data = _selectedDataFile.Get();

    GUILayout.BeginHorizontal();
    // Left: top level keys of the json file, clicking one edits its object
    GUILayout.BeginVertical(GUILayout.Width(280f));
    _dataKeysScroll = GUILayout.BeginScrollView(_dataKeysScroll);
    if (data != null)
    {
      // Left aligned buttons so long names are readable instead of clipped
      _leftButtonStyle ??= new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft };
      foreach (var prop in data.Properties())
      {
        if (prop.Name == _dataSelectedKey)
        {
          GUI.color = new Color(0.7f, 0.9f, 1f);
        }
        if (GUILayout.Button(prop.Name, _leftButtonStyle))
        {
          _dataSelectedKey = prop.Name;
          SyncPropBuffers(prop.Value);
        }
        GUI.color = Color.white;
      }
    }
    GUILayout.EndScrollView();
    GUILayout.BeginHorizontal();
    _dataNewKey = GUILayout.TextField(_dataNewKey, GUILayout.Width(210f));
    if (GUILayout.Button("Add", GUILayout.Width(55f)))
    {
      AddDataKey();
    }
    GUILayout.EndHorizontal();

    // For Custom Items, show a picker of all known item keys that are not in the selected item yet, so any property from the wiki can be added.
    // Takes 35% of the sidebar height.
    if (_selectedDataFile.Name == "Custom Items" && !string.IsNullOrEmpty(_dataSelectedKey)
                                                 && data?[_dataSelectedKey] is JObject itemObj)
    {
      GUILayout.Space(4f);
      GUILayout.Label("Available keys:", GUILayout.ExpandWidth(true));
      var editorHeight = GetWindowRect(9006).height;
      _itemKeysScroll = GUILayout.BeginScrollView(_itemKeysScroll, GUILayout.Height(Mathf.Max(editorHeight * 0.35f, 120f)));
      foreach (var key in ItemKeys)
      {
        if (itemObj.ContainsKey(key.Name)) continue;
        if (!GUILayout.Button(key.Name, _leftButtonStyle)) continue;
        itemObj[key.Name] = key.Default.DeepClone();
        SyncPropBuffers(itemObj);
        SetStatus("Added key " + key.Name);
      }
      GUILayout.EndScrollView();
    }
    GUILayout.EndVertical();

    // Right: editor for the selected key's properties
    GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
    GUILayout.Label(string.IsNullOrEmpty(_dataSelectedKey) ? "Select a key on the left" : "Editing: " + _dataSelectedKey, GUILayout.ExpandWidth(true));
    _dataPropsScroll = GUILayout.BeginScrollView(_dataPropsScroll);
    if (!string.IsNullOrEmpty(_dataSelectedKey) && data != null && data[_dataSelectedKey] is JObject obj)
    {
      // Sound picker takes priority, then the sub-picker, then the raw editor
      if (!DrawSoundPicker() && !DrawSubPicker())
      {
        DrawObjectEditor(obj);
      }
    }
    GUILayout.EndScrollView();
    if (!string.IsNullOrEmpty(_dataSelectedKey) && data != null && data[_dataSelectedKey] != null)
    {
      GUILayout.BeginHorizontal();
      if (GUILayout.Button("Delete Key", GUILayout.Width(100f)))
      {
        data.Remove(_dataSelectedKey);
        _dataSelectedKey = "";
        PropBuffers.Clear();
        SetStatus("Deleted key");
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndVertical();
    GUILayout.EndHorizontal();

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
      if (!PropBuffers.TryGetValue(prop.Name, out var buffer))
      {
        buffer = prop.Value.Type == JTokenType.String ? prop.Value.Value<string>() : prop.Value.ToString(Formatting.None);
        PropBuffers[prop.Name] = buffer;
      }
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
          // Nested structures are edited as compact json text, except the known item/attack lists which get a sub-picker
          if (IsSubPickerProperty(prop.Name))
          {
            if (GUILayout.Button("Edit...", GUILayout.Width(70f)))
            {
              _subPickerKey = _dataSelectedKey;
              _subPickerProp = prop.Name;
              _subPickerNewItem = "";
            }
          }
          else
          {
            var newBuffer = GUILayout.TextField(buffer, GUILayout.Width(260f));
            if (newBuffer != buffer)
            {
              PropBuffers[prop.Name] = newBuffer;
              try
              {
                var parsed = JToken.Parse(newBuffer);
                if (!JToken.DeepEquals(parsed, prop.Value)) prop.Value = parsed;
              }
              catch
              {
                // keep the old value while the user is typing
              }
            }
          }
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
          break;
        }
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
      var sounds = new List<string>();
      foreach (var category in controller.AudioCategories)
      {
        if (category?.AudioItems == null) continue;
        foreach (var audioItem in category.AudioItems)
        {
          if (audioItem != null && !string.IsNullOrEmpty(audioItem.Name) && !sounds.Contains(audioItem.Name))
          {
            sounds.Add(audioItem.Name);
          }
        }
      }
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

  // Which nested properties get a sub-picker instead of raw json text
  private static bool IsSubPickerProperty(string propName)
  {
    if (_selectedDataFile == null) return false;
    return _selectedDataFile.Name switch
    {
      "Custom Loot" => propName == "items",
      "Custom Random Inventories" => propName == "presets",
      "Custom Characters" => propName == "Attacks",
      "Custom Crafting Recipes" => propName == "requirements",
      _ => false
    };
  }

  // Draws the sub-picker for the current selection.
  // The picker stays open when switching items in the sidebar,
  // it re-targets to the newly selected key so the same property (items, presets, Attacks) is edited on the new item.
  private static bool DrawSubPicker()
  {
    if (string.IsNullOrEmpty(_subPickerKey) || string.IsNullOrEmpty(_subPickerProp) || _selectedDataFile == null) return false;
    var data = _selectedDataFile.Get();
    if (data == null) return false;

    // Re-target to the currently selected key, keeping the same property open
    var key = _subPickerKey;
    var propName = _subPickerProp;
    if (!string.IsNullOrEmpty(_dataSelectedKey) && _dataSelectedKey != key)
    {
      key = _dataSelectedKey;
    }
    if (data[key] is not JObject obj || obj[propName] == null)
    {
      // The new key does not have this property yet, create it empty
      if (data[key] is not JObject newObj)
      {
        data[key] = new JObject();
        newObj = (JObject)data[key];
      }
      if (propName is "items" or "Attacks")
      {
        newObj[propName] = new JArray();
      }
      else
      {
        newObj[propName] = new JObject();
      }
      obj = newObj;
    }

    GUILayout.BeginVertical("box");
    GUILayout.BeginHorizontal();
    GUILayout.Label("Editing " + key + " / " + propName, GUILayout.ExpandWidth(true));
    if (GUILayout.Button("Close", GUILayout.Width(60f)))
    {
      _subPickerKey = "";
      _subPickerProp = "";
      return false;
    }
    GUILayout.EndHorizontal();

    switch (_selectedDataFile.Name)
    {
      case "Custom Loot":
        DrawLootItemsPicker(obj[propName]);
        break;
      case "Custom Random Inventories":
        DrawRandomInvPresetsPicker(obj[propName]);
        break;
      case "Custom Characters":
        DrawAttacksPicker(obj[propName]);
        break;
      case "Custom Crafting Recipes":
        DrawRequirementsPicker(obj[propName]);
        break;
    }

    GUILayout.EndVertical();
    return true;
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
      GUILayout.Label("Min", GUILayout.Width(30f));
      var min = item["minAmount"]?.Value<int>() ?? 1;
      var newMin = GUILayout.TextField(min.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
      if (int.TryParse(newMin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMin) && parsedMin != min)
        item["minAmount"] = parsedMin;
      GUILayout.Label("Max", GUILayout.Width(30f));
      var max = item["maxAmount"]?.Value<int>() ?? 1;
      var newMax = GUILayout.TextField(max.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
      if (int.TryParse(newMax, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMax) && parsedMax != max)
        item["maxAmount"] = parsedMax;
      GUILayout.Label("Chance", GUILayout.Width(50f));
      var chance = item["chance"]?.Value<float>() ?? 1f;
      var newChance = GUILayout.TextField(chance.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
      if (float.TryParse(newChance, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedChance) && Math.Abs(parsedChance - chance) > 0.0001f)
        item["chance"] = parsedChance;
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
    foreach (var preset in presets.Properties())
    {
      GUILayout.Label("Preset " + preset.Name, GUILayout.ExpandWidth(true));
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
        GUILayout.Label("Max", GUILayout.Width(30f));
        var max = item["amountMax"]?.Value<int>() ?? 0;
        var newMax = GUILayout.TextField(max.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
        if (int.TryParse(newMax, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMax) && parsedMax != max)
          item["amountMax"] = parsedMax;
        GUILayout.Label("Chance", GUILayout.Width(50f));
        var chance = item["chance"]?.Value<float>() ?? 0f;
        var newChance = GUILayout.TextField(chance.ToString(CultureInfo.InvariantCulture), GUILayout.Width(50f));
        if (float.TryParse(newChance, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedChance) && Math.Abs(parsedChance - chance) > 0.0001f)
          item["chance"] = parsedChance;
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
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();
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
      GUILayout.Label("Barricade", GUILayout.Width(70f));
      var barricade = attack["BarricadeDamage"]?.Value<int>() ?? 0;
      var newBarricade = GUILayout.TextField(barricade.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60f));
      if (int.TryParse(newBarricade, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBarricade) && parsedBarricade != barricade)
        attack["BarricadeDamage"] = parsedBarricade;
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
      GUILayout.Label("Amount", GUILayout.Width(50f));
      var amount = req.Value.Type == JTokenType.Float ? req.Value.Value<float>() : req.Value.Value<int>();
      var newAmount = GUILayout.TextField(amount.ToString(CultureInfo.InvariantCulture), GUILayout.Width(60f));
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
    GUILayout.EndHorizontal();
  }

  private static void AddDataKey()
  {
    if (string.IsNullOrEmpty(_dataNewKey)) return;
    var data = _selectedDataFile.Get();
    if (data == null) return;
    if (data.ContainsKey(_dataNewKey))
    {
      SetStatus("Key already exists");
      return;
    }
    data[_dataNewKey] = _selectedDataFile.NewKeyTemplate != null ? _selectedDataFile.NewKeyTemplate() : new JObject();
    _dataSelectedKey = _dataNewKey;
    _dataNewKey = "";
    SyncPropBuffers(data[_dataSelectedKey]);
    SetStatus("Added key");
  }

  private static void SaveDataFile()
  {
    try
    {
      _selectedDataFile.Set(_selectedDataFile.Get());
      Plugin.SaveJsonFile(_selectedDataFile.Path(), _selectedDataFile.Get());
      SetStatus("Saved " + _selectedDataFile.Name);
    }
    catch (Exception e)
    {
      SetStatus("Error saving: " + e.Message);
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
      GUILayout.Label("Mod", GUILayout.Width(26f));
      inputs.Modifier = GUILayout.TextField(inputs.Modifier, GUILayout.Width(50f));
      GUILayout.Label("Int", GUILayout.Width(24f));
      inputs.Interval = GUILayout.TextField(inputs.Interval, GUILayout.Width(50f));

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
      GUILayout.EndHorizontal();
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
    }
  }

  private static void ApplyEffect(CharacterEffectType type, EffectInputs inputs)
  {
    try
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
      if (!ItemsDatabase.Instance.hasItem(type)) return "Unknown item: " + type;
      Player.Instance.Inventory.addItemTypeToPlayer(type, amount, true);
      Plugin.Log.LogInfo($"Giving item {type} x{amount} via custom UI");
      return $"Gave {amount}x {type}";
    }
    catch (Exception e)
    {
      Plugin.Log.LogError(e);
      return "Error: " + e.Message;
    }
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

  private static void QuickPickWindow(int id)
  {
    DrawWindowHeader();
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

    GUILayout.BeginHorizontal();
    GUILayout.Label("Search", GUILayout.Width(55f));
    _quickpickSearch = GUILayout.TextField(_quickpickSearch, GUILayout.Width(200f));
    GUILayout.FlexibleSpace();
    GUILayout.Label("Click an item to insert its ID into the selected slot editor", GUILayout.ExpandWidth(true));
    GUILayout.EndHorizontal();

    var filtered = string.IsNullOrEmpty(_quickpickSearch)
      ? _allItems
      : [.. _allItems.Where(x => x.IndexOf(_quickpickSearch, StringComparison.OrdinalIgnoreCase) >= 0)];

    _quickpickScroll = GUILayout.BeginScrollView(_quickpickScroll);
    const int cols = 6;
    for (var i = 0; i < filtered.Length; i += cols)
    {
      GUILayout.BeginHorizontal();
      for (var j = 0; j < cols && i + j < filtered.Length; j++)
      {
        var item = filtered[i + j];
        if (!GUILayout.Button(item)) continue;
        _invSlotItemName = item;
        SetStatus("Item ID set to " + item);
      }
      GUILayout.EndHorizontal();
    }
    GUILayout.EndScrollView();
  }
}
