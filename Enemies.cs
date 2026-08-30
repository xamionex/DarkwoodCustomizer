using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class EnemiesPatch
{
  private static float _baseSpawnInterval = -1f;
  private static float _originalSpawnChance;

  // Night spawn chance: how often the night spawner ticks.
  // The interval is divided by the chance, so 2 ticks twice as often, 0.5 half as often.
  [HarmonyPatch(typeof(CharacterSpawner), "init")]
  [HarmonyPostfix]
  public static void CharacterSpawnerInitPostfix(CharacterSpawner __instance)
  {
    _baseSpawnInterval = __instance.spawnInterval;
    ApplyNightSpawnChance(__instance);
  }

  public static void RefreshNightSpawnChance()
  {
    if (Singleton<CharacterSpawner>.Instance == null) return;
    ApplyNightSpawnChance(Singleton<CharacterSpawner>.Instance);
  }

  private static void ApplyNightSpawnChance(CharacterSpawner spawner)
  {
    if (_baseSpawnInterval <= 0f) return;
    var chance = Plugin.CreatureSpawnChanceNight.Value;
    if (chance <= 0f) return;
    spawner.spawnInterval = _baseSpawnInterval / chance;
  }

  // Night enemy multiplier. The first spawn tick caches the vanilla amounts of the night scenario, every tick then applies amount = base * multiplier.
  private static readonly Dictionary<NightScenario.CharacterToSpawn, int> _nightBaseAmounts = new();

  [HarmonyPatch(typeof(CharacterSpawner), "spawnNightChar")]
  [HarmonyPrefix]
  public static void NightEnemyMultiplierPrefix()
  {
    var mult = Plugin.CreatureEnemyMultiplierNight.Value;
    var scenario = Singleton<NightScenarios>.Instance?.currentScenario;
    if (scenario == null) return;
    foreach (var characterToSpawn in scenario.characters)
    {
      if (characterToSpawn == null) continue;
      if (!_nightBaseAmounts.TryGetValue(characterToSpawn, out var baseAmount))
      {
        baseAmount = characterToSpawn.amount;
        _nightBaseAmounts[characterToSpawn] = baseAmount;
      }
      characterToSpawn.amount = (int)(baseAmount * mult);
    }
    if (mult == 1f || scenario.characters.Count <= 1) return;
    // Vanilla spawns one creature per tick and always picks the first entry that is not at its cap, so with multiplied amounts the first entry's budget eats the whole night and the other types never spawn.
    // Rotating the list one step per tick round-robins the spawns across all types.
    var first = scenario.characters[0];
    scenario.characters.RemoveAt(0);
    scenario.characters.Add(first);
  }

  // Day spawn chance: scales the spawn roll of day spawn points.
  [HarmonyPatch(typeof(CharacterSpawnPoint), "actuallySpawn")]
  [HarmonyPrefix]
  public static void DaySpawnChancePrefix(CharacterSpawnPoint __instance)
  {
    _originalSpawnChance = __instance.spawnChance;
    var chance = Plugin.CreatureSpawnChanceDay.Value;
    if (chance <= 0f) return;
    __instance.spawnChance = Mathf.Clamp01(_originalSpawnChance * chance);
  }

  [HarmonyPatch(typeof(CharacterSpawnPoint), "actuallySpawn")]
  [HarmonyPostfix]
  public static void DaySpawnChancePostfix(CharacterSpawnPoint __instance)
  {
    __instance.spawnChance = _originalSpawnChance;
  }

  // Day enemy multiplier, free roaming path: WorldChunk.spawnChars is a coroutine, so the amounts are scaled on its generated MoveNext.
  // The first coroutine caches the vanilla amounts of the shared Biome, all coroutines then apply amount = base * multiplier.
  private static readonly Dictionary<CharacterToSpawn, int> _freeRoamingBaseAmounts = new();

  [HarmonyPatch]
  private static class FreeRoamingMultiplierPatch
  {
    private static MethodBase TargetMethod()
    {
      return AccessTools.Method(AccessTools.TypeByName("WorldChunk+<spawnChars>d__31"), "MoveNext");
    }

    [HarmonyPrefix]
    private static void Prefix(object __instance)
    {
      var mult = Plugin.CreatureEnemyMultiplierDay.Value;
      var chunk = (WorldChunk)AccessTools.Field(__instance.GetType(), "<>4__this").GetValue(__instance);
      if (chunk?.biome == null) return;
      foreach (var characterToSpawn in chunk.biome.characters)
      {
        if (characterToSpawn == null || characterToSpawn.useCharacterSpawnPoint) continue;
        if (!_freeRoamingBaseAmounts.TryGetValue(characterToSpawn, out var baseAmount))
        {
          baseAmount = characterToSpawn.amount;
          _freeRoamingBaseAmounts[characterToSpawn] = baseAmount;
        }
        characterToSpawn.amount = (int)(baseAmount * mult);
      }
    }
  }

  // Day enemy multiplier, spawn point path: WorldGenerator.BigBiome spawns global characters in a regular method, same base amount cache pattern.
  private static readonly Dictionary<CharacterToSpawn, int> _globalBaseAmounts = new();

  [HarmonyPatch(typeof(WorldGenerator.BigBiome), "spawnGlobalCharacters")]
  [HarmonyPrefix]
  public static void DayGlobalMultiplierPrefix(WorldGenerator.BigBiome __instance)
  {
    var mult = Plugin.CreatureEnemyMultiplierDay.Value;
    if (Singleton<WorldGenerator>.Instance == null) return;
    var preset = Singleton<WorldGenerator>.Instance.getBiomePreset(__instance.type);
    if (preset == null) return;
    foreach (var characterToSpawn in preset.characters)
    {
      if (characterToSpawn == null || !characterToSpawn.useCharacterSpawnPoint) continue;
      if (!_globalBaseAmounts.TryGetValue(characterToSpawn, out var baseAmount))
      {
        baseAmount = characterToSpawn.amount;
        _globalBaseAmounts[characterToSpawn] = baseAmount;
      }
      characterToSpawn.amount = (int)(baseAmount * mult);
    }
  }

  // Creatures always know where the player is: every frame, creatures that would attack the player keep relentless pursuit of their live position, so they hunt the player down even through walls.
  [HarmonyPatch(typeof(Character), nameof(Character.Update))]
  [HarmonyPostfix]
  public static void CharacterUpdatePostfix(Character __instance)
  {
    if (!Plugin.CreaturesKnowPlayerPosition.Value) return;
    if (__instance == null || Player.Instance == null) return;
    if (__instance.dummy || __instance.npc != null || __instance.invisible) return;
    if (!__instance.isActive || !__instance.alive || __instance.dying) return;
    if (Player.Instance.invisible) return;
    if (!__instance.attacksFaction(Player.Instance.faction)) return;
    __instance.relentlessPursuit = true;
    if (__instance.target == Player.Instance._transform && __instance.behaviour == Character.Behaviour.chasingTarget) return;
    __instance.attackPlayer();
  }
}
