using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace DarkwoodCustomizer;

public class InventoryPatch
{
	[HarmonyPatch(typeof(Inventory), nameof(Inventory.show))]
	[HarmonyTranspiler]
	public static IEnumerable<CodeInstruction> InventoryShow(IEnumerable<CodeInstruction> instructions)
	{
		var codes = new List<CodeInstruction>(instructions);
		var targetMethod = GetWorkbenchCraftingOffset();

		for (int i = 0; i < codes.Count; i++)
		{
			// replaces 670 offset for workbench crafting with 1000
			if (codes[i].opcode == OpCodes.Ldc_R4 && codes[i].operand.ToString() == "670")
			{
				codes[i] = Transpilers.EmitDelegate(GetWorkbenchCraftingOffset);
				Plugin.Log.LogInfo("Patched offset of workbench window");
				break;
			}
		}
		return codes;
	}

	public static float GetWorkbenchCraftingOffset()
	{
		return Plugin.CraftingOffset.Value; // Return the actual value
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
					if (Plugin.CraftingSlots.Value)
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