using Harmony12;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker.Blueprints;

namespace KingmakerBuffBot
{

    public class Settings : UnityModManager.ModSettings
    {
        public List<SpellProfile> spellProfiles = new List<SpellProfile>();
        public List<SpellInfo> readyForProfileSpells = new List<SpellInfo>();
        public List<SlotAssignment> slotAssignments = new List<SlotAssignment>();
        public bool spendSpellSlot = true;
        public bool spendMaterialComponent = true;
        public bool checkGameState = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
    public class SlotAssignment
    {
        public int SlotIndex { get; set; }
        public string SpellProfileId { get; set; }
        public int CastingOrder { get; set; }
    }
    public class SpellProfile
    {
        public string ProfileID { get; set; }
        public string ProfileName { get; set; }
        public List<SpellInfo> Spells { get; set; }
    }
    public class SpellInfo
    {
        public string Name { get; set; }
        public int SpellLevel { get; set; }
        public List<Metamagic> Metamagics { get; set; }
    }
    static class Main
    {
        public static Settings settings;
        public static int menu = 0;
        public static bool enabled;
        public static List<AbilityData> allKnownSpells = new List<AbilityData>();
        public static int allKnownSpellLevelMenu;
        public static int readyForProfileSpellLevelMenu;
        public static string createProfileName = "Profile";
        public static SpellProfile managedProfile = new SpellProfile();
        public static List<UnitAssignment> unitAssignments = new List<UnitAssignment>();
        public static List<SlotAssignmentWithSpellProfile> slotAssignmentWithSpellProfiles = new List<SlotAssignmentWithSpellProfile>();
        public static List<UnitAssignmentWithSlot> unitAssignmentWithSlots = new List<UnitAssignmentWithSlot>();
        public static List<AbilityData> spellsAvailable = new List<AbilityData>();
        public class SlotAssignmentWithSpellProfile
        {
            public SpellProfile SpellProfile { get; set; }
            public SlotAssignment SlotAssignment { get; set; }
        }
        public class UnitAssignmentWithSlot
        {
            public UnitAssignment UnitAssignment { get; set; }
            public SlotAssignmentWithSpellProfile SlotAssignmentWithSpellProfile { get; set; }
        }
        public class UnitAssignment
        {
            public int SlotIndex { get; set; }
            public UnitEntityData Unit { get; set; }
        }
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
                if (settings.checkGameState &&(Game.Instance == null || Game.Instance.CurrentMode != GameModeType.Default
                && Game.Instance.CurrentMode != GameModeType.Pause))
                {
                    Helpers.Label("Buff Bot cannot be used right now.");
                    if (GUILayout.Button("Disable gamestate checker"))
                    {
                        settings.checkGameState = false;
                    }
                    return;
                }
                else
                {
                    CreateMainMenu();
                }
            }
            catch (Exception e)
            {
                modEntry.Logger.Error($"Error rendering GUI: {e}");
            }
        }

        private static void CreateMainMenu()
        {
            using (var verticalScope = new GUILayout.VerticalScope())
            {
                using (var horizontalScope = new GUILayout.HorizontalScope("box"))
                {
                    if (GUILayout.Button("Main"))
                    {
                        menu = 0;
                    }
                    if (GUILayout.Button("Add Spells"))
                    {
                        menu = 2;
                    }
                    if (GUILayout.Button("Create/Remove Profiles"))
                    {
                        menu = 3;
                    }
                    if (GUILayout.Button("Spell/Profile Management"))
                    {
                        menu = 1;
                    }
                    if (GUILayout.Button("Manage Metamagics"))
                    {
                        menu = 4;
                    }
                    if (GUILayout.Button("Configuration"))
                    {
                        menu = 5;
                    }
                }
                switch (menu)
                {
                    case 1:
                        SpellsManager();
                        break;
                    case 2:
                        SpellsManager();
                        break;
                    case 3:
                        ProfilesManager();
                        break;
                    case 4:
                        ProfilesManager();
                        break;
                    case 5:
                        ConfigurationManager();
                        break;
                    default:
                        Execute();
                        AttachProfilesManager();
                        break;
                }
            }
        }

        private static void ConfigurationManager()
        {
            using (var verticalscope = new GUILayout.VerticalScope("box"))
            {
                if (settings.spendSpellSlot)
                {
                    if (GUILayout.Button("Casting spells spend spell slots."))
                    {
                        settings.spendSpellSlot = false;
                    }
                }
                else
                {
                    if (GUILayout.Button("Casting spells do not spend spell slots."))
                    {
                        settings.spendSpellSlot = true;
                    }
                }
                if (settings.spendMaterialComponent)
                {
                    if (GUILayout.Button("Casting spells use material components."))
                    {
                        settings.spendMaterialComponent = false;
                    }
                }
                else
                {
                    if (GUILayout.Button("Casting spells do not use material components."))
                    {
                        settings.spendMaterialComponent = true;
                    }
                }
                if(settings.checkGameState == false)
                {
                    if (GUILayout.Button("Enable gamestate checker"))
                    {
                        settings.checkGameState = true;
                    }
                }
            }
        }

        private static void Execute()
        {
            if (GUILayout.Button("Execute"))
            {
                //Refresh spell data and unit data. Gui will not show even when you press the button.
                GUILayout.BeginArea(Rect.zero);
                ExecutionManager();
                GUILayout.EndArea();
                ////
                foreach (var u in unitAssignmentWithSlots.OrderBy(o => o.SlotAssignmentWithSpellProfile.SlotAssignment.CastingOrder).ToList())
                {
                    Helpers.Log("Casting Order: " + u.SlotAssignmentWithSpellProfile.SlotAssignment.CastingOrder.ToString());
                    foreach (var s in u.SlotAssignmentWithSpellProfile.SpellProfile.Spells.OrderBy(p => p.SpellLevel).ThenByDescending(o => o.Metamagics.Count).ToList())
                    {
                        GetCastableSpellsBySpellProfiles();
                        //First, get all spells that have the same name as the one the profile needs.
                        List<AbilityData> spells = spellsAvailable.Where(o => o.Name == s.Name).OrderBy(p => p.Spellbook.CasterLevel).ToList();
                        //Second, get a list of spells that qualify for metamagics, if any apply
                        List<AbilityData> validSpells = new List<AbilityData>();
                        if (s.Metamagics.Count > 0)
                        {
                            List<Metamagic> metamagics = new List<Metamagic>();

                            foreach (var spell in spells)
                            {
                                foreach (Metamagic m in (Metamagic[])Enum.GetValues(typeof(Metamagic)))
                                {
                                    if (spell.MetamagicData != null && spell.MetamagicData.Has(m))
                                    {
                                        metamagics.Add(m);
                                    }
                                }
                                bool isValid = true;
                                foreach (var mm in s.Metamagics)
                                {
                                    if (!metamagics.Contains(mm))
                                    {
                                        isValid = false;
                                    }
                                }
                                if (isValid)
                                {
                                    validSpells.Add(spell);
                                }
                            }
                        }
                        else
                        {
                            validSpells = spells;
                        }
                        if (validSpells.Count > 0)
                        {
                            validSpells = validSpells.Where(o => o.CanTarget(u.UnitAssignment.Unit)).ToList();

                            AbilityData spellToCast = validSpells.ToList().OrderByDescending(o => o.Spellbook.CasterLevel).FirstOrDefault();
                            if (spellToCast != null)
                            {
                                AbilityData spellModified = new AbilityData(spellToCast.Blueprint,spellToCast.Caster,spellToCast.Spellbook.Blueprint);
                                AbilityEffectStickyTouch component2 = spellModified.Blueprint.GetComponent<AbilityEffectStickyTouch>();
                                if ((bool)component2)
                                {
                                    spellModified = new AbilityData(component2.TouchDeliveryAbility, spellModified.Spellbook);
                                }

                                TargetWrapper targetWrapper = new TargetWrapper(u.UnitAssignment.Unit);
                                AbilityParams abilityParams = spellModified.CalculateParams();
                                AbilityExecutionContext abilityExecutionContext = new AbilityExecutionContext(spellToCast, abilityParams, targetWrapper);
                                if ((bool)component2)
                                {
                                    abilityExecutionContext = new AbilityExecutionContext(spellModified, abilityParams, Vector3.zero);
                                    Kingmaker.Controllers.AbilityExecutionProcess.ApplyEffectImmediate(abilityExecutionContext, u.UnitAssignment.Unit);
                                }
                                spellToCast.Cast(abilityExecutionContext);
                                if (settings.spendSpellSlot)
                                {
                                    spellToCast.SpendFromSpellbook();
                                }
                                if (settings.spendMaterialComponent)
                                {
                                    spellToCast.SpendMaterialComponent();
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ExecutionManager()
        {
            AttachSlotAssignmentToProfile();
            AttachSlotAssignmentToCharacter();
            GetCastableSpellsBySpellProfiles();
            ShowAvailableSpells(false);
        }

        private static void ShowAvailableSpells(bool show)
        {
            if (show)
            {
                foreach (var sa in spellsAvailable.ToList().OrderByDescending(o => o.SpellLevel))
                {
                    using (var horizontalScope = new GUILayout.HorizontalScope("box"))
                    {
                        GUILayout.Label("Caster: ");
                        GUILayout.Label(sa.Caster.CharacterName);
                        GUILayout.Label(" Spell: ");
                        GUILayout.Label(sa.Name);
                        GUILayout.Label(" Caster Level: ");
                        GUILayout.Label(sa.Spellbook.CasterLevel.ToString());
                        GUILayout.Label(" Metamagic:");
                        MetamagicData metamagicData = sa.MetamagicData;
                        if (metamagicData != null)
                        {
                            foreach (Metamagic m in (Metamagic[])Enum.GetValues(typeof(Metamagic)))
                            {
                                if (metamagicData.Has(m))
                                {
                                    GUILayout.Label(" " + Enum.GetName(typeof(Metamagic), m));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void AttachSlotAssignmentToProfile()
        {
            var slotAssignmentWithSpellProfileTemp = new List<SlotAssignmentWithSpellProfile>();
            foreach (var sa in settings.slotAssignments.OrderBy(o => o.CastingOrder).ToList())
            {
                SpellProfile sp = settings.spellProfiles.FirstOrDefault(o => o.ProfileID == sa.SpellProfileId);
                if (sp != null)
                {
                    SlotAssignmentWithSpellProfile sJoin = new SlotAssignmentWithSpellProfile
                    {
                        SlotAssignment = sa,
                        SpellProfile = sp
                    };
                    slotAssignmentWithSpellProfileTemp.Add(sJoin);
                }
            }
            slotAssignmentWithSpellProfiles = slotAssignmentWithSpellProfileTemp;
        }

        private static void AttachSlotAssignmentToCharacter()
        {
            var unitsWithSlot = new List<UnitAssignmentWithSlot>();
            foreach (var saToSp in slotAssignmentWithSpellProfiles.OrderBy(o => o.SlotAssignment.CastingOrder).ToList())
            {
                UnitAssignment ua = unitAssignments.FirstOrDefault(o => o.SlotIndex == saToSp.SlotAssignment.SlotIndex);
                if (ua != null)
                {
                    UnitAssignmentWithSlot unitSlot = new UnitAssignmentWithSlot
                    {
                        UnitAssignment = ua,
                        SlotAssignmentWithSpellProfile = saToSp
                    };
                    unitsWithSlot.Add(unitSlot);
                }
            }
            unitAssignmentWithSlots = unitsWithSlot;
        }

        private static void GetCastableSpellsBySpellProfiles()
        {
            List<AbilityData> abilitiesBySpellProfile = new List<AbilityData>();
            foreach (var ua in unitAssignments)
            {
                UnitDescriptor uR = ua.Unit.Descriptor;
                foreach (var spellbook in uR.Spellbooks)
                {
                    foreach (var sp in spellbook.GetAllKnownSpells().Where(o => o.GetAvailableForCastCount() > 0).ToList())
                    {
                        if (sp.Blueprint.HasVariants)
                        {
                            foreach (var variant in sp.Blueprint.Variants)
                            {
                                var variantAbility = new AbilityData(variant, uR, spellbook.Blueprint);
                                foreach (var sa in slotAssignmentWithSpellProfiles.Where(o => (o.SpellProfile.Spells.FirstOrDefault(p => p.Name == variant.Name)) != null).ToList())
                                {
                                    abilitiesBySpellProfile.Add(variantAbility);
                                }
                            }
                        }
                        else
                        {
                            foreach (var sa in slotAssignmentWithSpellProfiles.Where(o => (o.SpellProfile.Spells.FirstOrDefault(p => p.Name == sp.Name)) != null).ToList())
                            {
                                abilitiesBySpellProfile.Add(sp);
                            }
                        }
                    }
                    for (int i = 0; i < 10; i++)
                    {
                        foreach (var sp in spellbook.GetCustomSpells(i).Where(o => o.GetAvailableForCastCount() > 0).ToList())
                        {
                            foreach (var sa in slotAssignmentWithSpellProfiles.Where(o => (o.SpellProfile.Spells.FirstOrDefault(p => p.Name == sp.Name)) != null).ToList())
                            {
                                abilitiesBySpellProfile.Add(sp);
                            }
                        }
                    }
                }
            }
            spellsAvailable = abilitiesBySpellProfile;
        }


        private static void AttachProfilesManager()
        {
            FindPartyAssignment();
            AttachProfiles();
        }

        private static void FindPartyAssignment()
        {
            int i = 1;
            List<UnitAssignment> unitAssignmentsTemp = new List<UnitAssignment>();
            foreach (var u in Game.Instance.Player.PartyCharacters.ToList())
            {
                UnitDescriptor uD = u.Value.Descriptor;
                UnitAssignment uA = new UnitAssignment
                {
                    SlotIndex = i,
                    Unit = uD.Unit
                };
                unitAssignmentsTemp.Add(uA);
                i++;
                if (uD.Pet != null)
                {
                    if (uD.Pet.IsInState)
                    {
                        UnitAssignment uAPet = new UnitAssignment
                        {
                            SlotIndex = i,
                            Unit = uD.Pet
                        };
                        unitAssignmentsTemp.Add(uAPet);
                        i++;
                    }
                }
            }
            unitAssignments = unitAssignmentsTemp;
        }

        private static void AttachProfiles()
        {
            List<UnitReference> partyRefs = Game.Instance.Player.PartyCharacters;

            if (settings.slotAssignments.Count == 0)
            {
                for (int i = 1; i < 13; i++)
                {
                    SlotAssignment sa = new SlotAssignment();
                    SpellProfile sp = new SpellProfile();
                    sa.SlotIndex = i;
                    sa.SpellProfileId = sp.ProfileID;
                    sa.CastingOrder = i;
                    settings.slotAssignments.Add(sa);
                }
            }
            using (var verticalScope = new GUILayout.VerticalScope())
            {
                foreach (var sa in settings.slotAssignments.ToList())
                {
                    using (var horizontalScope = new GUILayout.HorizontalScope())
                    {
                        string characterInSlot = "";
                        if (unitAssignments.FirstOrDefault(o => o.SlotIndex == sa.SlotIndex) != null)
                        {
                            characterInSlot = unitAssignments.FirstOrDefault(o => o.SlotIndex == sa.SlotIndex).Unit.CharacterName;
                        }
                        GUILayout.Label("Slot " + sa.SlotIndex + ": " + characterInSlot, GUILayout.Width(150));
                        GUILayout.Label("Casting Order: " + sa.CastingOrder + " ", GUILayout.Width(150));
                        sa.CastingOrder = Mathf.RoundToInt(GUILayout.HorizontalSlider(sa.CastingOrder, 1.0f, 12.0f, GUILayout.Width(200)));
                        if (settings.slotAssignments.FirstOrDefault(o => (o.CastingOrder == sa.CastingOrder) && (o.SlotIndex != sa.SlotIndex)) != null)
                        {
                            List<int> currentNum = new List<int>();
                            foreach (var s in settings.slotAssignments.ToList())
                            {
                                currentNum.Add(s.CastingOrder);
                            }
                            var result = Enumerable.Range(1, 12).Except(currentNum).ToList();
                            if (result.Count > 0)
                            {
                                settings.slotAssignments.FirstOrDefault(o => (o.CastingOrder == sa.CastingOrder) && (o.SlotIndex != sa.SlotIndex)).CastingOrder = result.FirstOrDefault();
                            }
                        }
                        GUILayout.Label("Current Profile: ", GUILayout.Width(75));
                        if (settings.spellProfiles.Count == 0)
                        {
                            GUILayout.Label("No profiles!");
                        }
                        else
                        {
                            int spIndex = 0;
                            if (settings.spellProfiles.FirstOrDefault(o => o.ProfileID == sa.SpellProfileId) != null)
                            {
                                spIndex = settings.spellProfiles.FindIndex(o => o.ProfileID == sa.SpellProfileId);
                            }
                            float f = settings.spellProfiles.Count - 1;
                            spIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(spIndex, 0.0f, f, GUILayout.Width(200)));
                            sa.SpellProfileId = settings.spellProfiles.ElementAtOrDefault(spIndex).ProfileID;
                            GUILayout.Label(settings.spellProfiles.FirstOrDefault(o => o.ProfileID == sa.SpellProfileId).ProfileName);
                        }
                    }
                }
            }

        }

        private static void ProfilesManager()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope("box"))
            {
                using (var verticalScope = new GUILayout.VerticalScope())
                {
                    switch (menu)
                    {
                        case 3:
                            CreateRemoveProfile();
                            break;
                        case 4:
                            ManageMetamagics();
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private static void ManageMetamagics()
        {
            using (var verticalScope = new GUILayout.VerticalScope("box"))
            {
                foreach (var sp in settings.spellProfiles.ToList())
                {
                    using (var horizontalScope = new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label(sp.ProfileName, GUILayout.Width(150));
                        using (var verticalScope2 = new GUILayout.VerticalScope())
                        {
                            foreach (var s in sp.Spells.ToList())
                            {
                                GUILayout.Label(s.Name, GUILayout.Width(150));
                                using (var horizontalScope2 = new GUILayout.HorizontalScope())
                                {
                                    foreach (Metamagic m in (Metamagic[])Enum.GetValues(typeof(Metamagic)))
                                    {
                                        if (s.Metamagics == null)
                                        {
                                            s.Metamagics = new List<Metamagic>();
                                        }
                                        if (!s.Metamagics.Contains(m))
                                        {
                                            if (GUILayout.Button("+", GUILayout.Width(25)))
                                            {
                                                s.Metamagics.Add(m);
                                            }
                                            GUILayout.Label(Enum.GetName(typeof(Metamagic), m), GUILayout.Width(75));
                                        }
                                        else
                                        {
                                            if (GUILayout.Button("-", GUILayout.Width(25)))
                                            {
                                                s.Metamagics.Remove(m);
                                            }
                                            GUILayout.Label(Enum.GetName(typeof(Metamagic), m), GUILayout.Width(75));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    GUILayout.Label("", GUI.skin.horizontalSlider);
                }
            }
        }

        private static void CreateRemoveProfile()
        {
            using (var verticalScope = new GUILayout.VerticalScope())
            {
                using (var horizontalScope = new GUILayout.HorizontalScope("box"))
                {
                    GUILayout.Label("Create a profile:", GUILayout.ExpandWidth(false));
                    createProfileName = GUILayout.TextField(createProfileName, 250);
                    if (!String.IsNullOrEmpty(createProfileName))
                    {
                        if (GUILayout.Button("Create"))
                        {
                            SpellProfile spellProfile = new SpellProfile();
                            spellProfile.ProfileID = Guid.NewGuid().ToString();
                            spellProfile.ProfileName = createProfileName;
                            spellProfile.Spells = new List<SpellInfo>();
                            settings.spellProfiles.Add(spellProfile);
                            createProfileName = "";
                        }
                    }
                }
                foreach (var sp in settings.spellProfiles.ToList())
                {
                    using (var horizontalScope2 = new GUILayout.HorizontalScope("box"))
                    {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            foreach (var sa in settings.slotAssignments.Where(o => o.SpellProfileId == sp.ProfileID).ToList())
                            {
                                sa.SpellProfileId = "";
                            };
                            settings.spellProfiles.Remove(sp);
                        }
                        if (sp != null)
                        {
                            Helpers.Label(sp.ProfileName);
                        }
                    }
                }
            }
        }


        private static void SpellsManager()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope("box"))
            {
                using (var verticalScope = new GUILayout.VerticalScope())
                {
                    switch (menu)
                    {
                        case 1:
                            SetSpellLevelButtons();
                            ShowReadyForProfileSpells();
                            break;
                        case 2:
                            SetSpellLevelButtons();
                            ShowKnownSpells();
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private static void ShowKnownSpells()
        {
            foreach (var aks in allKnownSpells.Where(o => o.SpellLevel == allKnownSpellLevelMenu).ToList())
            {
                using (var horizontalScope = new GUILayout.HorizontalScope(GUILayout.MaxWidth(920)))
                {
                    if (settings.readyForProfileSpells.FirstOrDefault(o => o.Name == aks.Name) == null)
                    {
                        if (aks.Blueprint.HasVariants)
                        {
                            foreach (var v in aks.Blueprint.Variants)
                            {
                                if (settings.readyForProfileSpells.FirstOrDefault(o => o.Name == v.Name) == null)
                                {
                                    if (GUILayout.Button(v.Name))
                                    {
                                        SpellInfo rfps = new SpellInfo()
                                        {
                                            Name = v.Name,
                                            SpellLevel = aks.SpellLevel,
                                            Metamagics = new List<Metamagic>()
                                        };
                                        settings.readyForProfileSpells.Add(rfps);
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (GUILayout.Button(aks.Name))
                            {
                                SpellInfo rfps = new SpellInfo()
                                {
                                    Name = aks.Name,
                                    SpellLevel = aks.SpellLevel,
                                    Metamagics = new List<Metamagic>()
                                };
                                settings.readyForProfileSpells.Add(rfps);
                            }
                        }
                    }
                }
            }
        }
        private static void ShowReadyForProfileSpells()
        {
            foreach (var rfps in settings.readyForProfileSpells.Where(o => o.SpellLevel == readyForProfileSpellLevelMenu).ToList())
            {
                using (var horizontalScope = new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("X", GUILayout.Width(25)))
                    {
                        foreach (var sp in settings.spellProfiles.ToList())
                        {
                            foreach (var s in sp.Spells.Where(o => o.Name.Contains(rfps.Name)).ToList())
                            {
                                sp.Spells.Remove(s);
                            }
                        }
                        settings.readyForProfileSpells.Remove(rfps);
                    }
                    if (rfps != null)
                    {
                        GUILayout.Label(rfps.Name, GUILayout.Width(150));
                        using (var verticalScope = new GUILayout.VerticalScope())
                        {
                            var amountPerRow = 3;
                            for (var i = 0; i <= Math.Round(settings.spellProfiles.Count / (double)amountPerRow) * amountPerRow; i += amountPerRow)
                            {
                                using (var horizontalScope2 = new GUILayout.HorizontalScope())
                                {
                                    foreach (var sp in settings.spellProfiles.Skip(i).Take(amountPerRow).ToList())
                                    {
                                        if (sp.Spells.FirstOrDefault(o => o.Name == rfps.Name) != null)
                                        {
                                            if (GUILayout.Button("-", GUILayout.Width(25)))
                                            {
                                                sp.Spells.Remove(sp.Spells.FirstOrDefault(o => o.Name == rfps.Name));
                                            }
                                        }
                                        else
                                        {
                                            if (GUILayout.Button("+", GUILayout.Width(25)))
                                            {
                                                sp.Spells.Add(rfps);
                                            }
                                        }
                                        GUILayout.Label(sp.ProfileName, GUILayout.Width(75));
                                    }
                                    for (var i2 = 0; i2 < (amountPerRow - settings.spellProfiles.ToList().Skip(i).Take(amountPerRow).Count()); i2++)
                                    {
                                        GUILayout.Label("", GUILayout.Width(25));
                                        GUILayout.Label("", GUILayout.Width(50));
                                        GUILayout.Label("", GUILayout.Width(25));
                                    }
                                }
                            }
                        }

                    }
                }
                GUILayout.Label("", GUI.skin.horizontalSlider);
            }
        }

        private static void SetSpellLevelButtons()
        {
            using (var verticalScope = new GUILayout.VerticalScope())
            {
                using (var horizontalScope = new GUILayout.HorizontalScope("box"))
                {
                    Helpers.Label("Spell Level:");
                    for (var i = 0; i < 10; i++)
                    {
                        if (menu == 2)
                        {
                            if (GUILayout.Button(i.ToString()))
                            {
                                PopulateAllKnownSpells();
                                allKnownSpellLevelMenu = i;
                            }
                        }
                        else
                        {
                            if (GUILayout.Button(i.ToString()))
                            {
                                readyForProfileSpellLevelMenu = i;
                            }
                        }
                    }
                }
            }
        }

        private static void PopulateAllKnownSpells()
        {
            foreach (var ac in Game.Instance.Player.AllCharacters.ToList())
            {
                foreach (var sb in ac.Descriptor.Spellbooks.ToList())
                {
                    foreach (var s in sb.GetAllKnownSpells().ToList())
                    {
                        if (allKnownSpells.FirstOrDefault(o => o.Name == s.Name) == null)
                        {
                            allKnownSpells.Add(s);
                        }
                    }
                }
            }
        }
    }
}