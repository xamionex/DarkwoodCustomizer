using HarmonyLib;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace DarkwoodCustomizer;

[HarmonyPatch]
internal static class FlamethrowerPatch
{
    [HarmonyPatch(typeof(Flame), "onCollideWith")]
    [HarmonyPrefix]
    // ReSharper disable once InconsistentNaming
    private static void Prefix_FlameContact(Flame __instance)
    {
        if (!Plugin.ItemsModification.Value) return;

        if (Player.Instance.currentItem == null || Player.Instance.currentItem.type != "flamethrower") return;
        if (!Plugin.CustomItems.TryGetValue("flamethrower", out var itemData) || itemData is not JObject data) return;
        var contactDmgToken = data["flamethrowerContactDamage"];
        if (contactDmgToken != null)
        {
            AccessTools.Field(typeof(Flame), "contactDamage").SetValue(__instance, contactDmgToken.Value<int>());
        }
    }

    [HarmonyPatch]
    internal static class BurnTickPatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod()
        {
            // This finds the hidden inner class created by the yield return in burnTick
            return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(Burn), "burnTick"));
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (var i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Callvirt && codes[i].operand is MethodInfo { Name: "getHit" })
                {
                    codes.Insert(i - 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FlamethrowerPatch), nameof(GetCustomBurnDamage))));
                }
            }

            return codes;
        }
    }

    public static float GetCustomBurnDamage(float originalModifier)
    {
        if (!Plugin.ItemsModification.Value || !Plugin.CustomItems.TryGetValue("flamethrower", out var itemData) ||
            itemData is not JObject data) return originalModifier;
        var burnDmgToken = data["flamethrowerBurnDamage"];
        return burnDmgToken?.Value<float>() ?? originalModifier;
    }
}