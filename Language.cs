using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace DarkwoodCustomizer;

internal class LanguagePatch
{
  [HarmonyPatch(typeof(Language), nameof(Language.Get), [typeof(string), typeof(string)])]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void LanguageGet(string key, string sheetTitle, ref string __result)
  {
    var cleankey = key.Replace("_name", "").Replace("_desc", "");
    if (Plugin.CustomItemsUseDefaults.Value && Plugin.DefaultCustomItems[cleankey] != null)
    {
      var data = (JObject)Plugin.DefaultCustomItems[cleankey];
      var name = data["name"]?.Value<string>();
      var desc = data["description"]?.Value<string>();
      if (key.EndsWith("_name") && name != null && name != key) __result = name;
      if (key.EndsWith("_desc") && desc != null && desc != key) __result = desc;
    }
    if (Plugin.CustomItems[cleankey] != null)
    {
      var data = (JObject)Plugin.CustomItems[cleankey];
      var name = data["name"]?.Value<string>();
      var desc = data["description"]?.Value<string>();
      if (key.EndsWith("_name"))
      {
        if (name == null)
        {
          if (__result == key) data["name"] = "";
          else data["name"] = __result;
          Plugin.SaveItems = true;
        }
        name = data["name"]?.Value<string>();
        if (name != null && name != key) __result = name;
      }

      if (key.EndsWith("_desc"))
      {
        if (desc == null)
        {
          if (__result == key) data["description"] = "";
          else data["description"] = __result;
          Plugin.SaveItems = true;
        }
        desc = data["description"]?.Value<string>();
        if (desc != null && desc != key) __result = desc;
      }
    }
    __result ??= "Unset Name Property";
  }

  [HarmonyPatch(typeof(Language), nameof(Language.Get), [typeof(string)])]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void LanguageGet(string key, ref string __result)
  {
    var cleanKey = key.Replace("_name", "").Replace("_desc", "");
    if (Plugin.CustomItems[cleanKey] != null)
    {
      var data = (JObject)Plugin.CustomItems[cleanKey];
      if (key.EndsWith("_name"))
      {
        var name = data["name"]?.Value<string>();
        if (name == null | name == $"{key}_name")
        {
          data["name"] = __result;
          Plugin.SaveJsonFile(Plugin.CustomItemsPath, Plugin.CustomItems);
        }
      }

      if (key.EndsWith("_desc"))
      {
        var description = data["description"]?.Value<string>();
        if (description == null | description == $"{key}_desc")
        {
          data["description"] = __result;
          Plugin.SaveJsonFile(Plugin.CustomItemsPath, Plugin.CustomItems);
        }
      }
    }
    __result ??= "Unset Name Property";
  }
  
  [HarmonyPatch(typeof(Language), nameof(Language.HasKey), typeof(string), typeof(string))]
  [HarmonyPostfix]
  // ReSharper disable once InconsistentNaming
  public static void LanguageHasKey(string key, string sheetTitle, ref bool __result)
  {
    var cleanKey = key.Replace("_name", "").Replace("_desc", "");
    if (Plugin.CustomItems.TryGetValue(cleanKey, out var data) && (data["name"] != null || data["description"] != null) || Plugin.DefaultCustomItems.TryGetValue(cleanKey, out data) && (data["name"] != null || data["description"] != null)) __result = true;
  }
}
