using HarmonyLib;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class InventoryPatch
{
  [HarmonyPatch(typeof(Inventory), nameof(Inventory.show))]
  [HarmonyPostfix]
  public static void InventoryBackgrounds(Inventory instance, string labelName = "")
  {
    var gameObject = instance.thisUI;
    if (!gameObject.TryGetComponent<PositionMe>(out var positionMe)) return;
    if (Plugin.WorkbenchInventoryModification.Value && labelName == "Storage")
    {
      positionMe.offset = new Vector2(instance.position.x + Plugin.StorageXOffset.Value, instance.position.z + Plugin.StorageZOffset.Value);
    }

    if (!Plugin.CraftingModification.Value || instance.invType != Inventory.InvType.crafting) return;
    positionMe.offset = new Vector2(instance.position.x + Plugin.CraftingXOffset.Value, instance.position.z + Plugin.CraftingZOffset.Value);
    UpgradeItemMenuPatch.CraftingPos.x = positionMe.offset.x;
    UpgradeItemMenuPatch.CraftingPos.y = positionMe.offset.y;
    if (!instance.isWorkbench) return;
    foreach (var child in gameObject.GetComponentsInChildren<Transform>())
    {
      if (child.name != "WorkbenchBackground") continue;
      UnityEngine.Object.Destroy(child.gameObject);
      var extraSlots = Plugin.CraftingRightSlots.Value - 5f;
      var xPosition = 119f + 31f * extraSlots;
      var xScale = 1f + 0.21f * extraSlots;
      var workbenchBackground = Core.AddPrefab("UI/WorkbenchBackground", new Vector3(xPosition, -10f, -233f), Quaternion.Euler(90f, 0f, 0f), gameObject.gameObject, false);
      workbenchBackground.transform.localScale = new Vector3(xScale, 1f, 1f);
      Singleton<InventoryController>.Instance.repairBtn.transform.position = gameObject.transform.position + new Vector3(xPosition, 5f, -409f);
      Singleton<InventoryController>.Instance.upgradeBtn.transform.position = gameObject.transform.position + new Vector3(xPosition, 5f, -451f);
      Singleton<InventoryController>.Instance.upgradeWorkbenchBtn.transform.position = gameObject.transform.position + new Vector3(xPosition, 5f, -503f);
      break;
    }
  }

  [HarmonyPatch(typeof(Inventory), nameof(Inventory.show))]
  [HarmonyPrefix]
  public static void InventorySlots(Inventory instance, string labelName = "")
  {
    var maxSlots = 0;

    if (labelName == "Storage")
    {
      if (Plugin.WorkbenchInventoryModification.Value)
      {
        instance.maxColumns = Plugin.RightSlots.Value;
        maxSlots = instance.maxColumns * Plugin.DownSlots.Value;
      }
      else
      {
        instance.maxColumns = 6;
        maxSlots = 48; // 6*8
      }
    }
    else
    {
      switch (instance.invType)
      {
        case Inventory.InvType.crafting:
          if (Plugin.CraftingModification.Value)
          {
            instance.maxColumns = Plugin.CraftingRightSlots.Value;
            maxSlots = instance.maxColumns * Plugin.CraftingDownSlots.Value;
          }
          else
          {
            instance.maxColumns = 5;
            maxSlots = 35; // 5*7
          }
          break;
        case Inventory.InvType.hotbar:
          if (Plugin.HotbarSlots.Value)
          {
            instance.maxColumns = Plugin.HotbarRightSlots.Value;
            maxSlots = instance.maxColumns * Plugin.HotbarDownSlots.Value;
          }
          else
          {
            instance.maxColumns = 1;
            maxSlots = 3 + Player.Instance.hotbarUpgrades;
          }
          break;
        case Inventory.InvType.playerInv:
          if (Plugin.InventorySlots.Value)
          {
            instance.maxColumns = Plugin.InventoryRightSlots.Value;
            maxSlots = instance.maxColumns * Plugin.InventoryDownSlots.Value;
          }
          else
          {
            instance.maxColumns = 2;
            maxSlots = 12 + (2 * Player.Instance.inventoryUpgrades);
          }
          break;
        case Inventory.InvType.shop:
          if (Plugin.TraderSlots.Value)
          {
            instance.maxColumns = Plugin.TraderRightSlots.Value;
            maxSlots = instance.maxColumns * Plugin.TraderDownSlots.Value;
          }
          else
          {
            instance.maxColumns = 6;
            maxSlots = 42; // 6*7
          }
          break;
        case Inventory.InvType.itemInv:
        case Inventory.InvType.shopExchangePlayer:
        case Inventory.InvType.shopExchangeTrader:
        case Inventory.InvType.leveling:
        case Inventory.InvType.deathDrop:
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }
    
    if (maxSlots > 0 && maxSlots != instance.slots.Count)
    {
      if (Plugin.LogDebug.Value)
      {
        Plugin.LogDivider();
        Plugin.Log.LogInfo($"Player has {Player.Instance.inventoryUpgrades} inventory upgrades and {Player.Instance.hotbarUpgrades} hotbar upgrades.");
      }
      ChangeSlots(instance, maxSlots);
    }
  }
  public static void ChangeSlots(Inventory instance, int maximumSlots)
  {
    var difference = maximumSlots - instance.slots.Count;
    if (Plugin.LogDebug.Value)
    {
      Plugin.LogDivider();
      Plugin.Log.LogInfo($"Maximum slots allowed in \"{instance.name}\" is {maximumSlots} and we have {instance.slots.Count}");

      if (difference < 0)
      {
        Plugin.Log.LogInfo($"Removing {Math.Abs(difference)} slots");
      }
      else
      {
        Plugin.Log.LogInfo($"Adding {difference} slots");
      }
      Plugin.LogDivider();
    }
    while (difference < 0 && Plugin.RemoveExcess.Value)
    {
      for (var i = maximumSlots - 1; i < instance.slots.Count; i++)
      {
        instance.slots.RemoveAt(i - 1);
        difference++;
      }
    }
    while (difference > 0)
    {
      for (var i = 0; i < difference; i++)
      {
        instance.slots.Add(new InvSlot());
        difference--;
      }
    }
    return;
  }

  [HarmonyPatch(typeof(Inventory), nameof(Inventory.initSlots))]
  [HarmonyPrefix]
  static void InventoryItems(ref Inventory instance)
  {
    var name = instance.gameObject.name;

    // Check if the inventory name exists in CustomLoot
    if (!Plugin.CustomLoot.ContainsKey(name))
    {
      // Create an empty JArray for itemsList
      var itemsList = new JArray();

      // Loop through slots and add items dynamically
      for (var i = 0; i < instance.slots.Count; i++)
      {
        var itemObject = new JObject
        {
          { "item", instance.slots[i].item?.type ?? "Empty" },
          { "minAmount", 1 },
          { "maxAmount", 1 },
          { "chance", 1f },
        };

        // Add the item object to the itemsList
        itemsList.Add(itemObject);
      }

      // Default structure for this inventory
      Plugin.CustomLoot[name] = new JObject
      {
        { "enabled", false },
        { "replace", false },
        { "items", itemsList },
      };
      Plugin.SaveLoot = true;
    }

    if (!Plugin.LootModification.Value) return;

    // Iterate over each inventory in CustomLoot
    var lootEntry = Plugin.CustomLoot.Properties().FirstOrDefault(x => x.Name == name);
    var lootItems = lootEntry.Value["items"];
    var replaceSlot = lootEntry.Value["replace"];

    if (!(bool)lootEntry.Value["enabled"]) return;

    for (var i = 0; i < instance.slots.Count; i++)
    {
      if (!(bool)replaceSlot && instance.slots[i] != null) continue;

      // Get the item data for the current slot
      // lootItems can have less or more than this inventories slots
      JObject itemData;
      try
      {
        itemData = (JObject)lootItems[i];
      }
      catch
      {
        continue;
      }
      var itemName = itemData["item"].ToString();
      var chance = (float)itemData["chance"];

      if (itemName == "Empty") continue;

      // Determine if the item should be added based on its chance
      if (UnityEngine.Random.value <= chance)
      {
        // Add the item to the inventories slots
        var amount = UnityEngine.Random.Range((int)itemData["minAmount"], (int)itemData["maxAmount"] + 1);
        instance.slots[i] = new() { item = ItemsDatabase.Instance.getItem(itemName, false), itemAmount = amount };
      }
    }
  }
}