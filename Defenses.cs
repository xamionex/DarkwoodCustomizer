using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal static class DefensesPatch
{
  private class RechargeRecord
  {
    public Trigger Trap;
    public float ReadyTime;
    public string ArmedSprite;
  }

  private class MushroomRespawnRecord
  {
    public string Location;
    public Vector3 Position;
    public Quaternion Rotation;
    public int ConsumedAt;
  }

  private class LootRespawnRecord
  {
    public string Location;
    public string Prefab;
    public Vector3 Position;
    public Quaternion Rotation;
    public int ConsumedAt;
  }

  private static readonly List<RechargeRecord> RechargeQueue = new();
  private static readonly Dictionary<Window, int> WindowOriginalMax = new();
  private static readonly Dictionary<Door, int> DoorOriginalMax = new();
  private static readonly List<MushroomRespawnRecord> MushroomRespawnQueue = new();
  private static readonly List<LootRespawnRecord> LootRespawnQueue = new();
  private static float _lastBarricadeHealTime = -9999f;

  // ===== Discrimination =====

  // Returns true only for actual placeable trap items (bear trap, chain trap, mutated trap).
  // Excludes mushrooms and other world items that share the isBearTrap flag.
  private static bool IsTrapType(Trigger trigger)
  {
    var item = trigger.GetComponent<Item>();
    if (item == null || item.invItem == null) return false;
    var type = item.invItem.type;
    if (type.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0) return false;
    if (trigger.isBearTrap) return type.Equals("beartrap", StringComparison.OrdinalIgnoreCase);
    if (trigger.isChainTrap) return type.Equals("chaintrap", StringComparison.OrdinalIgnoreCase);
    if (trigger.isMutatedTrap) return true;
    return false;
  }

  // ===== Public tick methods (called from Plugin.FixedUpdate) =====

  public static void FixedUpdateTick()
  {
    if (!Plugin.DefensesModification.Value) return;
    TickRecharge();
    TickBarricadeHealing();
  }

  public static void RespawnTick()
  {
    if (!Plugin.MushroomRespawn.Value && !Plugin.LootRespawn.Value) return;
    if (Player.Instance == null || Singleton<Controller>.Instance == null) return;
    var bigLocation = Player.Instance.whereAmI?.bigLocation;
    if (bigLocation == null) return;
    var locationName = bigLocation.name;
    var currentTime = Singleton<Controller>.Instance.totalTime;

    if (Plugin.MushroomRespawn.Value && !Plugin.MushroomRespawnPerDay.Value)
      TickMushroomRespawn(locationName, currentTime, bigLocation.nightMushroom);
    if (Plugin.LootRespawn.Value && !Plugin.LootRespawnPerDay.Value)
      TickLootRespawn(locationName, currentTime);
  }

  // Barricade Health Modification
  [HarmonyPatch(typeof(Window), nameof(Window.barricade))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static void WindowBarricadePrefix(Window __instance)
  {
    if (!Plugin.DefensesModification.Value || !Plugin.BarricadeHealthModification.Value) return;
    if (Mathf.Approximately(Plugin.BarricadeHealthMultiplier.Value, 1f)) return;
    if (!WindowOriginalMax.ContainsKey(__instance))
      WindowOriginalMax[__instance] = __instance.barricadeMaxHealth;
    var newMax = Mathf.Max(1, Mathf.RoundToInt(WindowOriginalMax[__instance] * Plugin.BarricadeHealthMultiplier.Value));
    if (newMax == __instance.barricadeMaxHealth) return;
    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] Window '{__instance.name}' barricade max health: {__instance.barricadeMaxHealth} -> {newMax}");
    __instance.barricadeMaxHealth = newMax;
  }

  [HarmonyPatch(typeof(Door), nameof(Door.init))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static void DoorInitPrefix(Door __instance)
  {
    if (!Plugin.DefensesModification.Value || !Plugin.BarricadeHealthModification.Value) return;
    if (Mathf.Approximately(Plugin.BarricadeHealthMultiplier.Value, 1f)) return;
    if (!DoorOriginalMax.ContainsKey(__instance))
      DoorOriginalMax[__instance] = __instance.maxBarricadeHealth;
    var newMax = Mathf.Max(1, Mathf.RoundToInt(DoorOriginalMax[__instance] * Plugin.BarricadeHealthMultiplier.Value));
    if (newMax == __instance.maxBarricadeHealth) return;
    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] Door '{__instance.name}' barricade max health: {__instance.maxBarricadeHealth} -> {newMax}");
    __instance.maxBarricadeHealth = newMax;
  }

  [HarmonyPatch(typeof(Door), nameof(Door.barricade))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static void DoorBarricadePrefix(Door __instance)
  {
    if (!Plugin.DefensesModification.Value || !Plugin.BarricadeHealthModification.Value) return;
    if (Mathf.Approximately(Plugin.BarricadeHealthMultiplier.Value, 1f)) return;
    if (!DoorOriginalMax.ContainsKey(__instance))
      DoorOriginalMax[__instance] = __instance.maxBarricadeHealth;
    var newMax = Mathf.Max(1, Mathf.RoundToInt(DoorOriginalMax[__instance] * Plugin.BarricadeHealthMultiplier.Value));
    if (newMax == __instance.maxBarricadeHealth) return;
    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] Door '{__instance.name}' barricade max health: {__instance.maxBarricadeHealth} -> {newMax}");
    __instance.maxBarricadeHealth = newMax;
  }

  // Barricade Healing
  private static void TickBarricadeHealing()
  {
    if (!Plugin.BarricadeHealing.Value) return;
    if (Time.time - _lastBarricadeHealTime < Plugin.BarricadeHealInterval.Value) return;
    _lastBarricadeHealTime = Time.time;
    var percent = Plugin.BarricadeHealPercent.Value;
    if (percent <= 0f) return;
    var healed = 0;

    var windows = UnityEngine.Object.FindObjectsOfType<Window>();
    foreach (var window in windows)
    {
      if (!window.barricaded || window.barricadeHealth >= window.barricadeMaxHealth) continue;
      var amount = Mathf.Max(1, Mathf.RoundToInt(window.barricadeMaxHealth * percent / 100f));
      var before = window.barricadeHealth;
      window.barricadeHealth = Mathf.Min(window.barricadeMaxHealth, window.barricadeHealth + amount);
      if (window.barricadeHealth != before) healed++;
    }

    var doors = UnityEngine.Object.FindObjectsOfType<Door>();
    foreach (var door in doors)
    {
      if (!door.barricaded || door.barricadeHealth >= door.maxBarricadeHealth) continue;
      var amount = Mathf.Max(1, Mathf.RoundToInt(door.maxBarricadeHealth * percent / 100f));
      var before = door.barricadeHealth;
      door.barricadeHealth = Mathf.Min(door.maxBarricadeHealth, door.barricadeHealth + amount);
      if (door.barricadeHealth != before) healed++;
    }

    if (Plugin.DefensesLogging.Value && healed > 0)
      Plugin.Log.LogInfo($"[Defenses] Barricade healing tick: healed {healed} barricades for {percent}% of their max health");
  }

  // Only Player Can Damage Barricades
  [HarmonyPatch(typeof(MeleeSensor), "OnTriggerEnter")]
  [HarmonyPrefix]
  // ReSharper disable InconsistentNaming
  private static bool MeleeSensorOnTriggerEnter(MeleeSensor __instance, Collider _collider)
  // ReSharper restore InconsistentNaming
  {
    if (!Plugin.DefensesModification.Value || !Plugin.OnlyPlayerCanDamageBarricades.Value) return true;
    if (__instance.type != MeleeSensor.MeleeSensorType.character) return true;
    if (__instance.barricadeDamage <= 0) return true;
    if (_collider == null) return true;

    var go = _collider.gameObject;
    if (go.CompareTag("Door"))
    {
      var door = Door.getDoorScript(go.transform.parent);
      if (door != null && door.barricaded)
      {
        if (Plugin.DefensesLogging.Value)
          Plugin.Log.LogInfo($"[Defenses] Blocked enemy barricade damage on door '{door.name}' from '{__instance.attackerTransform?.name ?? "unknown"}'");
        return false;
      }
    }

    var window = go.GetComponent<Window>();
    if (window == null && _collider.attachedRigidbody != null)
      window = _collider.attachedRigidbody.GetComponent<Window>();
    if (window != null && window.barricaded)
    {
      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogInfo($"[Defenses] Blocked enemy barricade damage on window '{window.name}' from '{__instance.attackerTransform?.name ?? "unknown"}'");
      return false;
    }

    return true;
  }

  // Trap Damage
  [HarmonyPatch(typeof(Trigger), nameof(Trigger.checkCollision))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static void TrapDamagePrefix(Trigger __instance)
  {
    if (!Plugin.DefensesModification.Value) return;
    if (!__instance.active) return;
    if (!IsTrapType(__instance)) return;
    var item = __instance.GetComponent<Item>();
    if (item == null) return;

    int newDamage;
    string trapType;

    if (__instance.isBearTrap || __instance.isMutatedTrap)
    {
      if (!Plugin.BearTrapDamageModification.Value) return;
      newDamage = Plugin.BearTrapDamage.Value;
      trapType = __instance.isMutatedTrap ? "MutatedTrap" : "BearTrap";
    }
    else if (__instance.isChainTrap)
    {
      if (!Plugin.ChainTrapDamageModification.Value) return;
      newDamage = Plugin.ChainTrapDamage.Value;
      trapType = "ChainTrap";
    }
    else return;

    if (item.damage == newDamage) return;
    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] {trapType} '{__instance.name}' damage: {item.damage} -> {newDamage}");
    item.damage = newDamage;
  }

  // Trap Auto Recharge
  [HarmonyPatch(typeof(Trigger), "OnAfterTrigger")]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static bool TrapRechargePrefix(Trigger __instance, Collider other, bool doConnectChain)
  {
    if (!Plugin.DefensesModification.Value) return true;
    if (!IsTrapType(__instance)) return true;
    var rechargeTime = GetRechargeTime(__instance);
    if (rechargeTime <= 0f) return true;

    var sprite = __instance.GetComponent<tk2dBaseSprite>();
    if (sprite == null) sprite = __instance.GetComponentInChildren<tk2dBaseSprite>();
    var armedSprite = sprite != null ? sprite.CurrentSprite.name : "";

    if (!__instance.loadedFromSave)
    {
      if (!string.IsNullOrEmpty(__instance.activateSound))
        AudioController.Play(__instance.activateSound, __instance.transform);
      if (__instance.prefabToSpawn != null)
      {
        var spawn = Core.AddPrefab(__instance.prefabToSpawn, __instance.transform.position + new Vector3(0f, 1f, 0f), Quaternion.Euler(90f, 0f, 0f), null);
        if (spawn != null && __instance.isChainTrap && doConnectChain)
        {
          var chainParent = spawn.GetComponent<ChainParent>();
          if (chainParent != null && other != null)
            chainParent.target = other.gameObject;
        }
      }
      if (__instance.alertRadius > 0f)
        Character.alertInArea(__instance.transform.position, __instance.alertRadius, false, 1f);
    }

    var item = __instance.GetComponent<Item>();
    if (item != null) item.onTriggerFire();

    SetTriggeredVisual(__instance);
    __instance.triggered = true;
    __instance.active = false;
    __instance.canDisarm = false;

    RechargeQueue.Add(new RechargeRecord { Trap = __instance, ReadyTime = Time.time + rechargeTime, ArmedSprite = armedSprite });

    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] Trap '{__instance.name}' triggered, will recharge in {rechargeTime} seconds");

    return false;
  }

  private static void TickRecharge()
  {
    if (RechargeQueue.Count == 0) return;
    for (var i = RechargeQueue.Count - 1; i >= 0; i--)
    {
      var record = RechargeQueue[i];
      var trap = record.Trap;
      if (ReferenceEquals(trap?.gameObject, null))
      {
        RechargeQueue.RemoveAt(i);
        continue;
      }
      if (Time.time < record.ReadyTime) continue;
      RechargeQueue.RemoveAt(i);

      trap.active = true;
      trap.canDisarm = true;
      trap.triggered = false;

      var sprite = trap.GetComponent<tk2dBaseSprite>();
      if (!sprite) sprite = trap.GetComponentInChildren<tk2dBaseSprite>();
      if (sprite && !string.IsNullOrEmpty(record.ArmedSprite))
        sprite.SetSprite(record.ArmedSprite);

      trap.checkCollisions();

      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogInfo($"[Defenses] Trap '{trap.name}' recharged and is active again");
    }
  }

  private static float GetRechargeTime(Trigger trigger)
  {
    if (!IsTrapType(trigger)) return 0f;
    if (trigger.isChainTrap && Plugin.ChainTrapAutoRecharge.Value)
      return Mathf.Max(0f, Plugin.ChainTrapRechargeTime.Value);
    if ((trigger.isBearTrap || trigger.isMutatedTrap) && Plugin.BearTrapAutoRecharge.Value)
      return Mathf.Max(0f, Plugin.BearTrapRechargeTime.Value);
    return 0f;
  }

  private static void SetTriggeredVisual(Trigger trigger)
  {
    var sprite = trigger.GetComponent<tk2dBaseSprite>();
    var animator = trigger.GetComponent<tk2dSpriteAnimator>();
    if (sprite == null) sprite = trigger.GetComponentInChildren<tk2dBaseSprite>();
    if (animator == null) animator = trigger.GetComponentInChildren<tk2dSpriteAnimator>();
    if (string.IsNullOrEmpty(trigger.triggeredState) || sprite == null) return;

    if (trigger.triggeredStateAdditive)
    {
      if (trigger.triggeredStateFromAnim && animator != null)
        sprite.SetSprite(animator.CurrentOrDefaultClip.name + trigger.triggeredState);
      else
        sprite.SetSprite(sprite.CurrentSprite.name + trigger.triggeredState);
    }
    else
    {
      if (trigger.triggeredStateFromAnim && animator != null)
        sprite.SetSprite(animator.CurrentOrDefaultClip.name);
      else
        sprite.SetSprite(trigger.triggeredState);
    }
  }

  // Mushroom Respawn, Stepping on a mushroom consumes it via OnAfterTrigger.
  [HarmonyPatch(typeof(Trigger), "OnAfterTrigger")]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void OnAfterTriggerPostfix(Trigger __instance)
  {
    if (!Plugin.MushroomRespawn.Value) return;
    if (__instance.staysAfterTriggering) return;
    if (!__instance.isBearTrap && !__instance.isMutatedTrap) return;
    if (IsTrapType(__instance)) return;
    if (Player.Instance == null || Player.Instance.whereAmI?.bigLocation == null) return;
    if (Singleton<Controller>.Instance == null) return;

    MushroomRespawnQueue.Add(new MushroomRespawnRecord
    {
      Location = Player.Instance.whereAmI.bigLocation.name,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      ConsumedAt = Singleton<Controller>.Instance.totalTime
    });
    SaveRespawnState();
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Mushroom at {__instance.transform.position} consumed, scheduled for respawn");
  }

  // Harvesting a mushroom consumes it via Item.switchTriggerState.
  [HarmonyPatch(typeof(Item), nameof(Item.switchTriggerState))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void SwitchTriggerStatePostfix(Item __instance)
  {
    if (!Plugin.MushroomRespawn.Value) return;
    var trigger = __instance.GetComponent<Trigger>();
    if (trigger == null) return;
    if (trigger.staysAfterDisarming) return;
    if (!trigger.isBearTrap && !trigger.isMutatedTrap) return;
    if (IsTrapType(trigger)) return;
    if (Player.Instance == null || Player.Instance.whereAmI?.bigLocation == null) return;
    if (Singleton<Controller>.Instance == null) return;

    MushroomRespawnQueue.Add(new MushroomRespawnRecord
    {
      Location = Player.Instance.whereAmI.bigLocation.name,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      ConsumedAt = Singleton<Controller>.Instance.totalTime
    });
    SaveRespawnState();
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Mushroom at {__instance.transform.position} harvested, scheduled for respawn");
  }

  // Loot Respawn
  [HarmonyPatch(typeof(Inventory), nameof(Inventory.hide))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void InventoryHidePostfix(Inventory __instance)
  {
    if (!Plugin.LootRespawn.Value) return;
    if (__instance.invType != Inventory.InvType.itemInv) return;
    if (!__instance.removeWhenEmpty) return;
    if (__instance.getAllItems().Count > 0) return;
    if (Player.Instance == null || Player.Instance.whereAmI?.bigLocation == null) return;
    if (Singleton<Controller>.Instance == null) return;

    var prefabName = __instance.gameObject.name;
    if (prefabName.EndsWith("(Clone)")) prefabName = prefabName.Substring(0, prefabName.Length - 7);

    LootRespawnQueue.Add(new LootRespawnRecord
    {
      Location = Player.Instance.whereAmI.bigLocation.name,
      Prefab = prefabName,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      ConsumedAt = Singleton<Controller>.Instance.totalTime
    });
    SaveRespawnState();
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Container '{prefabName}' emptied at {__instance.transform.position}, scheduled for respawn");
  }

  // Per-day respawns
  [HarmonyPatch(typeof(Controller), nameof(Controller.startDay))]
  [HarmonyPostfix]
  private static void ControllerStartDayPostfix()
  {
    if (!Plugin.MushroomRespawn.Value && !Plugin.LootRespawn.Value) return;
    if (Player.Instance == null || Player.Instance.whereAmI?.bigLocation == null) return;
    var bigLocation = Player.Instance.whereAmI.bigLocation;
    var locationName = bigLocation.name;
    var changed = false;

    if (Plugin.MushroomRespawn.Value && Plugin.MushroomRespawnPerDay.Value && bigLocation.nightMushroom != null)
    {
      for (var i = MushroomRespawnQueue.Count - 1; i >= 0; i--)
      {
        if (MushroomRespawnQueue[i].Location != locationName) continue;
        SpawnPrefab(bigLocation.nightMushroom, Core.getYPos(MushroomRespawnQueue[i].Position, PosType.items2), MushroomRespawnQueue[i].Rotation, "Mushroom");
        MushroomRespawnQueue.RemoveAt(i);
        changed = true;
      }
    }

    if (Plugin.LootRespawn.Value && Plugin.LootRespawnPerDay.Value)
    {
      for (var i = LootRespawnQueue.Count - 1; i >= 0; i--)
      {
        if (LootRespawnQueue[i].Location != locationName) continue;
        var prefab = Singleton<SaveManager>.Instance?.getPrefab(LootRespawnQueue[i].Prefab);
        if (prefab != null)
          SpawnPrefab(prefab, LootRespawnQueue[i].Position, LootRespawnQueue[i].Rotation, "Container");
        LootRespawnQueue.RemoveAt(i);
        changed = true;
      }
    }

    if (!changed) return;
    SaveRespawnState();
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo("[Loot] Per-day respawn completed");
  }

  // Timer-based respawns
  private static void TickMushroomRespawn(string locationName, int currentTime, UnityEngine.Object nightMushroom)
  {
    if (nightMushroom == null) return;
    var respawnTime = Plugin.MushroomRespawnTime.Value;
    var changed = false;

    for (var i = MushroomRespawnQueue.Count - 1; i >= 0; i--)
    {
      var r = MushroomRespawnQueue[i];
      if (r.Location != locationName) continue;
      if (currentTime - r.ConsumedAt < respawnTime) continue;
      SpawnPrefab(nightMushroom, Core.getYPos(r.Position, PosType.items2), r.Rotation, "Mushroom");
      MushroomRespawnQueue.RemoveAt(i);
      changed = true;
    }

    if (changed) SaveRespawnState();
  }

  private static void TickLootRespawn(string locationName, int currentTime)
  {
    var respawnTime = Plugin.LootRespawnTime.Value;
    var changed = false;

    for (var i = LootRespawnQueue.Count - 1; i >= 0; i--)
    {
      var r = LootRespawnQueue[i];
      if (r.Location != locationName) continue;
      if (currentTime - r.ConsumedAt < respawnTime) continue;
      var prefab = Singleton<SaveManager>.Instance?.getPrefab(r.Prefab);
      if (prefab != null)
        SpawnPrefab(prefab, r.Position, r.Rotation, "Container");
      LootRespawnQueue.RemoveAt(i);
      changed = true;
    }

    if (changed) SaveRespawnState();
  }

  private static void SpawnPrefab(UnityEngine.Object prefab, Vector3 position, Quaternion rotation, string label)
  {
    var go = Core.AddPrefab(prefab, position, rotation, null, true);
    if (go == null) return;
    Core.addToSaveable(go, true, true);
    if (Singleton<WorldGrid>.Instance)
      Singleton<WorldGrid>.Instance.registerToNode(go);
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] {label} respawned at {position}");
  }

  // ===== Persistence =====

  [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.Save))]
  [HarmonyPostfix]
  private static void SaveManagerSavePostfix()
  {
    SaveRespawnState();
  }

  [HarmonyPatch(typeof(WorldGenerator), "onFinished")]
  [HarmonyPostfix]
  private static void WorldGeneratorOnFinishedPostfix()
  {
    LoadRespawnState();
    CleanupStuckMushrooms();
  }

  private static string RespawnStatePath
  {
    get
    {
      if (Core.currentProfile == null) return null;
      var dir = Singleton<SaveManager>.Instance.baseSaveDirectory + "/prof" + Core.currentProfile.id;
      return dir + "/DarkwoodCustomizerRespawns.json";
    }
  }

  private static void SaveRespawnState()
  {
    var path = RespawnStatePath;
    if (path == null) return;
    try
    {
      var root = new JObject();

      var mushrooms = new JArray();
      foreach (var r in MushroomRespawnQueue)
      {
        mushrooms.Add(new JObject
        {
          ["location"] = r.Location,
          ["x"] = r.Position.x, ["y"] = r.Position.y, ["z"] = r.Position.z,
          ["rx"] = r.Rotation.x, ["ry"] = r.Rotation.y, ["rz"] = r.Rotation.z, ["rw"] = r.Rotation.w,
          ["consumedAt"] = r.ConsumedAt
        });
      }
      root["mushrooms"] = mushrooms;

      var loot = new JArray();
      foreach (var r in LootRespawnQueue)
      {
        loot.Add(new JObject
        {
          ["location"] = r.Location,
          ["prefab"] = r.Prefab,
          ["x"] = r.Position.x, ["y"] = r.Position.y, ["z"] = r.Position.z,
          ["rx"] = r.Rotation.x, ["ry"] = r.Rotation.y, ["rz"] = r.Rotation.z, ["rw"] = r.Rotation.w,
          ["consumedAt"] = r.ConsumedAt
        });
      }
      root["loot"] = loot;

      var dir = Path.GetDirectoryName(path);
      if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);
      File.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented));
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"[Loot] Failed to save respawn state: {e.Message}");
    }
  }

  private static void LoadRespawnState()
  {
    MushroomRespawnQueue.Clear();
    LootRespawnQueue.Clear();

    var path = RespawnStatePath;
    if (path == null || !File.Exists(path)) return;

    try
    {
      var root = JObject.Parse(File.ReadAllText(path));

      if (root["mushrooms"] is JArray mushrooms)
      {
        foreach (var t in mushrooms)
        {
          MushroomRespawnQueue.Add(new MushroomRespawnRecord
          {
            Location = t["location"]?.Value<string>() ?? "",
            Position = new Vector3(t["x"]?.Value<float>() ?? 0f, t["y"]?.Value<float>() ?? 0f, t["z"]?.Value<float>() ?? 0f),
            Rotation = new Quaternion(t["rx"]?.Value<float>() ?? 0f, t["ry"]?.Value<float>() ?? 0f, t["rz"]?.Value<float>() ?? 0f, t["rw"]?.Value<float>() ?? 1f),
            ConsumedAt = t["consumedAt"]?.Value<int>() ?? 0
          });
        }
      }

      if (root["loot"] is JArray loot)
      {
        foreach (var t in loot)
        {
          LootRespawnQueue.Add(new LootRespawnRecord
          {
            Location = t["location"]?.Value<string>() ?? "",
            Prefab = t["prefab"]?.Value<string>() ?? "",
            Position = new Vector3(t["x"]?.Value<float>() ?? 0f, t["y"]?.Value<float>() ?? 0f, t["z"]?.Value<float>() ?? 0f),
            Rotation = new Quaternion(t["rx"]?.Value<float>() ?? 0f, t["ry"]?.Value<float>() ?? 0f, t["rz"]?.Value<float>() ?? 0f, t["rw"]?.Value<float>() ?? 1f),
            ConsumedAt = t["consumedAt"]?.Value<int>() ?? 0
          });
        }
      }

      if (Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Loaded respawn state: {MushroomRespawnQueue.Count} mushrooms, {LootRespawnQueue.Count} containers");
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"[Loot] Failed to load respawn state: {e.Message}");
    }
  }

  // Clean up mushrooms stuck in the triggered state from the old trap-recharge bug.
  private static void CleanupStuckMushrooms()
  {
    try
    {
      var cleaned = 0;
      foreach (var trigger in UnityEngine.Object.FindObjectsOfType<Trigger>())
      {
        if (trigger == null) continue;
        if (!trigger.isBearTrap && !trigger.isMutatedTrap) continue;
        if (IsTrapType(trigger)) continue;
        if (!trigger.triggered || trigger.active || trigger.canDisarm) continue;
        if (Plugin.LogDebug.Value)
          Plugin.Log.LogInfo($"[Loot] Cleaning up stuck mushroom at {trigger.transform.position}");
        UnityEngine.Object.Destroy(trigger.gameObject);
        cleaned++;
      }
      if (cleaned > 0 && Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Cleaned up {cleaned} stuck mushrooms");
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"[Loot] Failed to clean up stuck mushrooms: {e.Message}");
    }
  }
}