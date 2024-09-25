using DarkwoodCustomizer;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

[HarmonyPatch]
public class InventoryPatch
{
	public delegate float GetWorkbenchCraftingOffsetDelegate();

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
				break;
			}
		}
		return codes;
	}

	public static float GetWorkbenchCraftingOffset()
	{
		return Plugin.WorkbenchCraftingOffset.Value; // Return the actual value
	}

	[HarmonyPatch(typeof(Inventory), nameof(Inventory.show))]
	[HarmonyPrefix]
	public static void ModifyInventorySlots(Inventory __instance, string labelName = "")
	{
		int maxSlots = 0;

		if (labelName == "Storage")
		{
			__instance.maxColumns = Plugin.RightSlots.Value;
			maxSlots = __instance.maxColumns * Plugin.DownSlots.Value;
		}
		else if (Plugin.HotbarSlots.Value && __instance.isHotbar())
		{
			__instance.maxColumns = Plugin.HotbarRightSlots.Value;
			maxSlots = __instance.maxColumns * Plugin.HotbarDownSlots.Value;
		}
		else if (Plugin.InventorySlots.Value && __instance.invType == Inventory.InvType.playerInv)
		{
			__instance.maxColumns = Plugin.InventoryRightSlots.Value;
			maxSlots = __instance.maxColumns * Plugin.InventoryDownSlots.Value;
		}
		else if (__instance.invType == Inventory.InvType.playerInv)
		{
			__instance.maxColumns = 2;
			maxSlots = 12 + (2 * Player.Instance.inventoryUpgrades);
		}
		else if (__instance.isHotbar())
		{
			__instance.maxColumns = 1;
			maxSlots = 3 + Player.Instance.hotbarUpgrades;
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
		if (difference < 0 && Plugin.RemoveExcess.Value)
		{
			__instance.slots.RemoveRange(MaximumSlots - 1, Math.Abs(difference));
		}
		else
		{
			__instance.slots.AddRange(Enumerable.Repeat(new InvSlot(), difference));
		}
		return;
	}
}