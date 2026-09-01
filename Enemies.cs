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

  // Night enemy multiplier. The first spawn tick caches the vanilla amounts of the night scenario.
  // When the multiplier is active the vanilla spawner is replaced: each tick spawns `mult` creatures, each a random type that is not yet at its cap.
  // This gives every creature type a fair share instead of the first list entry eating the whole night.
  private static readonly Dictionary<NightScenario.CharacterToSpawn, int> _nightBaseAmounts = new();

  [HarmonyPatch(typeof(CharacterSpawner), "spawnNightChar")]
  [HarmonyPrefix]
  public static bool NightEnemyMultiplierPrefix(CharacterSpawner __instance)
  {
    var mult = Plugin.CreatureEnemyMultiplierNight.Value;
    if (mult == 1f) return true; // vanilla behavior
    var scenario = Singleton<NightScenarios>.Instance?.currentScenario;
    if (scenario == null) return true;

    // Cache the vanilla amounts and scale the caps so the multiplied budget can actually be reached.
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

    // Replicate the vanilla spawnNightChar guards.
    if (Singleton<OutsideLocations>.Instance.playerInOutsideLocation || Singleton<Dreams>.Instance.dreaming || !__instance.spawnNocturnalCharacters)
    {
      return false;
    }
    if (Player.Instance.whereAmI.bigLocation == null || !Player.Instance.whereAmI.bigLocation.playerBase)
    {
      return false;
    }

    var spawnCount = Mathf.Max(1, (int)mult);
    for (var s = 0; s < spawnCount; s++)
    {
      var candidates = new List<NightScenario.CharacterToSpawn>();
      foreach (var characterToSpawn in scenario.characters)
      {
        if (characterToSpawn != null && !string.IsNullOrEmpty(characterToSpawn.characterName) && characterToSpawn.amount > 0 && characterToSpawn.spawned < characterToSpawn.amount)
        {
          candidates.Add(characterToSpawn);
        }
      }
      if (candidates.Count == 0) break;
      var pick = candidates[UnityEngine.Random.Range(0, candidates.Count)];
      var character = __instance.spawnCharacterAround(Player.Instance.gameObject, Vector3.zero, 1500f, pick.characterName, true, false, false, false);
      if (character == null) continue;
      var chapterTwo = (!Core.randomGeneration && GameObject.Find(Helpers.GetSceneName()).GetComponent<Location>().chapterId > 1)
                       || (Core.randomGeneration && Singleton<WorldGenerator>.Instance.chapterID > 1);
      if (chapterTwo && UnityEngine.Random.Range(0f, 1f) > 0.5f)
      {
        character.gameObject.AddComponent<ShadowArmor>();
      }
      pick.spawned++;
    }
    return false; // skip the vanilla single spawn
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
