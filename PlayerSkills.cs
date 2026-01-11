using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DarkwoodCustomizer
{
    internal class PlayerSkillsPatch
    {
        public static bool RefreshSkills = true;
        private static bool skillsAlreadyApplied = false;
        
        // Complete dictionary of all skills from the wiki with correct game object names
        private static readonly Dictionary<string, SkillInfo> AllSkills = new()
        {
            // Positive Skills
            { "EagleEye", new SkillInfo("farSight", "Passive", "Positive I") },
            { "Moth", new SkillInfo("moth", "Active", "Positive I") },
            { "Navigator", new SkillInfo("navigator", "Active", "Positive I") },
            { "MushroomHealing", new SkillInfo("mushroomHealing", "Passive", "Positive I") },
            
            { "AcidBlood", new SkillInfo("acidBlood", "Passive", "Positive II") },
            { "ThirdEye", new SkillInfo("thirdEye", "Active", "Positive II") },
            { "Runner", new SkillInfo("runner", "Active", "Positive II") },
            
            { "CarefulStep", new SkillInfo("carefulStep", "Passive", "Positive III") },
            { "Appetite", new SkillInfo("panda", "Passive", "Positive III") },
            { "Scream", new SkillInfo("scaryFace", "Active", "Positive III") },
            
            { "Adrenaline", new SkillInfo("strong", "Passive", "Positive IV") },
            { "Vitality", new SkillInfo("moreHealth", "Passive", "Positive IV") },
            { "Chameleon", new SkillInfo("ninja", "Active", "Positive IV") },
            
            // Negative Skills
            { "Shadows", new SkillInfo("nightShadows", "Passive", "Negative I") },
            { "PoisonVulnerability", new SkillInfo("poisonVulnerability", "Passive", "Negative II") },
            { "Fearful", new SkillInfo("narrowerFOV", "Passive", "Negative II") },
            { "WeakLungs", new SkillInfo("lessStamina", "Passive", "Negative III") },
            { "Weakness", new SkillInfo("weak", "Passive", "Negative III") },
            { "WeakRegeneration", new SkillInfo("weakHealthRegeneration", "Passive", "Negative IV") },
            { "ShakyHands", new SkillInfo("shakyHands", "Passive", "Negative IV") }
        };

        // Skills that are in the game files but not mapped (for logging)
        private static readonly List<string> UnmappedGameObjectSkills = new()
        {
            "badNavigator", 
            "clumsy",
            "cook",
            "dodge",
            "electrician",
            "enemyOfTheForest",
            "flatfoot",
            "friendOfTheForest",
            "hotbarPlus",
            "inventoryPlus",
            "lessHealth",
            "moreStamina",
            "perception1",
            "randomBleeding",
            "slow",
            "specialAttack",
            "vigilant",
            "widerFOV"
        };

        // Reverse mapping for easier lookup
        private static readonly Dictionary<string, string> GameObjectToConfigKey = new();

        static PlayerSkillsPatch()
        {
            // Build reverse mapping
            foreach (var kvp in AllSkills)
            {
                if (!string.IsNullOrEmpty(kvp.Value.GameObjectName) && 
                    !GameObjectToConfigKey.ContainsKey(kvp.Value.GameObjectName))
                {
                    GameObjectToConfigKey[kvp.Value.GameObjectName] = kvp.Key;
                }
            }
        }

        // ===================================================================
        // Main Patch Methods - Apply All Skill States
        // ===================================================================
        [HarmonyPatch(typeof(PlayerSkills), "initialize")]
        [HarmonyPostfix]
        private static void InitializeSkills(PlayerSkills __instance, bool resetTimesUsed = true)
        {
            if (!Plugin.PlayerSkillsModification.Value) return;
            
            // Prevent running multiple times
            if (skillsAlreadyApplied) return;
            
            ApplyAllSkillStates(__instance);
            RefreshSkills = false;
            skillsAlreadyApplied = true;
        }

        [HarmonyPatch(typeof(PlayerSkills), "Start")]
        [HarmonyPostfix]
        private static void StartSkills(PlayerSkills __instance)
        {
            if (!Plugin.PlayerSkillsModification.Value) return;
            
            // Reset the flag when starting a new game
            skillsAlreadyApplied = false;
        }

        // ===================================================================
        // Apply All Skill States
        // ===================================================================
        private static void ApplyAllSkillStates(PlayerSkills skills)
        {
            if (!Plugin.PlayerSkillsModification.Value) return;

            // Log all skills found in Resources
            LogAllAvailableSkills();

            // Clear and rebuild all skill lists
            RebuildSkillLists(skills);
            
            // Update UI and visual effects
            if (Player.Instance != null)
            {
                Player.Instance.updateHealthBar();
                Singleton<UI>.Instance.refreshLives();
            }

            // Log final skill states
            LogFinalSkillStates(skills);
        }

        private static void LogAllAvailableSkills()
        {
            var allSkills = Resources.LoadAll("Skills", typeof(GameObject));
            Plugin.Log.LogInfo("[Skills] All skills found in Resources:");
            
            foreach (var skillObj in allSkills)
            {
                if (skillObj is GameObject skillGameObject)
                {
                    var skill = skillGameObject.GetComponent<PlayerSkill>();
                    if (skill != null)
                    {
                        var configKey = GetConfigKeyFromGameObjectName(skillGameObject.name);
                        var status = string.IsNullOrEmpty(configKey) ? "UNMAPPED" : "MAPPED";
                        
                        Plugin.Log.LogInfo($"  - {skillGameObject.name}: " +
                                          $"Manual={skill.isActivatedManually}, " +
                                          $"Status={status}, " +
                                          $"Config={configKey ?? "N/A"}");
                    }
                }
            }
        }

        private static void RebuildSkillLists(PlayerSkills skills)
        {
            // Get all available skills from the game
            var allSkills = Resources.LoadAll("Skills", typeof(GameObject));
            
            // Track which skills we've already processed
            var processedGameObjects = new HashSet<string>();
            
            // Clear lists if empty
            if (skills.skills.Count == 0)
            {
                skills.skills.Clear();
                skills.manuallyActivatedSkills.Clear();
                skills.automaticActivatedSkills.Clear();
                skills.availableSkills.Clear();
            }

            // Add skills from Resources
            foreach (var skillObj in allSkills)
            {
                if (skillObj is GameObject skillGameObject)
                {
                    var skill = skillGameObject.GetComponent<PlayerSkill>();
                    if (skill == null) continue;

                    var gameObjectName = skillGameObject.name;
                    
                    // Skip if already processed
                    if (processedGameObjects.Contains(gameObjectName))
                        continue;
                    
                    processedGameObjects.Add(gameObjectName);
                    
                    var configKey = GetConfigKeyFromGameObjectName(gameObjectName);

                    if (string.IsNullOrEmpty(configKey))
                    {
                        // Skill not in our config map, skip it
                        continue;
                    }

                    // Check if skill should be enabled
                    var shouldBeEnabled = ShouldSkillBeEnabled(configKey);

                    if (shouldBeEnabled)
                    {
                        // Create a new instance of the skill
                        var skillInstance = CreateSkillInstance(skillGameObject, gameObjectName);
                        
                        if (skillInstance != null)
                        {
                            AddSkillToLists(skills, skillInstance);
                            
                            // Update boolean fields for skill effects
                            UpdateSkillBooleanField(skills, configKey, true);
                        }
                    }
                    else
                    {
                        // Skill is not enabled, set boolean field to false
                        UpdateSkillBooleanField(skills, configKey, false);
                    }
                }
            }

            // Update player stats for skills that affect them
            UpdatePlayerStats(skills);
        }

        private static PlayerSkill CreateSkillInstance(GameObject skillPrefab, string skillName)
        {
            // Create a new instance
            var skillInstance = GameObject.Instantiate(skillPrefab);
            skillInstance.name = skillName;
            skillInstance.SetActive(true);
            
            return skillInstance.GetComponent<PlayerSkill>();
        }

        private static void AddSkillToLists(PlayerSkills skills, PlayerSkill skill)
        {
            if (skill == null) return;

            var skillName = skill.gameObject.name;
            
            // Check if skill already exists
            if (skills.skills.Any(s => s != null && s.gameObject != null && s.gameObject.name == skillName))
            {
                if (Plugin.LogDebug.Value)
                {
                    Plugin.Log.LogInfo($"[Skills] Skipping duplicate skill: {skillName}");
                }
                return;
            }

            // Add to main skills list
            skills.skills.Add(skill);

            // Add to available skills
            skills.availableSkills.Add(skill);

            // Add to appropriate activation list
            if (skill.isActivatedManually)
            {
                skills.manuallyActivatedSkills.Add(skill);
            }
            else
            {
                skills.automaticActivatedSkills.Add(skill);
            }

            // Initialize the skill
            skill.chosen = true;
            skill.initialized = true;
            skill.timesUsed = 0;
        }

        private static string GetConfigKeyFromGameObjectName(string gameObjectName)
        {
            if (GameObjectToConfigKey.TryGetValue(gameObjectName, out var configKey))
            {
                return configKey;
            }
            return null;
        }

        private static bool ShouldSkillBeEnabled(string configKey)
        {
            return GetEnableConfigValue(configKey);
        }

        private static bool GetEnableConfigValue(string configKey)
        {
            if (!AllSkills.ContainsKey(configKey))
                return false;

            switch (configKey)
            {
                // Positive Skills
                case "EagleEye": return Plugin.PlayerSkillEagleEye.Value;
                case "Moth": return Plugin.PlayerSkillMoth.Value;
                case "Navigator": return Plugin.PlayerSkillNavigator.Value;
                case "MushroomHealing": return Plugin.PlayerSkillMushroomHealing.Value;
                
                case "AcidBlood": return Plugin.PlayerSkillAcidBlood.Value;
                case "ThirdEye": return Plugin.PlayerSkillThirdEye.Value;
                case "Runner": return Plugin.PlayerSkillRunner.Value;
                
                case "CarefulStep": return Plugin.PlayerSkillCarefulStep.Value;
                case "Appetite": return Plugin.PlayerSkillAppetite.Value;
                case "Scream": return Plugin.PlayerSkillScream.Value;
                
                case "Adrenaline": return Plugin.PlayerSkillAdrenaline.Value;
                case "Vitality": return Plugin.PlayerSkillVitality.Value;
                case "Chameleon": return Plugin.PlayerSkillChameleon.Value;
                
                // Negative Skills
                case "Shadows": return Plugin.PlayerSkillShadows.Value;
                case "PoisonVulnerability": return Plugin.PlayerSkillPoisonVulnerability.Value;
                case "Fearful": return Plugin.PlayerSkillFearful.Value;
                case "WeakLungs": return Plugin.PlayerSkillWeakLungs.Value;
                case "Weakness": return Plugin.PlayerSkillWeakness.Value;
                case "WeakRegeneration": return Plugin.PlayerSkillWeakRegeneration.Value;
                case "ShakyHands": return Plugin.PlayerSkillShakyHands.Value;
                
                default: return false;
            }
        }

        private static void UpdateSkillBooleanField(PlayerSkills skills, string configKey, bool value)
        {
            if (!AllSkills.TryGetValue(configKey, out var skillInfo))
                return;

            var fieldName = skillInfo.GameObjectName;
            
            // Special cases for skills with different field names
            switch (fieldName)
            {
                case "nightShadows":
                    fieldName = "NightShadows";
                    break;
                case "farSight":
                    fieldName = "farsight";
                    break;
                case "moreHealth":
                    fieldName = "moreHealth1";
                    break;
                case "lessStamina":
                    fieldName = "lessStamina1";
                    break;
                case "strong":
                    fieldName = "strong";
                    break;
                case "narrowerFOV":
                    fieldName = "narrowerFOV";
                    break;
                case "panda":
                case "scaryFace":
                case "ninja":
                    // These skills don't have boolean fields in PlayerSkills
                    return;
            }
            
            // Use reflection to set the boolean field
            var field = typeof(PlayerSkills).GetField(fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(skills, value);
            }
            else if (Plugin.LogDebug.Value)
            {
                Plugin.Log.LogInfo($"[Skills] No boolean field found for {configKey} -> {fieldName}");
            }
        }

        private static void UpdatePlayerStats(PlayerSkills skills)
        {
            if (Player.Instance == null) return;

            try
            {
                // Update boolean properties for skills that have them
                if (Plugin.PlayerSkillEagleEye.Value)
                {
                    skills.Farsight = true;
                }
                
                if (Plugin.PlayerSkillVitality.Value)
                {
                    skills.MoreHealth1 = true;
                }
                
                if (Plugin.PlayerSkillThirdEye.Value)
                {
                    skills.ThirdEye = true;
                }
                
                if (Plugin.PlayerSkillRunner.Value)
                {
                    skills.Runner = true;
                }
                
                if (Plugin.PlayerSkillCarefulStep.Value)
                {
                    skills.CarefulStep = true;
                }
                
                if (Plugin.PlayerSkillShakyHands.Value)
                {
                    skills.ShakyHands = true;
                }
                
                if (Plugin.PlayerSkillPoisonVulnerability.Value)
                {
                    skills.PoisonVulnerability = true;
                }
                
                if (Plugin.PlayerSkillWeakRegeneration.Value)
                {
                    skills.WeakHealthRegeneration = true;
                }
                
                if (Plugin.PlayerSkillShadows.Value)
                {
                    // Set NightShadows field directly
                    var nightShadowsField = typeof(PlayerSkills).GetField("NightShadows", 
                        BindingFlags.Public | BindingFlags.Instance);
                    if (nightShadowsField != null)
                    {
                        nightShadowsField.SetValue(skills, true);
                    }
                }
                
                if (Plugin.PlayerSkillAdrenaline.Value)
                {
                    skills.Strong = true;
                }
                
                if (Plugin.PlayerSkillFearful.Value)
                {
                    // Try to set narrowerFOV field
                    var narrowerFOVField = typeof(PlayerSkills).GetField("narrowerFOV", 
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (narrowerFOVField != null)
                    {
                        narrowerFOVField.SetValue(skills, true);
                    }
                }
                
                // Note: Appetite (panda) might not have a boolean property to set
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"[Skills] Error updating player stats: {e}");
            }
        }

        private static void LogFinalSkillStates(PlayerSkills skills)
        {
            Plugin.LogDivider();
            Plugin.Log.LogInfo("[Skills] FINAL SKILL STATES");
            Plugin.Log.LogInfo($"Total Skills: {skills.skills.Count} ({skills.manuallyActivatedSkills.Count} Manual, {skills.automaticActivatedSkills.Count} Auto)");
            
            // Track all known skills and their status
            var enabledActive = new List<string>();
            var enabledPassive = new List<string>();
            var disabledSkills = new List<string>();
            var missingSkills = new List<string>();
            
            // Also track which game objects were found for each config
            var foundGameObjects = new Dictionary<string, string>();
            foreach (var skill in skills.skills)
            {
                if (skill == null) continue;
                var configKey = GetConfigKeyFromGameObjectName(skill.gameObject.name);
                if (!string.IsNullOrEmpty(configKey))
                {
                    foundGameObjects[configKey] = skill.gameObject.name;
                }
            }
            
            foreach (var kvp in AllSkills)
            {
                var configKey = kvp.Key;
                var skillInfo = kvp.Value;
                
                var shouldBeEnabled = ShouldSkillBeEnabled(configKey);
                var isFound = foundGameObjects.ContainsKey(configKey);
                
                var displayName = $"{configKey} ({skillInfo.GameObjectName})";
                var type = skillInfo.Type;
                
                if (shouldBeEnabled)
                {
                    if (isFound)
                    {
                        if (type == "Active")
                            enabledActive.Add(displayName);
                        else
                            enabledPassive.Add(displayName);
                    }
                    else
                    {
                        missingSkills.Add($"{configKey} - GameObject '{skillInfo.GameObjectName}' not found");
                    }
                }
                else
                {
                    disabledSkills.Add(displayName);
                }
            }
            
            // Log enabled active skills
            Plugin.Log.LogInfo("");
            Plugin.Log.LogInfo($"ENABLED ACTIVE SKILLS ({enabledActive.Count}):");
            if (enabledActive.Count > 0)
            {
                foreach (var skill in enabledActive.OrderBy(s => s))
                {
                    Plugin.Log.LogInfo($"  • {skill}");
                }
            }
            else
            {
                Plugin.Log.LogInfo("  None");
            }
            
            // Log enabled passive skills
            Plugin.Log.LogInfo("");
            Plugin.Log.LogInfo($"ENABLED PASSIVE SKILLS ({enabledPassive.Count}):");
            if (enabledPassive.Count > 0)
            {
                foreach (var skill in enabledPassive.OrderBy(s => s))
                {
                    Plugin.Log.LogInfo($"  • {skill}");
                }
            }
            else
            {
                Plugin.Log.LogInfo("  None");
            }
            
            // Log disabled skills
            Plugin.Log.LogInfo("");
            Plugin.Log.LogInfo($"DISABLED SKILLS ({disabledSkills.Count}):");
            if (disabledSkills.Count > 0)
            {
                foreach (var skill in disabledSkills.OrderBy(s => s))
                {
                    Plugin.Log.LogInfo($"  • {skill}");
                }
            }
            else
            {
                Plugin.Log.LogInfo("  None");
            }
            
            // Log missing skills (enabled but not found)
            if (missingSkills.Count > 0)
            {
                Plugin.Log.LogInfo("");
                Plugin.Log.LogInfo($"MISSING SKILLS ({missingSkills.Count}):");
                foreach (var skill in missingSkills.OrderBy(s => s))
                {
                    Plugin.Log.LogWarning($"  ⚠ {skill}");
                }
            }
            
            // Log actual skills in lists for debugging
            if (Plugin.LogDebug.Value)
            {
                Plugin.Log.LogInfo("");
                Plugin.Log.LogInfo("[Skills] Actual Skills in Lists:");
                Plugin.Log.LogInfo($"Total: {skills.skills.Count}");
                Plugin.Log.LogInfo($"Manual: {skills.manuallyActivatedSkills.Count}");
                Plugin.Log.LogInfo($"Auto: {skills.automaticActivatedSkills.Count}");
                
                foreach (var skill in skills.skills)
                {
                    if (skill != null)
                    {
                        var configKey = GetConfigKeyFromGameObjectName(skill.gameObject.name);
                        Plugin.Log.LogInfo($"  {skill.gameObject.name} -> {configKey ?? "UNKNOWN"}");
                    }
                }
            }
            
            Plugin.LogDivider();
        }

        // ===================================================================
        // Skill Activation Patches
        // ===================================================================
        [HarmonyPatch(typeof(PlayerSkills), "activateSkill")]
        [HarmonyPrefix]
        private static bool OverrideSkillActivation(PlayerSkills __instance, int skillId)
        {
            if (!Plugin.PlayerSkillsModification.Value) return true;

            if (skillId < 0 || skillId >= __instance.manuallyActivatedSkills.Count)
                return false;

            var skill = __instance.manuallyActivatedSkills[skillId];
            if (skill == null) return false;

            var skillName = skill.gameObject.name;
            var configKey = GetConfigKeyFromGameObjectName(skillName);
            
            if (string.IsNullOrEmpty(configKey))
                return false;

            var skillEnabled = GetEnableConfigValue(configKey);
            
            if (!skillEnabled)
            {
                if (Plugin.LogDebug.Value)
                {
                    Plugin.Log.LogInfo($"[Skills] Blocked activation of non-enabled skill: {skillName}");
                }
                return false;
            }

            return true;
        }

        // ===================================================================
        // Helper method to refresh skills when config changes
        // ===================================================================
        public static void RefreshAllSkills()
        {
            skillsAlreadyApplied = false;
            if (Player.Instance != null && Player.Instance.skills != null)
            {
                ApplyAllSkillStates(Player.Instance.skills);
            }
        }

        // ===================================================================
        // Helper Class
        // ===================================================================
        private class SkillInfo
        {
            public string GameObjectName { get; }
            public string Type { get; } // "Active" or "Passive"
            public string Tier { get; }
            
            public SkillInfo(string gameObjectName, string type, string tier)
            {
                GameObjectName = gameObjectName;
                Type = type;
                Tier = tier;
            }
        }
    }
}