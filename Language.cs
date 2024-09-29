using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

public class LanguagePatch
{
    [HarmonyPatch(typeof(Language), nameof(Language.Get), [typeof(string), typeof(string)])]
    [HarmonyPostfix]
    public static void LanguageGet(string key, string sheetTitle, ref string __result)
    {
        var cleankey = key.Replace("_name", "").Replace("_desc", "");
        if (Plugin.CustomItemsUseDefaults.Value && Plugin.DefaultCustomItems[cleankey] != null)
        {
            var data = (JObject)Plugin.DefaultCustomItems[cleankey];
            if (key.EndsWith("_name")) __result = data["name"]?.Value<string>() ?? __result;
            if (key.EndsWith("_desc")) __result = data["description"]?.Value<string>() ?? __result;
        }
        if (Plugin.CustomItems[cleankey] != null)
        {
            var data = (JObject)Plugin.CustomItems[cleankey];
            if (key.EndsWith("_name")) __result = data["name"]?.Value<string>() ?? __result;
            if (key.EndsWith("_desc")) __result = data["description"]?.Value<string>() ?? __result;
        }
        __result ??= "Unset Name Property";
    }


    [HarmonyPatch(typeof(Language), nameof(Language.HasKey), typeof(string), typeof(string))]
    [HarmonyPostfix]
    public static void LanguageHasKey(string key, string sheetTitle, ref bool __result)
    {
        var cleankey = key.Replace("_name", "").Replace("_desc", "");
        if (Plugin.CustomItems.TryGetValue(cleankey, out var data) && (data["name"] != null || data["description"] != null))
            __result = true;
        else if (Plugin.DefaultCustomItems.TryGetValue(cleankey, out data) && (data["name"] != null || data["description"] != null))
            __result = true;
    }

}
