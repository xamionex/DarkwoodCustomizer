using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal static class DefensesPatch
{
  // A trap that is waiting to become usable again.
  // Bear traps and mutated traps stay in the world, so their record points at the Trigger and remembers the armed sprite to switch back to.
  // Chain traps are removed by the game when they fire, so their record carries the item type to respawn instead.
  // ReadyTime is real seconds, matching the recharge settings; the record is written to the same per profile JSON as mushrooms and loot, so a pending recharge survives saving and loading.
  private class RechargeRecord
  {
    public Trigger Trap;
    public float ReadyTime;
    public string ArmedSprite;
    public UnityEngine.Object RespawnPrefab;
    public string ItemType;
    public Vector3 Position;
    public Quaternion Rotation;
    public bool SetByPlayer;
    public string Location;
    public int ObjectId = -1;
  }

  private class MushroomRespawnRecord
  {
    public string Location;
    public string Prefab;
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
    public int ObjectId = -1;
    public bool RemoveWhenEmpty;
    public float NextAttempt;
  }

  private static readonly List<RechargeRecord> RechargeQueue = new();
  private static readonly Dictionary<Window, int> WindowOriginalMax = new();
  private static readonly Dictionary<Door, int> DoorOriginalMax = new();
  private static readonly List<MushroomRespawnRecord> MushroomRespawnQueue = new();
  private static readonly List<LootRespawnRecord> LootRespawnQueue = new();
  private static float _lastBarricadeHealTime = -9999f;
  private static string _lastSwitchTriggerPrefab;

  // ===== Discrimination =====

  // Returns true only for actual placeable trap items (bear trap, chain trap, mutated trap).
  // Excludes mushrooms and other world items that share the isBearTrap flag.
  internal static bool IsTrapType(Trigger trigger)
  {
    var item = trigger.GetComponent<Item>();
    if (!item || !item.invItem) return false;
    var type = item.invItem.type;
    if (type.IndexOf("mushroom", StringComparison.OrdinalIgnoreCase) >= 0) return false;
    if (trigger.isBearTrap) return type.Equals("beartrap", StringComparison.OrdinalIgnoreCase) || type.Equals("junk", StringComparison.OrdinalIgnoreCase);
    // The chain trap item type is "chainTrap" (camelCase) in the item database, unlike the all lowercase "beartrap".
    // Placed traps of both kinds carry the "junk" loot they give back, so "junk" still matches them while an unplaced chain trap item only matches through its own type.
    if (trigger.isChainTrap) return type.Equals("chainTrap", StringComparison.OrdinalIgnoreCase) || type.Equals("junk", StringComparison.OrdinalIgnoreCase);
    if (trigger.isMutatedTrap) return true;
    return false;
  }

  // The name the cursor should show for a trap, based on the Recover Items settings, so the tooltip matches what disarming it or picking it up will actually hand over.
  // Returns null when the mod is not changing that trap's reward, in which case the vanilla text is left alone.
  internal static string GetTrapRewardName(Trigger trigger)
  {
    if (!trigger) return null;

    string pristineType;
    int scrapAmount;
    bool givePristineTrap;

    if (trigger.isBearTrap && Plugin.BearTrapRecovery.Value)
    {
      pristineType = "beartrap";
      scrapAmount = 3;
      givePristineTrap = !Plugin.BearTrapRecoverySwitch.Value;
    }
    else if (trigger.isChainTrap && Plugin.ChainTrapRecovery.Value)
    {
      pristineType = "chainTrap";
      scrapAmount = 2;
      givePristineTrap = !Plugin.ChainTrapRecoverySwitch.Value;
    }
    else
    {
      return null;
    }

    // Use the same display names the game does: the pristine trap through its item name key, the scrap through the name of the native loot every trap carries.
    return givePristineTrap ? Language.Get(pristineType + "_name", "Items") : $"{Language.Get("junk_name", "Items")} ({scrapAmount})";
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
    if (!Player.Instance || !Singleton<Controller>.Instance) return;
    var bigLocation = Player.Instance.whereAmI?.bigLocation;
    if (!bigLocation) return;
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
    if (!_collider) return true;

    var go = _collider.gameObject;
    if (go.CompareTag("Door"))
    {
      var door = Door.getDoorScript(go.transform.parent);
      if (door && door.barricaded)
      {
        if (Plugin.DefensesLogging.Value)
          Plugin.Log.LogInfo($"[Defenses] Blocked enemy barricade damage on door '{door.name}' from '{__instance.attackerTransform?.name ?? "unknown"}'");
        return false;
      }
    }

    var window = go.GetComponent<Window>();
    if (!window && _collider.attachedRigidbody)
      window = _collider.attachedRigidbody.GetComponent<Window>();
    if (!window || !window.barricaded) return true;
    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] Blocked enemy barricade damage on window '{window.name}' from '{__instance.attackerTransform?.name ?? "unknown"}'");
    return false;
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
    if (!item) return;

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

    // A trap that is being restored from a save also comes through here: Trigger's SaveState sets the trap back to triggered and calls OnAfterTrigger, expecting the triggered presentation to be applied again (sprung sprite, not disarmable, loot pickable as a dropped item).
    // Letting vanilla handle that case keeps a trap that was saved mid-recharge looking and behaving like a sprung trap after loading.
    // Nothing is queued here, the recharge itself is restored from the respawn state file, so the remaining time is not restarted.
    if (__instance.loadedFromSave) return true;

    // A trap can end up processing more than one trigger call for what's really a single event - e.g. a creature's separate hitboxes, or the player and a companion, entering its trigger volume in the same physics step, before "active = false" from the first call has actually stopped anything.
    // Without this guard, each of those calls queues its own RechargeRecord for the same trap, so it can end up "recharging" and re-triggering far too quickly - looking like an instant reset - and having its damage reapplied more than once.
    // Once switchToTriggered() has run (below), __instance.triggered is true until the trap actually recharges, so this only blocks duplicates within the same episode, not a legitimate re-trigger after a real recharge.
    if (__instance.triggered) return false;

    var rechargeTime = GetRechargeTime(__instance);
    if (rechargeTime <= 0f) return true;

    // Chain traps are handled differently from bear traps.
    // They have no triggered state to show and vanilla removes them entirely when they fire - spawning the chain attached to whatever triggered them - so there is no object left to recharge.
    // Keeping the game object around (which is what the code below does for bear traps) leaves an armed looking trap sitting next to the victim and still pickable, which is not what the trap does in vanilla.
    // So let vanilla run its normal trigger sequence and queue a fresh armed trap to be spawned once the recharge time is up.
    // With ChainTrap Auto Recharge disabled GetRechargeTime() already returned 0 above, so a chain trap simply stays vanilla (it disappears and is never respawned).
    if (__instance.isChainTrap)
    {
      var trapPrefab = GetTrapPrefab("chainTrap");
      if (trapPrefab)
      {
        RechargeQueue.Add(new RechargeRecord
        {
          RespawnPrefab = trapPrefab,
          ItemType = "chainTrap",
          Position = __instance.transform.position,
          Rotation = __instance.transform.rotation,
          ReadyTime = Time.time + rechargeTime,
          SetByPlayer = __instance.setByPlayer,
          Location = GetCurrentLocation()
        });
        if (Plugin.DefensesLogging.Value)
          Plugin.Log.LogInfo($"[Defenses] ChainTrap '{__instance.name}' triggered, will respawn in {rechargeTime} seconds");
        return true;
      }
      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogWarning($"[Defenses] Could not resolve the chainTrap prefab, keeping the triggered ChainTrap '{__instance.name}' in the world instead of respawning it");
    }

    var sprite = __instance.GetComponent<tk2dBaseSprite>();
    if (!sprite) sprite = __instance.GetComponentInChildren<tk2dBaseSprite>();
    var armedSprite = sprite ? sprite.CurrentSprite.name : "";

    if (!__instance.loadedFromSave)
    {
      if (!string.IsNullOrEmpty(__instance.activateSound))
        AudioController.Play(__instance.activateSound, __instance.transform);
      if (__instance.prefabToSpawn)
      {
        var spawn = Core.AddPrefab(__instance.prefabToSpawn, __instance.transform.position + new Vector3(0f, 1f, 0f), Quaternion.Euler(90f, 0f, 0f), null);
        if (spawn && __instance.isChainTrap && doConnectChain)
        {
          var chainParent = spawn.GetComponent<ChainParent>();
          if (chainParent && other)
            chainParent.target = other.gameObject;
        }
      }
      if (__instance.alertRadius > 0f)
        Character.alertInArea(__instance.transform.position, __instance.alertRadius, false, 1f);
    }

    var item = __instance.GetComponent<Item>();
    if (item) item.onTriggerFire();

    // Reuse vanilla's own switchToTriggered() instead of reimplementing it.
    // It handles the sprite swap the same way SetTriggeredVisual did, but also stops and destroys the trigger's sprite animator when stopAnimatorAfter is set (removeSoundsAfterTrigger too);
    // Without that, a trap with a looping "armed" animation keeps repainting its sprite every frame and silently undoes a one-off SetSprite() call, which is why chain traps were showing a "mixed" triggered/untriggered look while bear traps (no competing animator) looked fine.
    // It also sets triggered/active/canDisarm and Item.isDroppedItem for us, matching vanilla exactly.
    __instance.switchToTriggered();
    __instance.canDisarm = false; // belt-and-braces in case this trap has multipleTrigger set

    // Vanilla's own trigger sequence also marks the trap as a dropped item and fills its loot slot, which is what makes a sprung trap pickable.
    // This path replaces that sequence, so do both here.
    // A trap whose loot slot is empty can be neither picked up nor disarmed, which is exactly the "stuck trap" state players end up reporting.
    if (item)
    {
      item.isDroppedItem = true;
      EnsureTrapLoot(__instance, item);
    }

    // Queue the recharge first, before touching anything else below, so nothing that follows can prevent the trap from actually being tracked for recharge.
    RechargeQueue.Add(new RechargeRecord
    {
      Trap = __instance,
      ReadyTime = Time.time + rechargeTime,
      ArmedSprite = armedSprite,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      SetByPlayer = __instance.setByPlayer,
      Location = GetCurrentLocation(),
      ObjectId = Core.getIDFrom(__instance.gameObject)
    });

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

      // Respawn record: the chain trap was removed by vanilla when it triggered, so there is no game object to switch back on.
      // Wait until the player is back in the location the trap was placed in, so nothing is spawned into a location that is not loaded.
      if (record.RespawnPrefab)
      {
        if (Time.time < record.ReadyTime) continue;
        if (!string.IsNullOrEmpty(record.Location) && GetCurrentLocation() != record.Location) continue;
        RechargeQueue.RemoveAt(i);
        RespawnTrap(record);
        continue;
      }

      // In place record.
      // The trap normally still exists, but right after loading a save it may not have been restored yet (or it may live in a location that is not loaded), so look it back up before doing anything.
      // A record whose trap cannot be found while the player is standing in the same location is dropped: at that point the trap is gone from the world for good, most likely because the player picked it up or disarmed it.
      if (ReferenceEquals(record.Trap?.gameObject, null))
      {
        record.Trap = null;
        if (!string.IsNullOrEmpty(record.Location) && GetCurrentLocation() != record.Location) continue;
        record.Trap = FindTrap(record);
        if (!record.Trap)
        {
          RechargeQueue.RemoveAt(i);
          continue;
        }
      }
      if (Time.time < record.ReadyTime) continue;
      RechargeQueue.RemoveAt(i);

      var trap = record.Trap;
      trap.active = true;
      trap.canDisarm = true;
      trap.triggered = false;

      var sprite = trap.GetComponent<tk2dBaseSprite>();
      if (!sprite) sprite = trap.GetComponentInChildren<tk2dBaseSprite>();
      if (sprite && !string.IsNullOrEmpty(record.ArmedSprite))
        sprite.SetSprite(record.ArmedSprite);

      // Undo the dropped-item marker from TrapRechargePrefix now that the trap is armed again, so it goes back to being disarm-able instead of a ground pickup.
      var item = trap.GetComponent<Item>();
      if (item) item.isDroppedItem = false;
      EnsureTrapLoot(trap, item);

      trap.checkCollisions();

      if (Plugin.DefensesLogging.Value)
        Plugin.Log.LogInfo($"[Defenses] Trap '{trap.name}' recharged and is active again");
    }
  }

  // Finds the trap an in place recharge record belongs to.
  // The save id is the reliable path, since traps placed by the player are registered with a unique id; searching near the recorded position is a fallback for any trap that never got one.
  private static Trigger FindTrap(RechargeRecord record)
  {
    var byId = FindTrapById(record.ObjectId);
    if (byId) return byId;

    return UnityEngine.Object.FindObjectsOfType<Trigger>().Where(trigger => trigger && IsTrapType(trigger)).Where(trigger => trigger.triggered && !trigger.active).FirstOrDefault(trigger => !((trigger.transform.position - record.Position).sqrMagnitude > 1f));
  }

  // Core.getGOFromID() reads .gameObject off the Transform stored in the save id dictionary, which throws a MissingReferenceException once that object has been destroyed - and the dictionary is never pruned when objects are destroyed, so the entry can linger there.
  // Looking the entry up here allows the destroyed case to be treated as "not found" instead of blowing up a respawn tick.
  // Ids of 0 mean the object has not been given one by a save yet, which counts as no id at all.
  private static GameObject FindObjectById(int objectId)
  {
    if (objectId <= 0) return null;
    var saveManager = Singleton<SaveManager>.Instance;
    if (!saveManager || !saveManager.uniqueIdDict.TryGetValue(objectId, out var transform)) return null;
    return !transform ? null : transform.gameObject;
  }

  private static Trigger FindTrapById(int objectId)
  {
    var go = FindObjectById(objectId);
    if (!go) return null;
    var trigger = go.GetComponent<Trigger>();
    return trigger && IsTrapType(trigger) ? trigger : null;
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

  // Spawns a replacement for a chain trap that vanilla removed when it triggered.
  // Mirrors how the game places a trap itself (Player.progressBarCompleted): the item's world prefab is spawned at the recorded position, registered for saving and with the world grid.
  // checkCollisions() is deliberately not called, so a creature that is still standing on the spot does not set the trap off the moment it appears again.
  private static void RespawnTrap(RechargeRecord record)
  {
    var go = Core.AddPrefab(record.RespawnPrefab, record.Position, record.Rotation, null);
    if (!go) return;

    // Never trust the prefab's own state here: the replacement has to come back armed and disarmable, exactly like the trap it stands in for.
    // An inactive or still triggered copy would look like a stuck trap to the player.
    var trigger = go.GetComponent<Trigger>();
    if (trigger)
    {
      trigger.setByPlayer = record.SetByPlayer;
      trigger.active = true;
      trigger.triggered = false;
      trigger.canDisarm = true;
      var item = go.GetComponent<Item>();
      if (item) item.isDroppedItem = false;
      EnsureTrapLoot(trigger, item);
    }

    Core.addToSaveable(go, true, true);
    if (Singleton<WorldGrid>.Instance)
      Singleton<WorldGrid>.Instance.registerToNode(go);

    if (Plugin.DefensesLogging.Value)
      Plugin.Log.LogInfo($"[Defenses] ChainTrap respawned at {record.Position}");
  }

  // A sprung trap is picked up through the item sitting in its own inventory slot 0, so a trap whose slot is empty can never be picked up again.
  // Vanilla keeps the native scrap metal in that slot, so put one back when it is missing instead of leaving the trap stuck.
  private static void EnsureTrapLoot(Trigger trigger, Item item)
  {
    var inventory = trigger ? trigger.GetComponent<Inventory>() : null;
    if (!inventory || inventory.slots.Count == 0) return;
    var slot = inventory.slots[0];
    if (!InvItemClass.isNull(slot.invItem)) return;
    slot.createItem(item && item.invItem ? item.invItem.type : "junk", 1);
  }

  // InvItem.item is the world prefab the game itself spawns when an item is placed, the same field Player.progressBarCompleted uses for traps.
  // Passing instantiate false returns the component on the loaded prefab instead of cloning it just to read one field.
  // Note that the chain trap item type is "chainTrap" (camelCase), unlike the all lowercase "beartrap", and ItemsDatabase lookups are case-sensitive.
  private static UnityEngine.Object GetTrapPrefab(string type)
  {
    if (!Singleton<ItemsDatabase>.Instance) return null;
    var item = Singleton<ItemsDatabase>.Instance.getItem(type, false);
    return item ? item.item : null;
  }

  private static string GetCurrentLocation()
  {
    if (!Player.Instance || !Player.Instance.whereAmI || !Player.Instance.whereAmI.bigLocation) return null;
    return Player.Instance.whereAmI.bigLocation.name;
  }

  // Mushroom Respawn, Stepping on a mushroom consumes it via OnAfterTrigger.
  [HarmonyPatch(typeof(Trigger), "OnAfterTrigger")]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void OnAfterTriggerPostfix(Trigger __instance)
  {
    if (!Plugin.MushroomRespawn.Value) return;
    // Loading a save calls OnAfterTrigger again for anything that was saved triggered, so without this a harvested mushroom would be rescheduled on every load.
    if (__instance.loadedFromSave) return;
    if (!__instance.isBearTrap && !__instance.isMutatedTrap) return;
    if (IsTrapType(__instance)) return;
    if (!Player.Instance || !Player.Instance.whereAmI?.bigLocation) return;
    if (!Singleton<Controller>.Instance) return;

    MushroomRespawnQueue.Add(new MushroomRespawnRecord
    {
      Location = Player.Instance.whereAmI.bigLocation.name,
      Prefab = __instance.gameObject.name,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      ConsumedAt = Singleton<Controller>.Instance.totalTime
    });
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Mushroom '{__instance.gameObject.name}' at {__instance.transform.position} consumed, scheduled for respawn");
  }

  // Harvesting a mushroom goes through Item.switchTriggerState.
  // The prefab name has to be captured before that call runs, because the game may rename the object while switching it to its triggered state.
  [HarmonyPatch(typeof(Item), nameof(Item.switchTriggerState))]
  [HarmonyPrefix]
  // ReSharper disable once InconsistentNaming
  private static void SwitchTriggerStatePrefix(Item __instance)
  {
    _lastSwitchTriggerPrefab = __instance.gameObject.name;
  }

  // Harvesting a mushroom consumes it via Item.switchTriggerState.
  // Mushrooms set staysAfterDisarming, so the game leaves a picked over husk behind instead of destroying the object;
  // that husk is not interactable and the mushroom is just as consumed as one that disappears, so it has to be recorded here as well.
  [HarmonyPatch(typeof(Item), nameof(Item.switchTriggerState))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void SwitchTriggerStatePostfix(Item __instance)
  {
    if (!Plugin.MushroomRespawn.Value) return;
    var trigger = __instance.GetComponent<Trigger>();
    if (!trigger) return;
    if (!trigger.isBearTrap && !trigger.isMutatedTrap) return;
    if (IsTrapType(trigger)) return;
    if (!Player.Instance || !Player.Instance.whereAmI?.bigLocation) return;
    if (!Singleton<Controller>.Instance) return;

    MushroomRespawnQueue.Add(new MushroomRespawnRecord
    {
      Location = Player.Instance.whereAmI.bigLocation.name,
      Prefab = string.IsNullOrEmpty(_lastSwitchTriggerPrefab) ? __instance.gameObject.name : _lastSwitchTriggerPrefab,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      ConsumedAt = Singleton<Controller>.Instance.totalTime
    });
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Mushroom '{__instance.gameObject.name}' at {__instance.transform.position} harvested, scheduled for respawn");
  }

  // Loot Respawn
  [HarmonyPatch(typeof(Inventory), nameof(Inventory.hide))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  private static void InventoryHidePostfix(Inventory __instance)
  {
    if (!Plugin.LootRespawn.Value) return;
    if (__instance.invType != Inventory.InvType.itemInv) return;
    if (__instance.isWorkbench) return;
    // The saw and the workbench are stations with their own inventory, not containers the player loots.
    if (__instance.GetComponent<Saw>()) return;
    if (__instance.getAllItems().Count > 0) return;
    if (!Player.Instance || !Player.Instance.whereAmI?.bigLocation) return;
    if (!Singleton<Controller>.Instance) return;

    var prefabName = __instance.gameObject.name;
    if (prefabName.EndsWith("(Clone)")) prefabName = prefabName.Substring(0, prefabName.Length - 7);

    // Give the container a save id right away, even before the game next saves, so the respawn can always find it again by id instead of falling back to a search that cannot see objects that are currently culled.
    Core.addToSaveable(__instance.gameObject, false, true);
    var objectId = Core.getIDFrom(__instance.gameObject);

    LootRespawnQueue.Add(new LootRespawnRecord
    {
      Location = Player.Instance.whereAmI.bigLocation.name,
      Prefab = prefabName,
      Position = __instance.transform.position,
      Rotation = __instance.transform.rotation,
      ConsumedAt = Singleton<Controller>.Instance.totalTime,
      ObjectId = objectId > 0 ? objectId : -1,
      // Containers the game destroys when they are emptied have to be spawned back from their prefab.
      // Everything else stays in the world, so it is only ever restocked in place and never replaced.
      RemoveWhenEmpty = __instance.removeWhenEmpty
    });
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Container '{prefabName}' emptied at {__instance.transform.position}, scheduled for respawn");
  }

  // Per-day respawns
  [HarmonyPatch(typeof(Controller), nameof(Controller.startDay))]
  [HarmonyPostfix]
  private static void ControllerStartDayPostfix()
  {
    if (!Plugin.MushroomRespawn.Value && !Plugin.LootRespawn.Value) return;
    if (!Player.Instance || !Player.Instance.whereAmI?.bigLocation) return;
    var bigLocation = Player.Instance.whereAmI.bigLocation;
    var locationName = bigLocation.name;
    var changed = false;

    if (Plugin.MushroomRespawn.Value && Plugin.MushroomRespawnPerDay.Value)
    {
      for (var i = MushroomRespawnQueue.Count - 1; i >= 0; i--)
      {
        if (MushroomRespawnQueue[i].Location != locationName) continue;
        var prefab = ResolveMushroomPrefab(MushroomRespawnQueue[i], bigLocation.nightMushroom);
        if (prefab)
        {
          RemoveMushroomRemainsAt(MushroomRespawnQueue[i].Position);
          SpawnPrefab(prefab, Core.getYPos(MushroomRespawnQueue[i].Position, PosType.items2), MushroomRespawnQueue[i].Rotation, "Mushroom");
        }
        MushroomRespawnQueue.RemoveAt(i);
        changed = true;
      }
    }

    if (Plugin.LootRespawn.Value && Plugin.LootRespawnPerDay.Value)
    {
      for (var i = LootRespawnQueue.Count - 1; i >= 0; i--)
      {
        if (LootRespawnQueue[i].Location != locationName) continue;
        if (!RespawnContainer(LootRespawnQueue[i])) continue;
        LootRespawnQueue.RemoveAt(i);
        changed = true;
      }
    }

    if (!changed) return;
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo("[Loot] Per-day respawn completed");
  }

  // Timer-based respawns
  private static void TickMushroomRespawn(string locationName, int currentTime, UnityEngine.Object nightMushroom)
  {
    var respawnTime = Plugin.MushroomRespawnTime.Value;

    for (var i = MushroomRespawnQueue.Count - 1; i >= 0; i--)
    {
      var r = MushroomRespawnQueue[i];
      if (r.Location != locationName) continue;
      if (currentTime - r.ConsumedAt < respawnTime) continue;
      var prefab = ResolveMushroomPrefab(r, nightMushroom);
      if (prefab)
      {
        RemoveMushroomRemainsAt(r.Position);
        SpawnPrefab(prefab, Core.getYPos(r.Position, PosType.items2), r.Rotation, "Mushroom");
      }
      MushroomRespawnQueue.RemoveAt(i);
    }
  }

  // Spawns back the same kind of mushroom that was consumed when its prefab can be resolved, and falls back to the location's night mushroom (which is what this feature used for every record before the prefab was stored) when it cannot.
  private static UnityEngine.Object ResolveMushroomPrefab(MushroomRespawnRecord record, UnityEngine.Object nightMushroom)
  {
    if (string.IsNullOrEmpty(record.Prefab)) return nightMushroom;
    var prefab = Singleton<SaveManager>.Instance?.getPrefab(record.Prefab);
    if (prefab) return prefab;
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogWarning($"[Loot] Could not resolve the '{record.Prefab}' mushroom prefab, falling back to the location's night mushroom");
    return nightMushroom;
  }

  // A world mushroom that has been consumed: not a trap, not a mushroom item, sitting in the triggered state the game leaves behind (the non interactable "remains" husk).
  private static bool IsMushroomRemains(Trigger trigger)
  {
    if (!trigger) return false;
    if (!trigger.isBearTrap && !trigger.isMutatedTrap) return false;
    if (IsTrapType(trigger)) return false;
    return trigger.triggered && !trigger.active && !trigger.canDisarm;
  }

  // Traps can end up stuck in the world in a state where they can be neither disarmed nor triggered, usually because a recharging trap lost its record (loading a save does that) or because an older build left one triggered, and a trap whose loot slot is empty cannot be picked up either.
  // This runs on world load and brings them back to a usable state.
  private static void RepairStuckTraps()
  {
    try
    {
      var repaired = 0;
      foreach (var trigger in UnityEngine.Object.FindObjectsOfType<Trigger>())
      {
        if (!trigger || !IsTrapType(trigger)) continue;
        var item = trigger.GetComponent<Item>();
        if (!item) continue;
        var inventory = trigger.GetComponent<Inventory>();
        var slot = inventory && inventory.slots.Count > 0 ? inventory.slots[0] : null;
        var hasLoot = slot != null && !InvItemClass.isNull(slot.invItem);

        if (Plugin.DefensesLogging.Value)
        {
          // The save id and the parent are logged because a trap that was spawned by the mod's own respawn instead of being placed by the player has no parent object, which is the difference that is otherwise impossible to see in game when a trap misbehaves.
          var saveable = trigger.GetComponent<SaveableObject>();
          Plugin.Log.LogInfo($"[Defenses] Trap '{trigger.name}' at {trigger.transform.position}: active={trigger.active} triggered={trigger.triggered} canDisarm={trigger.canDisarm} dropped={item.isDroppedItem} invItem={(item.invItem ? item.invItem.type + "x" + item.invItemAmount : "null")} loot={(hasLoot ? slot.invItem.type + "x" + slot.invItem.amount : "none")} saveId={(saveable != null ? saveable.uniqueId.ToString() : "none")} parent={(trigger.transform.parent != null ? trigger.transform.parent.name : "none")}");
        }

        var changed = false;

        // No loot in the slot means it cannot be picked up, no disarm reward means it cannot be disarmed.
        // Each is restored from the other, falling back to the native scrap metal.
        if (slot != null && !hasLoot)
        {
          slot.createItem(item.invItem ? item.invItem.type : "junk", 1);
          hasLoot = true;
          changed = true;
        }
        if (!item.invItem && hasLoot)
        {
          item.invItem = slot.invItem.baseClass;
          item.invItemAmount = 1;
          changed = true;
        }

        if (trigger.triggered && !trigger.active)
        {
          // Sprung: make it pickable again and let it recharge when the setting is on
          if (!item.isDroppedItem)
          {
            item.isDroppedItem = true;
            changed = true;
          }
          var rechargeTime = GetRechargeTime(trigger);
          if (rechargeTime > 0f && !RechargeQueue.Exists(record => ReferenceEquals(record.Trap, trigger)))
          {
            RechargeQueue.Add(new RechargeRecord
            {
              Trap = trigger,
              ReadyTime = Time.time + rechargeTime,
              ArmedSprite = GetArmedSpriteFromPrefab(trigger),
              Position = trigger.transform.position,
              Rotation = trigger.transform.rotation,
              SetByPlayer = trigger.setByPlayer,
              Location = GetCurrentLocation(),
              ObjectId = Core.getIDFrom(trigger.gameObject)
            });
            changed = true;
          }
        }
        else if (!trigger.active && !trigger.canDisarm)
        {
          // Neither armed nor pickable, re-arm it so it can be used again
          trigger.active = true;
          trigger.canDisarm = true;
          trigger.triggered = false;
          changed = true;
        }

        if (!changed) continue;
        repaired++;
        if (Plugin.DefensesLogging.Value)
          Plugin.Log.LogInfo($"[Defenses] Repaired stuck trap '{trigger.name}' at {trigger.transform.position}");
      }

      if (repaired > 0)
        Plugin.Log.LogInfo($"[Defenses] Repaired {repaired} stuck trap(s)");
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"[Defenses] Failed to repair stuck traps: {e.Message}");
    }
  }

  // The armed sprite of a trap comes from its prefab, which is what the repair uses to restore a sprung trap when it has no saved sprite to go back to.
  private static string GetArmedSpriteFromPrefab(Trigger trigger)
  {
    var prefab = GetTrapPrefab(trigger.isChainTrap ? "chainTrap" : "beartrap") as GameObject;
    if (!prefab) return "";
    var sprite = prefab.GetComponent<tk2dBaseSprite>() ?? prefab.GetComponentInChildren<tk2dBaseSprite>();
    return sprite ? sprite.CurrentSprite.name : "";
  }

  // Harvested mushrooms leave their husk behind. Remove the one sitting where the mushroom is about to grow back, otherwise both end up sharing the same spot.
  // The radius is small because the husk does not move from where it was harvested, while mushroom clusters can have neighbours half a unit away that must be left alone.
  private static void RemoveMushroomRemainsAt(Vector3 position)
  {
    foreach (var trigger in UnityEngine.Object.FindObjectsOfType<Trigger>())
    {
      if (!IsMushroomRemains(trigger)) continue;
      if ((trigger.transform.position - position).sqrMagnitude > 0.25f) continue;
      if (Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Removing mushroom remains at {trigger.transform.position}");
      UnityEngine.Object.Destroy(trigger.gameObject);
    }
  }

  private static void TickLootRespawn(string locationName, int currentTime)
  {
    var respawnTime = Plugin.LootRespawnTime.Value;

    for (var i = LootRespawnQueue.Count - 1; i >= 0; i--)
    {
      var r = LootRespawnQueue[i];
      if (r.Location != locationName) continue;
      if (currentTime - r.ConsumedAt < respawnTime) continue;
      if (Time.time < r.NextAttempt) continue;
      if (RespawnContainer(r)) LootRespawnQueue.RemoveAt(i);
    }
  }

  // Restocks a container that was emptied.
  // A container that is still in the world is filled again in place, while one the game destroyed when it was emptied is spawned back from its prefab.
  // A container that is still around but has something in it again is left alone: the player put it back there, so it is not a looted container anymore.
  // Returns false when the container is a kind that stays in the world but could not be found, which means its location is not loaded right now.
  // The record is kept so a later visit can restock it, and nothing is spawned that could end up next to the original.
  private static bool RespawnContainer(LootRespawnRecord record)
  {
    var container = FindObjectById(record.ObjectId)?.GetComponent<Inventory>();
    if (!container || container.invType != Inventory.InvType.itemInv)
      container = FindContainerAt(record.Position, record.Prefab);

    if (container)
    {
      if (container.getAllItems().Count > 0) return true;
      RefillContainer(container, record);
      return true;
    }

    if (!record.RemoveWhenEmpty)
    {
      if (Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Container '{record.Prefab}' is not loaded right now, leaving it for a later visit");
      record.NextAttempt = Time.time + 5f;
      return false;
    }

    var prefab = Singleton<SaveManager>.Instance?.getPrefab(record.Prefab);
    if (prefab)
      SpawnPrefab(prefab, record.Position, record.Rotation, "Container");
    else if (Plugin.LogDebug.Value)
      Plugin.Log.LogWarning($"[Loot] Could not resolve the '{record.Prefab}' container prefab, nothing was respawned");
    return true;
  }

  // The save id is only assigned once the game saves, so a container that has never been saved is found by its prefab and position instead.
  // Resources.FindObjectsOfTypeAll is used because GetObjectsOfType cannot see containers that are currently culled, and the scene check drops prefab assets, which do not live in a scene.
  private static Inventory FindContainerAt(Vector3 position, string prefabName)
  {
    return (from inventory in Resources.FindObjectsOfTypeAll<Inventory>() where inventory where inventory.gameObject.scene.IsValid() where inventory.invType == Inventory.InvType.itemInv where !inventory.isWorkbench where inventory.GetComponent<Saw>() == null where inventory.gameObject.name == prefabName select inventory).FirstOrDefault(inventory => !((inventory.transform.position - position).sqrMagnitude > 1f));
  }

  // Fills a container that stayed in the world with loot again, mirroring how the game stocks it when the location is generated:
  // the fixed loot sitting on the prefab first, then the custom loot config for this container, and finally a fresh roll from the container's loot pool.
  private static void RefillContainer(Inventory container, LootRespawnRecord record)
  {
    var itemsBefore = container.getAllItems().Count;

    var prefab = Singleton<SaveManager>.Instance?.getPrefab(record.Prefab);
    var prefabInventory = prefab ? prefab.GetComponent<Inventory>() : null;
    if (prefabInventory)
    {
      foreach (var slot in prefabInventory.slots)
      {
        // A prefab keeps its loot in the editor field (slot.item); live instances turn that
        // into an InvItemClass when the inventory initializes.
        if (slot.item)
          container.addItemType(slot.item.type, Mathf.Max(1, slot.itemAmount));
        else if (!InvItemClass.isNull(slot.invItem))
          container.addItemType(slot.invItem.type, Mathf.Max(1, slot.invItem.amount));
      }
    }

    InventoryPatch.ApplyCustomLoot(container);

    var random = container.GetComponent<InventoryRandom>();
    if (random) RefillRandomLoot(container, random);

    var item = container.GetComponent<Item>();
    if (item) item.searched = false;

    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] Container '{record.Prefab}' restocked at {record.Position} with {container.getAllItems().Count - itemsBefore} items");
  }

  // Containers get their random loot from the location's difficulty preset, filtered through the container's own presets (see Location's generation code), and the spawned items sit in the randomizer's permitted pool afterward.
  // Spawning from that pool again is the faithful way to restock; when it is gone, for example right after loading a save, it is rebuilt from the preset the same way the game builds it.
  // Randomizers that opt out of the difficulty pool roll straight from their presets instead, which is what InventoryRandom.init does for them.
  private static void RefillRandomLoot(Inventory container, InventoryRandom random)
  {
    if (random.excludeFromDifficultyRandomizer || !random.inLocation)
    {
      random.randomize();
      if (Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Restock rolled from presets (excluded={random.excludeFromDifficultyRandomizer}, inLocation={random.inLocation}, disabled={random.disabled}, pool={random.permittedItems.Count})");
      return;
    }

    var difficultyPreset = GetDifficultyPreset(container.GetComponentInParent<Location>());
    if (difficultyPreset)
    {
      random.permittedItems.Clear();
      foreach (var permitted in random.presets.Where(preset => preset).SelectMany(preset1 => from permitted in difficultyPreset.permittedItems where permitted != null && permitted.type let permitted1 = permitted where preset1.allowedItems.Any(t => t == permitted1.type) select permitted))
      {
        random.permittedItems.Add(permitted);
      }
    }

    if (random.permittedItems.Count > 0)
    {
      random.spawnItems();
      if (Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Restock rolled from the difficulty pool (preset={difficultyPreset}, disabled={random.disabled}, pool={random.permittedItems.Count})");
      return;
    }

    if (Plugin.LogDebug.Value)
      Plugin.Log.LogWarning($"[Loot] Restock had no loot pool to roll from (preset={difficultyPreset}, presets={random.presets.Count}), falling back to the presets");

    // Nothing could be rebuilt from the location, fall back to the presets' own pools.
    random.randomize();
  }

  // A sub location never gets its difficulty preset assigned by the game (its containers are stocked through the parent location), so the preset is looked up by difficulty instead.
  private static DifficultyPreset GetDifficultyPreset(Location location)
  {
    if (!location) return null;
    var worldGenerator = Singleton<WorldGenerator>.Instance;
    if (worldGenerator)
    {
      foreach (var preset in worldGenerator.difficultyPresets.Where(preset => preset && preset.difficulty == location.difficulty)) { return preset; }
    }
    return location.difficultyPreset;
  }

  private static void SpawnPrefab(UnityEngine.Object prefab, Vector3 position, Quaternion rotation, string label)
  {
    var go = Core.AddPrefab(prefab, position, rotation, null, true);
    if (!go) return;
    Core.addToSaveable(go, true, true);
    if (Singleton<WorldGrid>.Instance)
      Singleton<WorldGrid>.Instance.registerToNode(go);
    if (Plugin.LogDebug.Value)
      Plugin.Log.LogInfo($"[Loot] {label} respawned at {position}");
  }

  // ===== Persistence =====

  // The respawn state file is only written when the game itself saves.
  // Everything it holds describes work that is still pending in the world that was just saved (a consumed mushroom, an emptied container, a trap that has not become usable again yet), so committing it at any other moment would make entries appear or disappear for a session state that was never saved.
  // Records are added and consumed in memory while playing; this postfix is the single point where they are committed.
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
    RepairStuckTraps();
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
          ["prefab"] = r.Prefab,
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
          ["consumedAt"] = r.ConsumedAt,
          ["objectId"] = r.ObjectId,
          ["removeWhenEmpty"] = r.RemoveWhenEmpty
        });
      }
      root["loot"] = loot;

      var traps = new JArray();
      foreach (var r in RechargeQueue.Where(r => r.RespawnPrefab || r.ObjectId != -1 || !ReferenceEquals(r.Trap?.gameObject, null)))
      {
        // A record whose trap was destroyed without being recharged is on its way out (TickRecharge drops it on its next pass), so there is nothing to save.
        // A trap that is still waiting to be found again after a load has no live reference but does have a save id, and must stay in the file.
        traps.Add(new JObject
        {
          ["location"] = r.Location,
          ["x"] = r.Position.x, ["y"] = r.Position.y, ["z"] = r.Position.z,
          ["rx"] = r.Rotation.x, ["ry"] = r.Rotation.y, ["rz"] = r.Rotation.z, ["rw"] = r.Rotation.w,
          // The recharge settings are in real seconds, so this is the time left on the clock rather than a timestamp on the in-game clock the other queues use.
          ["readyIn"] = Mathf.Max(0f, r.ReadyTime - Time.time),
          ["respawn"] = r.RespawnPrefab is not null,
          ["setByPlayer"] = r.SetByPlayer,
          ["itemType"] = r.ItemType ?? "",
          ["objectId"] = r.ObjectId,
          ["armedSprite"] = r.ArmedSprite ?? ""
        });
      }
      root["traps"] = traps;

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

  // After loading, a trap that is restored from a save gets its triggered presentation from vanilla one frame after the save values are applied.
  // An in place record that is already due must not arm the trap before that presentation has run, or the deferred switchToTriggered() would put the sprung sprite and the disabled state back on the trap right after it was armed, leaving it stuck that way.
  // Half a second is far more than one frame and imperceptible for a trap that is ready to be used again.
  private const float LoadPresentationDelay = 0.5f;

  private static void LoadRespawnState()
  {
    MushroomRespawnQueue.Clear();
    LootRespawnQueue.Clear();
    RechargeQueue.Clear();

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
            Prefab = t["prefab"]?.Value<string>() ?? "",
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
            ConsumedAt = t["consumedAt"]?.Value<int>() ?? 0,
            ObjectId = t["objectId"]?.Value<int>() ?? -1,
            // Records from before this field existed all came from containers the game destroys when they are emptied.
            RemoveWhenEmpty = t["removeWhenEmpty"]?.Value<bool>() ?? true
          });
        }
      }

      if (root["traps"] is JArray traps)
      {
        foreach (var t in traps)
        {
          var record = new RechargeRecord
          {
            Location = t["location"]?.Value<string>() ?? "",
            Position = new Vector3(t["x"]?.Value<float>() ?? 0f, t["y"]?.Value<float>() ?? 0f, t["z"]?.Value<float>() ?? 0f),
            Rotation = new Quaternion(t["rx"]?.Value<float>() ?? 0f, t["ry"]?.Value<float>() ?? 0f, t["rz"]?.Value<float>() ?? 0f, t["rw"]?.Value<float>() ?? 1f),
            ReadyTime = Time.time + Mathf.Max(t["readyIn"]?.Value<float>() ?? 0f, LoadPresentationDelay),
            SetByPlayer = t["setByPlayer"]?.Value<bool>() ?? false,
            ObjectId = t["objectId"]?.Value<int>() ?? -1,
            ArmedSprite = t["armedSprite"]?.Value<string>() ?? ""
          };

          if (t["respawn"]?.Value<bool>() ?? false)
          {
            record.ItemType = t["itemType"]?.Value<string>() ?? "";
            record.RespawnPrefab = GetTrapPrefab(record.ItemType);
            if (!record.RespawnPrefab)
            {
              Plugin.Log.LogWarning($"[Defenses] Skipping a saved trap respawn, the '{record.ItemType}' prefab could not be resolved");
              continue;
            }
          }
          // In place records keep a null Trap for now: the trap itself is restored by the game when its location loads, so TickRecharge finds it again by save id.

          RechargeQueue.Add(record);
        }
      }

      if (Plugin.LogDebug.Value)
        Plugin.Log.LogInfo($"[Loot] Loaded respawn state: {MushroomRespawnQueue.Count} mushrooms, {LootRespawnQueue.Count} containers, {RechargeQueue.Count} traps");
    }
    catch (Exception e)
    {
      Plugin.Log.LogError($"[Loot] Failed to load respawn state: {e.Message}");
    }
  }

  // Clean up mushrooms stuck in the triggered state from the old trap-recharge bug.
  // This also clears the husks that harvested mushrooms leave behind, they are not interactable and the mushroom grows back on its own timer.
  private static void CleanupStuckMushrooms()
  {
    try
    {
      var cleaned = 0;
      foreach (var trigger in UnityEngine.Object.FindObjectsOfType<Trigger>())
      {
        if (!IsMushroomRemains(trigger)) continue;
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
