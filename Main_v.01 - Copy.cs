using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.Utility;
using Kingmaker.View;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityModManagerNet;

namespace KingmakerBuffBot
{
    public class SpellProfile
    {
        public string ProfileID { get; set; }
        public string ProfileName { get; set; }
        public List<string> Spells { get; set; }
    }
    public class Priorities
    {
        public string Priority1 { get; set; }
        public string Priority2 { get; set; }
        public string Priority3 { get; set; }
        public string Priority4 { get; set; }
        public string Priority5 { get; set; }
        public string Priority6 { get; set; }
        public string Priority7 { get; set; }
        public string Priority8 { get; set; }
        public string Priority9 { get; set; }
        public string Priority10 { get; set; }
        public string Priority11 { get; set; }
        public string Priority12 { get; set; }
    }
    public class Settings : UnityModManager.ModSettings
    {
        public List<string> spellList = new List<string>();

        public List<SpellProfile> spellProfiles = new List<SpellProfile>();

        public Priorities priorities = new Priorities();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
    static class Main
    {
        public static bool enabled;
        public static List<AbilityData> allKnownSpells = new List<AbilityData>();
        public static bool spellListPopulated = false;
        public static bool showSpellList = false;
        public static bool attachProfiles = false;
        public static bool createProfiles = false;
        public static bool manageProfiles = false;
        public static bool creatingQueue = true;
        public static string createProfileName = "Profile";
        public static Queue<QueueMember> spellcastingQueue = new Queue<QueueMember>();
        public static PartyPositions partyPositions = new PartyPositions();
        public static List<AbilityData> spellsAvailableToCast = new List<AbilityData>();
        public static List<TargetSpells> targetSpells = new List<TargetSpells>();
        public static bool abort = false;

        public class PartyPositions
        {
            public UnitEntityData Priority1 { get; set; }
            public UnitEntityData Priority2 { get; set; }
            public UnitEntityData Priority3 { get; set; }
            public UnitEntityData Priority4 { get; set; }
            public UnitEntityData Priority5 { get; set; }
            public UnitEntityData Priority6 { get; set; }
            public UnitEntityData Priority7 { get; set; }
            public UnitEntityData Priority8 { get; set; }
            public UnitEntityData Priority9 { get; set; }
            public UnitEntityData Priority10 { get; set; }
            public UnitEntityData Priority11 { get; set; }
            public UnitEntityData Priority12 { get; set; }
        }
        public class QueueMember
        {
            public UnitEntityData Caster { get; set; }
            public AbilityData Spell { get; set; }
            public UnitEntityData Target { get; set; }
        }
        public class TargetSpells
        {
            public string PriorityName { get; set; }
            public UnitEntityData target { get; set; }
            public List<string> spellList { get; set; }
        }

        public static Settings settings;
        static bool Load(UnityModManager.ModEntry modEntry)
        {
            var harmony = HarmonyInstance.Create(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            settings = Settings.Load<Settings>(modEntry);
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;

            return true;
        }

        public static void OnSaveGUI(UnityModManager.ModEntry modEntry)
        {
            settings.Save(modEntry);
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            enabled = value;

            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            try
            {
                if (Game.Instance == null)
                {
                    CreateLabel("BuffBot cannot be used in this state.");
                    return;
                }
                AttachProfileToPriority(modEntry);
                //CreateSpellcastingQueue(modEntry);
                CastSpellQueue(modEntry);
                SetPartyPriorities(modEntry);
                AttachProfiles(modEntry);
                ProfileManager(modEntry);
                WriteSpellList(modEntry);
                ReadSpellList(modEntry);
            }
            catch (Exception e)
            {
                modEntry.Logger.Error($"Error rendering GUI: {e}");
            }
        }

        private static void CastSpellQueue(UnityModManager.ModEntry modEntry)
        {
            var i = 0;
            if (GUILayout.Button("Start Casting Spells"))
            {
                List<TargetSpells> targetSpellsTemp = targetSpells;
                foreach (var t in targetSpellsTemp.ToList())
                {
                    foreach (var targetSpell in t.spellList.ToList())
                    {
                        SetCastingQueue(modEntry);
                        List<AbilityData> validSpells = CleanupSpells(spellsAvailableToCast, t);
                        AbilityData spell = validSpells.ToList().OrderByDescending(o => o.Spellbook.CasterLevel).FirstOrDefault(s => targetSpell.Equals(s.Name));
                        if(spell != null)
                        {
                            TargetWrapper targetWrapper = new TargetWrapper(t.target);
                            AbilityParams abilityParams = spell.CalculateParams();
                            AbilityExecutionContext abilityExecutionContext = new AbilityExecutionContext(spell, abilityParams, targetWrapper);
                            abilityExecutionContext.ForceAlwaysHit = true;
                            spell.Cast(abilityExecutionContext);
                            //spell.SpendFromSpellbook();
                        }
                    }
                }
            }
        }

        private static List<AbilityData> CleanupSpells(List<AbilityData> spellsList, TargetSpells t)
        {
            TargetWrapper targetWrapper = new TargetWrapper(t.target);
            return spellsList.Where(o => o.CanTarget(targetWrapper)).ToList();
        }

        private static void AttachProfileToPriority(UnityModManager.ModEntry modEntry)
        {
            List<PropertyInfo> settingPriorities = settings.priorities.GetType().GetProperties().ToList();
            List<TargetSpells> targetSpellsLink = new List<TargetSpells>();

            foreach (var priority in partyPositions.GetType().GetProperties().ToList())
            {
                UnitEntityData priorityUnit = (UnitEntityData)priority.GetValue(partyPositions);
                if (priorityUnit != null)
                {
                    PropertyInfo priorityToSet = settingPriorities.FirstOrDefault(o => Convert.ToString(o.Name) == Convert.ToString(priority.Name));
                    string profileId = Convert.ToString(priorityToSet.GetValue(settings.priorities));

                    SpellProfile spellProfileListMember = settings.spellProfiles.FirstOrDefault(o => o.ProfileID == profileId);
                    if (spellProfileListMember != null)
                    {
                        List<string> spellProfile = spellProfileListMember.Spells;
                        TargetSpells nameSpellsTarget = new TargetSpells
                        {
                            PriorityName = priority.Name,
                            spellList = spellProfile,
                            target = (UnitEntityData)priority.GetValue(partyPositions)
                        };
                        targetSpellsLink.Add(nameSpellsTarget);
                    }
                }

            }
            GUILayout.BeginVertical();
            foreach (var target in targetSpellsLink)
            {
                CreateLabel("Priority Name: " + target.PriorityName + " Target: " + target.target.CharacterName + " SpellCount: " + target.spellList.Count.ToString());
            }
            GUILayout.EndVertical();
            targetSpells = targetSpellsLink;
        }

        private static void SetCastingQueue(UnityModManager.ModEntry modEntry)
        {
            List<AbilityData> spellsToCast = new List<AbilityData>();
            List<UnitEntityData> unitGroup = Game.Instance.Player.Party.ToList();
            GUILayout.BeginVertical("Box");
            if (unitGroup.Count > 0)
            {
                foreach(var unit in unitGroup)
                {
                    UnitDescriptor unitDescriptor = unit.Descriptor;
                    foreach(var spellbook in unitDescriptor.Spellbooks)
                    {
                        if (spellbook.Blueprint.Spontaneous)
                        {
                            foreach(var spell in spellbook.GetAllKnownSpells())
                            {
                                if (!spellsToCast.Contains(spell))
                                {
                                    if ((spell.IsAvailableForCast && spell.IsAvailable) || (spell.Blueprint.IsCantrip))
                                    {
                                        spellsToCast.Add(spell);
                                    }
                                    else
                                    {
                                        if (spellsToCast.Contains(spell))
                                        {
                                            spellsToCast.Remove(spell);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            foreach (var spell in spellbook.GetAllMemorizedSpells())
                            {
                                if (!spellsToCast.Contains(spell.Spell))
                                {
                                    if ((spell.Spell.IsAvailableForCast && spell.Available) || (spell.Spell.Blueprint.IsCantrip))
                                    {
                                        spellsToCast.Add(spell.Spell);
                                    }
                                    else
                                    {
                                        if(spellsToCast.Contains(spell.Spell))
                                        {
                                            spellsToCast.Remove(spell.Spell);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                /*foreach (var spell in spellsToCast.ToList().OrderByDescending(o=>o.Spellbook.CasterLevel))
                {
                    if (settings.spellList.Contains(spell.Name))
                    {
                        GUILayout.BeginHorizontal();
                        CreateLabel(spell.Name);
                        CreateLabel(" - ");
                        CreateLabel(spell.Spellbook.CasterLevel.ToString());
                        CreateLabel(" - ");
                        CreateLabel(spell.Caster.CharacterName);
                        GUILayout.EndHorizontal();
                    }
                }*/
            }
            GUILayout.EndVertical();
            spellsAvailableToCast = spellsToCast;
        }

        private static void SetPartyPriorities(UnityModManager.ModEntry modEntry)
        {
            List<UnitEntityData> unitGroup = Game.Instance.Player.ControllableCharacters.Where(u => u.GroupId == "<directly-controllable-unit>").ToList();
            GUILayout.BeginVertical("Box");
            if (unitGroup.Count > 0)
            {
                foreach (PropertyInfo priority in partyPositions.GetType().GetProperties().ToList())
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(SeperateStringNum(priority.Name),GUILayout.Width(250));
                    if (String.IsNullOrEmpty(Convert.ToString(priority.GetValue(partyPositions))))
                    {
                        foreach (var unit in unitGroup)
                        {
                            if (GUILayout.Button(unit.CharacterName, GUILayout.ExpandWidth(false)))
                            {
                                priority.SetValue(partyPositions, unit);
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            priority.SetValue(partyPositions, null);
                        }
                        UnitEntityData partyPriority = (UnitEntityData)priority.GetValue(partyPositions);

                        if (partyPriority != null)
                        {
                            CreateLabel(Convert.ToString(partyPriority.CharacterName));
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }

        private static void Log(string v)
        {
            UnityModManager.Logger.Log(v);
        }

        private static void AttachProfiles(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            if (attachProfiles)
            {
                if (GUILayout.Button("Hide Attach Profiles", GUILayout.ExpandWidth(false)))
                {
                    attachProfiles = false;
                }
                CreateLabel("Attach a profile:");

               foreach(PropertyInfo priority in settings.priorities.GetType().GetProperties().ToList())
                {
                    GUILayout.BeginHorizontal();
                    CreateLabel(SeperateStringNum(priority.Name));
                    if (String.IsNullOrEmpty(Convert.ToString(priority.GetValue(settings.priorities))))
                    {
                        foreach (var profile in settings.spellProfiles)
                        {
                            if (GUILayout.Button(profile.ProfileName, GUILayout.ExpandWidth(false)))
                            {
                                priority.SetValue(settings.priorities,profile.ProfileID);
                            }
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            priority.SetValue(settings.priorities, null);
                        }
                        if (!String.IsNullOrEmpty(Convert.ToString(priority.GetValue(settings.priorities))))
                        {
                            IEnumerable<SpellProfile> profileList = settings.spellProfiles.Where(profile => profile.ProfileID == priority.GetValue(settings.priorities).ToString());
                            foreach (var profile in profileList.ToList())
                            {
                                CreateLabel(profile.ProfileName);
                            }
                        }
                    }
                    GUILayout.EndHorizontal();
                }
               
            }
            else
            {
                if (GUILayout.Button("Attach Profiles", GUILayout.ExpandWidth(false)))
                {
                    attachProfiles = true;
                }
            }
            GUILayout.EndVertical();
        }

        private static string SeperateStringNum(string v)
        {
            Regex re = new Regex(@"([a-zA-Z]+)(\d+)");
            Match result = re.Match(v);

            string alpha = result.Groups[1].Value;
            string number = result.Groups[2].Value;

            return alpha + " " + number;
        }

        public static object GetPropValue(object src, string propName)
        {
            return src.GetType().GetProperty(propName).GetValue(src, null);
        }

        private static void ProfileManager(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            if (createProfiles)
            {
                if (GUILayout.Button("Hide Create Profiles", GUILayout.ExpandWidth(false)))
                {
                    createProfiles = false;
                }
                CreateLabel("Create a profile:");
                GUILayout.BeginHorizontal();
                createProfileName = GUILayout.TextField(createProfileName, GUILayout.Width(200));
                if (GUILayout.Button("Create",GUILayout.ExpandWidth(false)))
                {
                    SpellProfile spellProfile = new SpellProfile();
                    spellProfile.ProfileID = Guid.NewGuid().ToString();
                    spellProfile.ProfileName = createProfileName;
                    spellProfile.Spells = new List<string>();
                    settings.spellProfiles.Add(spellProfile);
                }
                GUILayout.EndHorizontal();

            }
            else
            {
                if (GUILayout.Button("Create Profiles",GUILayout.ExpandWidth(false)))
                {
                    createProfiles = true;
                }
            }
            if (manageProfiles)
            {
                if (GUILayout.Button("Hide Manage Profiles", GUILayout.ExpandWidth(false)))
                {
                    manageProfiles = false;
                }
                CreateLabel("Manage current profiles:");
                GUILayout.BeginVertical("Box");
                foreach(var profile in settings.spellProfiles.ToList())
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("X",GUILayout.ExpandWidth(false)))
                    {
                        foreach (PropertyInfo priority in settings.priorities.GetType().GetProperties().ToList())
                        {
                            if (Convert.ToString(priority.GetValue(settings.priorities, null)).Equals(profile.ProfileID))
                            {
                                priority.SetValue(settings.priorities,null);
                            }
                        }
                            settings.spellProfiles.Remove(profile);
                    }
                    CreateLabel(profile.ProfileName);
                    GUILayout.BeginVertical();
                    foreach (var spell in profile.Spells.ToList())
                    {
                        GUILayout.BeginHorizontal(GUILayout.Width(300));
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            profile.Spells.Remove(spell);
                        }
                        CreateLabel(spell);
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();

                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

            }
            else
            {
                if (GUILayout.Button("Manage Profiles", GUILayout.ExpandWidth(false)))
                {
                    manageProfiles = true;
                }
            }
            GUILayout.EndVertical();
        }
        
        private static void ReadSpellList(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            if (!showSpellList)
            {
                if (GUILayout.Button("Show Saved Spells",GUILayout.ExpandWidth(false)))
                {
                    showSpellList = true;
                }
            }
            if (showSpellList)
            {
                if (GUILayout.Button("Hide Saved Spells", GUILayout.ExpandWidth(false)))
                {
                    showSpellList = false;
                }
            }
            if (showSpellList)
            {
                if(settings.spellList.Count>0)
                {
                    foreach(var spell in settings.spellList)
                    {
                        GUILayout.BeginHorizontal();
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            foreach (var profile in settings.spellProfiles)
                            {
                                if (profile.Spells.Contains(spell))
                                {
                                    profile.Spells.Remove(spell);
                                }
                            }
                            settings.spellList.Remove(spell);
                        }
                        GUILayout.Label(spell, GUILayout.ExpandWidth(false));
                        GUILayout.BeginVertical();
                        CreateLabel("Add to profile:");
                        GUILayout.BeginHorizontal();
                            foreach (var profile in settings.spellProfiles)
                            {
                                if (!profile.Spells.Contains(spell))
                                {
                                    if (GUILayout.Button(profile.ProfileName, GUILayout.ExpandWidth(false)))
                                    {
                                        profile.Spells.Add(spell);
                                    }
                                }
                            }
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                        GUILayout.EndHorizontal();
                    }
                }
                else
                {
                    CreateLabel("Spell List is empty!");
                }
            }
            GUILayout.EndVertical();

        }

        private static void WriteSpellList(UnityModManager.ModEntry modEntry)
        {
            GUILayout.BeginVertical("box");
            CreateLabel("Add Spells:");
            GUILayout.BeginHorizontal();
            if (spellListPopulated == false)
            {
                if (GUILayout.Button("Show Currently Known Spells", GUILayout.ExpandWidth(false)))
                {
                    GetSpells(Game.Instance.Player.Party, modEntry);
                }
            }
            else
            {
                if (GUILayout.Button("Refresh Currently Known Spells", GUILayout.ExpandWidth(false)))
                {
                    spellListPopulated = false;
                    GetSpells(Game.Instance.Player.Party, modEntry);
                    spellListPopulated = true;
                }
                if (GUILayout.Button("Hide Spells", GUILayout.ExpandWidth(false)))
                {
                    spellListPopulated = false;
                }
            }
            GUILayout.EndHorizontal();
            if (spellListPopulated)
            {
                GUILayout.BeginHorizontal();
                for (var i = 0; i < 10; i++)
                {
                    GUILayout.BeginVertical("box");
                    CreateLabel("Spell Level: " + i);

                    IEnumerable<AbilityData> spellList = allKnownSpells.Where(spell => spell.SpellLevel == i);
                    foreach (var spell in spellList.Distinct())
                    {
                        if (!settings.spellList.Contains(spell.Name))
                        {
                            if (GUILayout.Button(spell.Name))
                            {
                                settings.spellList.Add(spell.Name);
                                
                            }
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();
        }

        private static void GetSpells(List<UnitEntityData> party, UnityModManager.ModEntry modEntry)
        {
            allKnownSpells.Clear();
            foreach (var character in party)
            {
                UnitDescriptor characterDescriptor = character.Descriptor;
                foreach (var spellbook in characterDescriptor.Spellbooks)
                {
                    foreach (var spell in spellbook.GetAllKnownSpells())
                    {
                        allKnownSpells.Add(spell);
                    }
                }
            }
            spellListPopulated = true;
        }

        private static void CastSpell(QueueMember q)
        {
            q.Caster.Commands.Run(new UnitUseAbility(q.Spell, q.Target));
        }

        private static void CreateLabel(string label)
        {
            GUILayout.Label(label,GUILayout.ExpandWidth(false));
        }

    }
}