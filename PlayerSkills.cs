using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCustomizer
{
    internal class PlayerSkillsPatch
    {
        public static bool RefreshSkills = true;
        private static bool _skillsAlreadyApplied;
        
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
        // ReSharper disable once UnusedMember.Local
        private static readonly List<string> UnmappedGameObjectSkills =
        [
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
        ];

        // Reverse mapping for easier lookup
        private static readonly Dictionary<string, string> GameObjectToConfigKey = new();

        static PlayerSkillsPatch()
        {
            // Build reverse mapping
            foreach (var kvp in AllSkills.Where(kvp => !string.IsNullOrEmpty(kvp.Value.GameObjectName) && !GameObjectToConfigKey.ContainsKey(kvp.Value.GameObjectName)))
            {
                GameObjectToConfigKey[kvp.Value.GameObjectName] = kvp.Key;
            }
        }

        // Main Patch Methods - Apply All Skill States
        [HarmonyPatch(typeof(PlayerSkills), "initialize")]
        [HarmonyPostfix]
        // ReSharper disable once InconsistentNaming
        private static void InitializeSkills(PlayerSkills __instance, bool resetTimesUsed = true)
        {
            if (!Plugin.PlayerSkillsModification.Value) return;
            
            // Prevent running multiple times
            if (_skillsAlreadyApplied) return;
            
            ApplyAllSkillStates(__instance);
            RefreshSkills = false;
            _skillsAlreadyApplied = true;
        }

        [HarmonyPatch(typeof(PlayerSkills), "Start")]
        [HarmonyPostfix]
        // ReSharper disable once InconsistentNaming
        private static void StartSkills(PlayerSkills __instance)
        {
            if (!Plugin.PlayerSkillsModification.Value) return;
            
            // Reset the flag when starting a new game
            _skillsAlreadyApplied = false;
        }

        // Apply All Skill States
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

        // Returns every skill the game knows about. The game data has no
        // "Skills" resource folder (the game's own Resources.LoadAll("Skills")
        // in PlayerSkills.Start returns an empty array), the skill prefabs are
        // referenced from the player's progressionSkills list and the
        // SkillTiers prefab that the skills menu loads.
        private static List<PlayerSkill> GetAllSkills()
        {
            var skills = new List<PlayerSkill>();

            if (Player.Instance != null && Player.Instance.skills != null)
            {
                foreach (var skill in Player.Instance.skills.progressionSkills.Where(skill => skill != null && !skills.Contains(skill)))
                {
                    skills.Add(skill);
                }
            }

            var skillTiers = Resources.Load("SkillTiers") as GameObject;
            if (skillTiers == null) return skills;
            {
                var tiers = skillTiers.GetComponent<SkillTiers>();
                if (tiers == null) return skills;
                foreach (var tier in tiers.tiers.Where(tier => tier != null))
                {
                    foreach (var skill in tier.skills.Where(skill => skill != null && !skills.Contains(skill)))
                    {
                        skills.Add(skill);
                    }

                    foreach (var skill in tier.negativeSkills.Where(skill => skill != null && !skills.Contains(skill)))
                    {
                        skills.Add(skill);
                    }
                }
            }

            return skills;
        }

        private static void LogAllAvailableSkills()
        {
            var allSkills = GetAllSkills();
            Plugin.Log.LogInfo("[Skills] All skills found in game:");
            
            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                var skillGameObject = skill.gameObject;
                var configKey = GetConfigKeyFromGameObjectName(skillGameObject.name);
                var status = string.IsNullOrEmpty(configKey) ? "UNMAPPED" : "MAPPED";
                        
                Plugin.Log.LogInfo($"  - {skillGameObject.name}: " +
                                   $"Manual={skill.isActivatedManually}, " +
                                   $"Status={status}, " +
                                   $"Config={configKey ?? "N/A"}");
            }
        }

        private static void RebuildSkillLists(PlayerSkills skills)
        {
            // Get all available skills from the game
            var allSkills = GetAllSkills();
            
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

            // Add skills from the game's skill lists
            foreach (var skill in allSkills)
            {
                if (skill == null) continue;
                var skillGameObject = skill.gameObject;
                if (skillGameObject == null) continue;

                var gameObjectName = skillGameObject.name;
                    
                // Skip if already processed
                if (!processedGameObjects.Add(gameObjectName))
                    continue;

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

                    if (skillInstance == null) continue;
                    AddSkillToLists(skills, skillInstance);
                            
                    // Update boolean fields for skill effects
                    UpdateSkillBooleanField(skills, configKey, true);
                }
                else
                {
                    // Skill is not enabled, set boolean field to false
                    UpdateSkillBooleanField(skills, configKey, false);
                }
            }

            // Update player stats for skills that affect them
            UpdatePlayerStats(skills);
        }

        private static PlayerSkill CreateSkillInstance(GameObject skillPrefab, string skillName)
        {
            // Create a new instance
            var skillInstance = Object.Instantiate(skillPrefab);
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
            return GameObjectToConfigKey.TryGetValue(gameObjectName, out var configKey) ? configKey : null;
        }

        private static bool ShouldSkillBeEnabled(string configKey)
        {
            return GetEnableConfigValue(configKey);
        }

        private static bool GetEnableConfigValue(string configKey)
        {
            if (!AllSkills.ContainsKey(configKey))
                return false;

            return configKey switch
            {
                // Positive Skills
                "EagleEye" => Plugin.PlayerSkillEagleEye.Value,
                "Moth" => Plugin.PlayerSkillMoth.Value,
                "Navigator" => Plugin.PlayerSkillNavigator.Value,
                "MushroomHealing" => Plugin.PlayerSkillMushroomHealing.Value,
                "AcidBlood" => Plugin.PlayerSkillAcidBlood.Value,
                "ThirdEye" => Plugin.PlayerSkillThirdEye.Value,
                "Runner" => Plugin.PlayerSkillRunner.Value,
                "CarefulStep" => Plugin.PlayerSkillCarefulStep.Value,
                "Appetite" => Plugin.PlayerSkillAppetite.Value,
                "Scream" => Plugin.PlayerSkillScream.Value,
                "Adrenaline" => Plugin.PlayerSkillAdrenaline.Value,
                "Vitality" => Plugin.PlayerSkillVitality.Value,
                "Chameleon" => Plugin.PlayerSkillChameleon.Value,
                // Negative Skills
                "Shadows" => Plugin.PlayerSkillShadows.Value,
                "PoisonVulnerability" => Plugin.PlayerSkillPoisonVulnerability.Value,
                "Fearful" => Plugin.PlayerSkillFearful.Value,
                "WeakLungs" => Plugin.PlayerSkillWeakLungs.Value,
                "Weakness" => Plugin.PlayerSkillWeakness.Value,
                "WeakRegeneration" => Plugin.PlayerSkillWeakRegeneration.Value,
                "ShakyHands" => Plugin.PlayerSkillShakyHands.Value,
                _ => false
            };
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

        // Skill Activation Patches
        [HarmonyPatch(typeof(PlayerSkills), "activateSkill")]
        [HarmonyPrefix]
        // ReSharper disable once InconsistentNaming
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

        // Helper method to refresh skills when config changes
        // ReSharper disable once UnusedMember.Global
        public static void RefreshAllSkills()
        {
            _skillsAlreadyApplied = false;
            if (Player.Instance != null && Player.Instance.skills != null)
            {
                ApplyAllSkillStates(Player.Instance.skills);
            }
        }

        // Helper Class
        private class SkillInfo(string gameObjectName, string type, string tier)
        {
            public string GameObjectName { get; } = gameObjectName;
            public string Type { get; } = type; // "Active" or "Passive"
            // ReSharper disable once UnusedMember.Local
            public string Tier { get; } = tier;
        }
    }
}