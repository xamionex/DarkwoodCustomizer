using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

internal class CharacterPatch
{
  public static List<string> CustomCharactersList = [];

  private static readonly Dictionary<string, (Character.SensorType[], int[])> LoggedDamage = new();

  [HarmonyPatch(typeof(Character), nameof(Character.Update))]
  [HarmonyPrefix]
  public static void CharUpdate(Character __instance)
  {
    SetCharacterValues(__instance);
  }

  [HarmonyPatch(typeof(Character), "Awake")]
  [HarmonyPrefix]
  public static void ChararcterAwake(Character __instance)
  {
    SetCharacterValues(__instance, true);
  }

  public static void SetCharacterValues(Character __instance, bool updateHealth = false)
  {
    var name = __instance.name;
    if (name.Contains("(Clone)"))
    {
      name = name.Substring(0, name.IndexOf("(Clone)"));
    }
    AddMissingStats(__instance, name);
    if (!Plugin.CharacterModification.Value) return;
    if (!Plugin.CustomCharacters.TryGetValue(name, out var stats)) return;
    UpdateCharacterValues(__instance, stats, name, updateHealth);
  }

  public static void UpdateCharacterValues(Character __instance, JToken stats, string name, bool updateHealth = false)
  {
    if (__instance == null) return;
    var maxHealth = stats["Health"]?.Value<float>() ?? __instance.maxHealth;
    if (maxHealth != __instance.maxHealth)
    {
      __instance.maxHealth = maxHealth;
      if (updateHealth) __instance.health = maxHealth;
    }
    var walkSpeed = stats["WalkSpeed"]?.Value<float>() ?? __instance.idleWalkSpeed;
    if (walkSpeed != __instance.idleWalkSpeed)
    {
      __instance.idleWalkSpeed = walkSpeed;
    }
    var chaseSpeed = stats["RunSpeed"]?.Value<float>() ?? __instance.chaseSpeed;
    if (chaseSpeed != __instance.chaseSpeed)
    {
      __instance.chaseSpeed = chaseSpeed;
    }
    if (stats["Attacks"] is JArray attacks)
    {
      if (attacks.Count > __instance.sensorTypes.Count)
      {
        Plugin.Log.LogError($"[CHARACTER] {name} has only {__instance.sensorTypes.Count} attacks but the CustomCharacters file has {attacks.Count}! This won't work, Skipping!");
      }
      else
      {
        for (var i = 0; i < attacks.Count; i++)
        {
          var attack = attacks[i] as JObject;
          if (attack != null)
          {
            var attackIndex = attack[$"{i + 1}"] as JObject;
            var damage = attackIndex["Damage"]?.ToObject<int>() ?? null;
            var barricadeDamage = attackIndex["BarricadeDamage"]?.ToObject<int>() ?? null;
            if (damage == null || barricadeDamage == null)
            {
              var newDamage = damage ?? __instance.sensorTypes[i]?.damage ?? null;
              var newBarricadeDamage = barricadeDamage ?? __instance.sensorTypes[i]?.barricadeDamage ?? null;
              attackIndex["AttackName(ReadOnly)"] = __instance.sensorTypes[i].name;
              attackIndex["AttackIsRanged(ReadOnly)"] = __instance.sensorTypes[i].isRanged;
              attackIndex["Damage"] = newDamage;
              attackIndex["BarricadeDamage"] = newBarricadeDamage;
              damage = newDamage;
              barricadeDamage = newBarricadeDamage;
              File.WriteAllText(Plugin.CustomCharactersPath, JsonConvert.SerializeObject(Plugin.CustomCharacters, Formatting.Indented));
            }
            switch ((damage == null, barricadeDamage == null))
            {
              case (true, true):
                Plugin.Log.LogWarning($"[CHARACTER] {name} has a corrupted attack damage AND barricade damage, this most likely happened because this charracter is peaceful, skipping!");
                break;
              case (true, false):
                Plugin.Log.LogWarning($"[CHARACTER] {name} has a corrupted attack damage, this most likely happened because this charracter isnt supposed to deal attack damage, skipping!");
                break;
              case (false, true):
                Plugin.Log.LogWarning($"[CHARACTER] {name} has a corrupted barricade damage, this most likely happened because this charracter isnt supposed to deal barricade damage, skipping!");
                break;
            };
            if (damage != null && damage != __instance.sensorTypes[i].damage)
            {
              __instance.sensorTypes[i].damage = (int)damage;
            }
            if (barricadeDamage != null && barricadeDamage != 0 && barricadeDamage != __instance.sensorTypes[i].barricadeDamage)
            {
              __instance.sensorTypes[i].barricadeDamage = (int)barricadeDamage;
            }
          }
        }
      }
    }
  }

  private static void AddMissingStats(Character __instance, string name)
  {
    var changed = false;
    // Corrects the character stats if they are missing
    if (Plugin.CustomCharacters.TryGetValue(name, out var character))
    {
      var charStats = (JObject)character;
      if (!charStats.ContainsKey("Health"))
      {
        charStats["Health"] = __instance.maxHealth;
        changed = true;
      }
      if (!charStats.ContainsKey("WalkSpeed"))
      {
        charStats["WalkSpeed"] = __instance.idleWalkSpeed;
        changed = true;
      }
      if (!charStats.ContainsKey("RunSpeed"))
      {
        charStats["RunSpeed"] = __instance.chaseSpeed;
        changed = true;
      }
      if (!charStats.ContainsKey("Attacks") && __instance.sensorTypes.Count > 0)
      {
        charStats["Attacks"] = JArray.FromObject(__instance.sensorTypes.Select((s, i) => new JObject
                {
                    { $"{i + 1}", new JObject
                        {
                            { "AttackName(ReadOnly)", s.name },
                            { "AttackIsRanged(ReadOnly)", s.isRanged },
                            { "Damage", s.damage },
                            { "BarricadeDamage", s.barricadeDamage }
                        }
                    }
                }).ToArray());
        changed = true;
      }
    }
    else
    {
      Plugin.CustomCharacters[name] = JObject.FromObject(new
      {
        Health = __instance.maxHealth,
        WalkSpeed = __instance.idleWalkSpeed,
        RunSpeed = __instance.chaseSpeed,
        Attacks = __instance.sensorTypes.Select((s, i) => new JObject
            {
                { $"{i + 1}", new JObject
                    {
                        { "AttackName(ReadOnly)", s.name },
                        { "AttackIsRanged(ReadOnly)", s.isRanged },
                        { "Damage", s.damage },
                        { "BarricadeDamage", s.barricadeDamage }
                    }
                }
            }).ToArray()
      });
      changed = true;
    }
    if (changed) Plugin.SaveCharacters = true;
  }
}
