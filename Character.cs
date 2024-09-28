using System;
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
        if (Plugin.LogCharacters.Value)
        {
            if (!CustomCharactersList.Contains(name))
            {
                CustomCharactersList.Add(name);
                Plugin.Log.LogInfo($"[CHARACTER] {name}: ({__instance.chaseSpeed}RS), ({__instance.idleWalkSpeed}WS), ({__instance.maxHealth}MaxHP)");
            }
        }
        if (!Plugin.CustomCharacters.ContainsKey(name))
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
            if (Plugin.LogCharacters.Value) Plugin.Log.LogInfo($"[CHARACTER] {name}: Adding to CustomCharacters since it didn't exist");
            File.WriteAllText(Plugin.CustomCharactersPath, JsonConvert.SerializeObject(Plugin.CustomCharacters, Formatting.Indented));
        }
        if (!Plugin.CharacterModification.Value) return;
        if (Plugin.CustomCharacters.TryGetValue(name, out var Stats) && Stats != null)
        {
            float MaxHealth = float.Parse(Stats["Health"]?.ToString() ?? "1");
            if (MaxHealth != __instance.maxHealth)
            {
                __instance.maxHealth = MaxHealth;
                if (UpdateHealth) __instance.health = MaxHealth;
                Plugin.Log.LogInfo($"[CHARACTER] {name}: Health set to {MaxHealth}");
            }
            float WalkSpeed = float.Parse(Stats["WalkSpeed"]?.ToString() ?? "1");
            if (WalkSpeed != __instance.idleWalkSpeed)
            {
                __instance.idleWalkSpeed = WalkSpeed;
                Plugin.Log.LogInfo($"[CHARACTER] {name}: Walk speed set to {WalkSpeed}");
            }
            float ChaseSpeed = float.Parse(Stats["RunSpeed"]?.ToString() ?? "1");
            if (ChaseSpeed != __instance.chaseSpeed)
            {
                __instance.chaseSpeed = ChaseSpeed;
                Plugin.Log.LogInfo($"[CHARACTER] {name}: Chase speed set to {ChaseSpeed}");
            }
            var Attacks = Stats["Attacks"] as JArray;
            if (Attacks != null)
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
                            var damage = Attack[$"{i + 1}"]?["Damage"]?.ToObject<int>() ?? 1;
                            if (damage != __instance.sensorTypes[i].damage)
                            {
                                __instance.sensorTypes[i].damage = damage;
                                Plugin.Log.LogInfo($"[CHARACTER] {name}: Attack {i + 1} damage set to {damage}");
                            }
                        }
                    }
                }
            }
        }
    }
}
