using HarmonyLib;
using System;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class InventoryPatch
{
	[HarmonyPatch(typeof(Inventory), nameof(Inventory.show))]
	[HarmonyPostfix]
	public static void InventoryShow(Inventory __instance, string labelName = "")
	{
		GameObject gameObject = __instance.thisUI;
		PositionMe positionMe = gameObject.GetComponent<PositionMe>();
		if (Plugin.WorkbenchInventoryModification.Value && labelName == "Storage")
		{
			positionMe.offset = new Vector2(__instance.position.x + Plugin.StorageXOffset.Value, __instance.position.z + Plugin.StorageZOffset.Value);
		}
		if (Plugin.CraftingModification.Value && __instance.invType == Inventory.InvType.crafting)
		{
			positionMe.offset = new Vector2(__instance.position.x + Plugin.CraftingXOffset.Value, __instance.position.z + Plugin.CraftingZOffset.Value);
			UpgradeItemMenuPatch.craftingPos.x = positionMe.offset.x;
			UpgradeItemMenuPatch.craftingPos.y = positionMe.offset.y;
			if (__instance.isWorkbench)
			{
				foreach (var child in gameObject.GetComponentsInChildren<Transform>())
				{
					if (child.name == "WorkbenchBackground")
					{
						UnityEngine.Object.Destroy(child.gameObject);
						float extraSlots = Plugin.CraftingRightSlots.Value - 5f;
						float xPosition = 119f + 31f * extraSlots;
						float xScale = 1f + 0.21f * extraSlots;
						GameObject workbenchBackground = Core.AddPrefab("UI/WorkbenchBackground", new Vector3(xPosition, -10f, -233f), Quaternion.Euler(90f, 0f, 0f), gameObject.gameObject, false);
						workbenchBackground.transform.localScale = new Vector3(xScale, 1f, 1f);
						Singleton<InventoryController>.Instance.repairBtn.transform.position = gameObject.transform.position + new Vector3(xPosition, 5f, -409f);
						Singleton<InventoryController>.Instance.upgradeBtn.transform.position = gameObject.transform.position + new Vector3(xPosition, 5f, -451f);
						Singleton<InventoryController>.Instance.upgradeWorkbenchBtn.transform.position = gameObject.transform.position + new Vector3(xPosition, 5f, -503f);
						break;
					}
				}
			}
		}
	}

	[HarmonyPatch(typeof(Inventory), nameof(Inventory.show))]
	[HarmonyPrefix]
	public static void ModifyInventorySlots(Inventory __instance, string labelName = "")
	{
		int maxSlots = 0;

		if (labelName == "Storage")
		{
			if (Plugin.WorkbenchInventoryModification.Value)
			{
				__instance.maxColumns = Plugin.RightSlots.Value;
				maxSlots = __instance.maxColumns * Plugin.DownSlots.Value;
			}
			else
			{
				__instance.maxColumns = 6;
				maxSlots = 48; // 6*8
			}
		}
		else
		{
			switch (__instance.invType)
			{
				case Inventory.InvType.crafting:
					if (Plugin.CraftingModification.Value)
					{
						__instance.maxColumns = Plugin.CraftingRightSlots.Value;
						maxSlots = __instance.maxColumns * Plugin.CraftingDownSlots.Value;
					}
					else
					{
						__instance.maxColumns = 5;
						maxSlots = 35; // 5*7
					}
					break;
				case Inventory.InvType.hotbar:
					if (Plugin.HotbarSlots.Value)
					{
						__instance.maxColumns = Plugin.HotbarRightSlots.Value;
						maxSlots = __instance.maxColumns * Plugin.HotbarDownSlots.Value;
					}
					else
					{
						__instance.maxColumns = 1;
						maxSlots = 3 + Player.Instance.hotbarUpgrades;
					}
					break;
				case Inventory.InvType.playerInv:
					if (Plugin.InventorySlots.Value)
					{
						__instance.maxColumns = Plugin.InventoryRightSlots.Value;
						maxSlots = __instance.maxColumns * Plugin.InventoryDownSlots.Value;
					}
					else
					{
						__instance.maxColumns = 2;
						maxSlots = 12 + (2 * Player.Instance.inventoryUpgrades);
					}
					break;
			}
		}


		if (maxSlots > 0 && maxSlots != __instance.slots.Count)
		{
			if (Plugin.LogDebug.Value)
			{
				Plugin.LogDivider();
				Plugin.Log.LogInfo($"Player has {Player.Instance.inventoryUpgrades} inventory upgrades and {Player.Instance.hotbarUpgrades} hotbar upgrades.");
			}
			ModifySlots(__instance, maxSlots);
		}
	}
	public static void ModifySlots(Inventory __instance, int MaximumSlots)
	{
		int difference = MaximumSlots - __instance.slots.Count;
		if (Plugin.LogDebug.Value)
		{
			Plugin.LogDivider();
			Plugin.Log.LogInfo($"Maximum slots allowed in \"{__instance.name}\" is {MaximumSlots} and we have {__instance.slots.Count}");

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
			for (int i = MaximumSlots - 1; i < __instance.slots.Count; i++)
			{
				__instance.slots.RemoveAt(i - 1);
				difference++;
			}
		}
		while (difference > 0)
		{
			for (int i = 0; i < difference; i++)
			{
				__instance.slots.Add(new InvSlot());
				difference--;
			}
		}
		return;
	}
}