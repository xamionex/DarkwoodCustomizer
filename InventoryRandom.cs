using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

internal class InventoryRandomizePatch
{
    [HarmonyPatch(typeof(InventoryRandom), nameof(InventoryRandom.randomize))]
    [HarmonyPostfix]
    static void PatchRandomizedInventory(InventoryRandom __instance)
    {
        var CustomRandomInventories = Plugin.CustomRandomInventories;
        if (!CustomRandomInventories.ContainsKey(__instance.name))
        {
            CustomRandomInventories[__instance.name] = new JObject
            {
                { "presets", new JObject() }
            };
            Plugin.SaveRandomInventories = true;
        }
        for (int i = 0; i < __instance.presets.Count; i++)
        {
            if (__instance.presets[i] == null)
            {
                continue;
            }

            var token = CustomRandomInventories[__instance.name]["presets"][i.ToString()];
            if (token == null || token.Type != JTokenType.Object)
            {
                CustomRandomInventories[__instance.name]["presets"][i.ToString()] = new JObject();
                foreach (var item in __instance.presets[i].permittedItems)
                {
                    if (item.type == null)
                    {
                        Plugin.Log.LogError($"[CustomRandomInventories] Preset {i} in {__instance.name} has an item ({item}) with no type, skipping");
                        continue;
                    }
                    CustomRandomInventories[__instance.name]["presets"][i.ToString()][item.type.name] = new JObject
                    {
                        { "type", item.type.name },
                        { "amountMin", item.amountMin },
                        { "amountMax", item.amountMax },
                        { "chance", item.chance }
                    };
                }
                Plugin.SaveRandomInventories = true;
            }

            if (!Plugin.RandomInventoriesModification.Value) return;

            __instance.presets[i].permittedItems = [];
            foreach (var item in (JObject)CustomRandomInventories[__instance.name]["presets"][i.ToString()])
            {

                var typeName = item.Value["type"]?.Value<string>();
                if (typeName == null)
                {
                    Plugin.Log.LogError($"[CustomRandomInventories] Preset {i} in {__instance.name} has an item with a null type, skipping");
                    continue;
                }
                var type = ItemsDatabase.Instance.getItem(typeName, false);
                if (type == null)
                {
                    Plugin.Log.LogError($"[CustomRandomInventories] Preset {i} in {__instance.name} has an item with an invalid type, skipping");
                    continue;
                }
                __instance.presets[i].permittedItems.Add(new PermittedItem
                {
                    type = type,
                    amountMin = item.Value["amountMin"]?.Value<int>() ?? 0,
                    amountMax = item.Value["amountMax"]?.Value<int>() ?? 0,
                    chance = item.Value["chance"]?.Value<float>() ?? 0
                });
            }

            __instance.excludeFromDifficultyRandomizer = true;

            __instance.presets[i].allowedItems = [];
            __instance.presets[i].allowedItems.AddRange(__instance.presets[i].permittedItems.Select(item => item.type));
        }
    }
}