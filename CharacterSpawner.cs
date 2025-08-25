using HarmonyLib;
using System;
using UnityEngine;

namespace DarkwoodCustomizer;

internal class CharacterSpawnerPatch
{
    [HarmonyPatch(typeof(CharacterSpawner), "waitToSpawnWorm")]
    [HarmonyPrefix]
    public static bool WormSpawnPrefix()
    {
        return !Plugin.DisableWormSpawn.Value;
    }
}