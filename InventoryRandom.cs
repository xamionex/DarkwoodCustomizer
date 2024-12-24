using System.Linq;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

internal class InventoryRandomizePatch
{
  [HarmonyPatch(typeof(InventoryRandom), nameof(InventoryRandom.randomize))]
  [HarmonyPostfix]
  static void PatchRandomizedInventory(InventoryRandom instance)
  {
    var customRandomInventories = Plugin.CustomRandomInventories;
    if (!customRandomInventories.ContainsKey(instance.name))
    {
      customRandomInventories[instance.name] = new JObject
            {
                { "presets", new JObject() }
            };
      Plugin.SaveRandomInventories = true;
    }
    for (var i = 0; i < instance.presets.Count; i++)
    {
      if (instance.presets[i] == null)
      {
        continue;
      }

      var token = customRandomInventories[instance.name]["presets"][i.ToString()];
      if (token == null || token.Type != JTokenType.Object)
      {
        customRandomInventories[instance.name]["presets"][i.ToString()] = new JObject();
        foreach (var item in instance.presets[i].permittedItems)
        {
          if (item.type == null)
          {
            Plugin.Log.LogError($"[CustomRandomInventories] Preset {i} in {instance.name} has an item ({item}) with no type, skipping");
            continue;
          }
          customRandomInventories[instance.name]["presets"][i.ToString()][item.type.name] = new JObject
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

      instance.presets[i].permittedItems = [];
      foreach (var item in (JObject)customRandomInventories[instance.name]["presets"][i.ToString()])
      {

        var typeName = item.Value["type"]?.Value<string>();
        if (typeName == null)
        {
          Plugin.Log.LogError($"[CustomRandomInventories] Preset {i} in {instance.name} has an item with a null type, skipping");
          continue;
        }
        var type = ItemsDatabase.Instance.getItem(typeName, false);
        if (type == null)
        {
          Plugin.Log.LogError($"[CustomRandomInventories] Preset {i} in {instance.name} has an item with an invalid type, skipping");
          continue;
        }
        instance.presets[i].permittedItems.Add(new PermittedItem
        {
          type = type,
          amountMin = item.Value["amountMin"]?.Value<int>() ?? 0,
          amountMax = item.Value["amountMax"]?.Value<int>() ?? 0,
          chance = item.Value["chance"]?.Value<float>() ?? 0
        });
      }

      instance.excludeFromDifficultyRandomizer = true;

      instance.presets[i].allowedItems = [];
      instance.presets[i].allowedItems.AddRange(instance.presets[i].permittedItems.Select(item => item.type));
    }
  }
}