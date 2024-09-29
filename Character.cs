using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

public class CharacterPatch
{
    public static List<string> CustomCharactersList = [];

    private static readonly Dictionary<string, (Character.SensorType[], int[])> loggedDamage = new();

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

    public static void SetCharacterValues(Character __instance, bool UpdateHealth = false)
    {
        string name = __instance.name;
        if (name.Contains("(Clone)"))
        {
            name = name.Substring(0, name.IndexOf("(Clone)"));
        }
        AddMissingStats(__instance, name);
        if (!Plugin.CharacterModification.Value) return;
        if (!Plugin.CustomCharacters.TryGetValue(name, out var Stats) && Stats != null) return;
        UpdateCharacterValues(__instance, Stats, name, UpdateHealth);
    }

    public static void UpdateCharacterValues(Character __instance, JToken Stats, string name, bool UpdateHealth = false)
    {
        float MaxHealth = float.Parse(Stats["Health"]?.ToString() ?? "1");
        if (MaxHealth != __instance.maxHealth)
        {
            __instance.maxHealth = MaxHealth;
            if (UpdateHealth) __instance.health = MaxHealth;
        }
        float WalkSpeed = float.Parse(Stats["WalkSpeed"]?.ToString() ?? "1");
        if (WalkSpeed != __instance.idleWalkSpeed)
        {
            __instance.idleWalkSpeed = WalkSpeed;
        }
        float ChaseSpeed = float.Parse(Stats["RunSpeed"]?.ToString() ?? "1");
        if (ChaseSpeed != __instance.chaseSpeed)
        {
            __instance.chaseSpeed = ChaseSpeed;
        }
        if (Stats["Attacks"] is JArray Attacks)
        {
            if (Attacks.Count > __instance.sensorTypes.Count)
            {
                Plugin.Log.LogError($"[CHARACTER] {name} has only {__instance.sensorTypes.Count} attacks but the CustomCharacters file has {Attacks.Count}! This won't work, Skipping!");
            }
            else
            {
                for (int i = 0; i < Attacks.Count; i++)
                {
                    var Attack = Attacks[i] as JObject;
                    if (Attack != null)
                    {
                        var attack = Attack[$"{i + 1}"] as JObject;
                        var damage = attack["Damage"]?.ToObject<int>() ?? null;
                        if (damage == null)
                        {
                            attack["AttackName(ReadOnly)"] = __instance.sensorTypes[i].name;
                            attack["AttackIsRanged(ReadOnly)"] = __instance.sensorTypes[i].isRanged;
                            attack["Damage"] = __instance.sensorTypes[i].damage;
                            damage = __instance.sensorTypes[i].damage;
                            File.WriteAllText(Plugin.CustomCharactersPath, JsonConvert.SerializeObject(Plugin.CustomCharacters, Formatting.Indented));
                        }
                        if (damage == null)
                        {
                            Plugin.Log.LogError($"[CHARACTER] {name} has a corrupted attack damage, skipping!");
                            return;
                        };
                        if (damage != __instance.sensorTypes[i].damage)
                        {
                            __instance.sensorTypes[i].damage = (int)damage;
                        }
                    }
                }
            }
        }
    }

    private static void AddMissingStats(Character __instance, string name)
    {
        bool Changed = false;
        // Corrects the character stats if they are missing
        if (Plugin.CustomCharacters.ContainsKey(name))
        {
            JObject charStats = (JObject)Plugin.CustomCharacters[name];
            if (!charStats.ContainsKey("Health"))
            {
                charStats["Health"] = __instance.maxHealth;
                Changed = true;
            }
            if (!charStats.ContainsKey("WalkSpeed"))
            {
                charStats["WalkSpeed"] = __instance.idleWalkSpeed;
                Changed = true;
            }
            if (!charStats.ContainsKey("RunSpeed"))
            {
                charStats["RunSpeed"] = __instance.chaseSpeed;
                Changed = true;
            }
            if (!charStats.ContainsKey("Attacks") && __instance.sensorTypes.Count > 0)
            {
                charStats["Attacks"] = JArray.FromObject(__instance.sensorTypes.Select((s, i) => new JObject
                {
                    { $"{i + 1}", new JObject
                        {
                            { "AttackName(ReadOnly)", s.name },
                            { "AttackIsRanged(ReadOnly)", s.isRanged },
                            { "Damage", s.damage }
                        }
                    }
                }).ToArray());
                Changed = true;
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
                        { "Damage", s.damage }
                    }
                }
            }).ToArray()
            });
            Changed = true;
        }
        if (Changed) File.WriteAllText(Plugin.CustomCharactersPath, JsonConvert.SerializeObject(Plugin.CustomCharacters, Formatting.Indented));
    }
}
