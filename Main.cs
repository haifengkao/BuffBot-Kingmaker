using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityModManagerNet;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.Abilities.Blueprints;

namespace WrathBuffBot
{
#if DEBUG
    [EnableReloading]
#endif
    public class Settings : UnityModManager.ModSettings
    {
        public List<SpellProfile> spellProfiles = new List<SpellProfile>();
        public List<SpellInfo> readyForProfileSpells = new List<SpellInfo>();
        public List<AbilityInfo> readyForProfileAbilities = new List<AbilityInfo>();
        public List<SlotAssignment> slotAssignments = new List<SlotAssignment>();
        public bool spendSpellSlot = true;
        public bool spendMaterialComponent = true;
        public bool checkGameState = true;
        public bool useHighestCl = true;
        public int pageAmounts = 52;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }
    public class SlotAssignment
    {
        public bool CanCast = true;
        public int SlotIndex { get; set; }
        public string SpellProfileId { get; set; }
        public int CastingOrder { get; set; }
    }
    public class SpellProfile
    {
        public string ProfileID { get; set; }
        public string ProfileName { get; set; }
        public List<SpellInfo> Spells { get; set; }
        public List<AbilityInfo> Abilities { get; set; }
    }
    public class SpellInfo
    {
        public string Name { get; set; }
        public int SpellLevel { get; set; }
        public List<Metamagic> Metamagics { get; set; }
        public string ParentName { get; set; }
    }
    public class AbilityInfo
    {
        public string Name { get; set; }
        public string ParentName { get; set; }
    }
    static class Main
    {
        public static Settings settings;
        public static int menu = 0;
        public static int maxSpellLevel = 10;
        public static bool enabled;
        public static HashSet<AbilityData> allKnownSpells = new HashSet<AbilityData>();
        public static HashSet<AbilityData> allKnownAbilities = new HashSet<AbilityData>();
        //public static int allKnownSpellLevelMenu;
        public static int readyForProfileSpellLevelMenu;
        public static string createProfileName = "Profile";
        public static string filterSpellName = "";
        public static string filterSpellPageName = "";
        public static string filterAbilityName = "";
        public static string filterAbilityPageName = "";
        public static string filterSpellNamePages = "";
        public static List<SpellInfo> readySpells = new List<SpellInfo>();
        public static List<AbilityInfo> readyAbilities = new List<AbilityInfo>();
        public static SpellProfile managedProfile = new SpellProfile();
        public static List<UnitAssignment> unitAssignments = new List<UnitAssignment>();
        public static List<SlotAssignmentWithSpellProfile> slotAssignmentWithSpellProfiles = new List<SlotAssignmentWithSpellProfile>();
        public static List<UnitAssignmentWithSlot> unitAssignmentWithSlots = new List<UnitAssignmentWithSlot>();
        public static HashSet<AbilityData> spellsAvailable = new HashSet<AbilityData>();
        public static HashSet<AbilityData> abilitiesAvailable = new HashSet<AbilityData>();
        public static Color defaultColor = new Color();
        public static int defaultFontSize = GUI.skin.button.fontSize;
        public static string profileSelected = "";
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
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
#if DEBUG
            modEntry.OnUnload = Unload;
#endif
            settings = Settings.Load<Settings>(modEntry);
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = OnSaveGUI;
            defaultColor = GUI.backgroundColor;
            return true;
        }
#if DEBUG
        static bool Unload(UnityModManager.ModEntry modEntry)
        {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.UnpatchAll();
            return true;
        }
#endif

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
                if (settings.checkGameState && (Game.Instance == null || Game.Instance.CurrentMode != GameModeType.Default
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
                    if (GUILayout.Button("Create/Remove Profiles"))
                    {
                        menu = 1;
                    }
                    if (GUILayout.Button("Add Spells To Profile"))
                    {
                        PopulateAllKnownSpells();
                        AddKnownSpells();
                        profileSelected = "";
                        menu = 2;
                    }
                    if (GUILayout.Button("Add Items/Abilities To Profile"))
                    {
                        PopulateAllAbilities();
                        AddKnownAbilities();
                        profileSelected = "";
                        menu = 3;
                    }
                    if (GUILayout.Button("Configuration"))
                    {
                        menu = 4;
                    }
                }
                switch (menu)
                {
                    case 1:
                        ProfilesManager();
                        break;
                    case 2:
                        ShowProfiles();
                        ShowAllSpells();
                        break;
                    case 3:
                        ShowProfiles();
                        ShowAllAbilities();
                        break;
                    case 4:
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
                using (var horizontalscopeSlider = new GUILayout.HorizontalScope())
                {
                    GUILayout.Label("Amount of Abilities Per Page: ", GUILayout.Width(200));
                    settings.pageAmounts = (int)Math.Round(settings.pageAmounts / 4.0) * 4;
                    settings.pageAmounts = (int)GUILayout.HorizontalSlider(settings.pageAmounts, 40, 1000);
                    if (settings.pageAmounts < 42)
                    {
                        settings.pageAmounts = 42;
                    }
                    GUILayout.Label(settings.pageAmounts.ToString(), GUILayout.Width(100));
                }
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
                if (settings.checkGameState == false)
                {
                    if (GUILayout.Button("Enable gamestate checker"))
                    {
                        settings.checkGameState = true;
                    }
                }
            }
        }
        private static void ExecutionLogic()
        {
            foreach (var u in unitAssignmentWithSlots.OrderBy(o => o.SlotAssignmentWithSpellProfile.SlotAssignment.CastingOrder))
            {
                foreach (var s in u.SlotAssignmentWithSpellProfile.SpellProfile.Spells.OrderBy(p => p.SpellLevel).ThenByDescending(o => o.Metamagics.Count))
                {
                    GetCastableSpellsBySpellProfiles();
                    //First, get all spells that have the same name as the one the profile needs.
                    List<AbilityData> spells;
                    if (settings.useHighestCl)
                    {
                        spells = spellsAvailable.Where(o => o.Name == s.Name).OrderBy(p => p.Spellbook.CasterLevel).ToList();
                    }
                    else
                    {
                        spells = spellsAvailable.Where(o => o.Name == s.Name).ToList();
                    }
                    //Second, get a list of spells that qualify for metamagics, if any apply.
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

                        AbilityData spellToCast;
                        if (settings.useHighestCl)
                        {
                            spellToCast = validSpells.OrderByDescending(o => o.Spellbook.CasterLevel).FirstOrDefault();
                        }
                        else
                        {
                            spellToCast = validSpells.FirstOrDefault();
                        }
                        if (spellToCast != null)
                        {
                            AbilityData spellModified = new AbilityData(spellToCast.Blueprint, spellToCast.Caster, spellToCast.Spellbook.Blueprint);
                            AbilityEffectStickyTouch component2 = spellModified.Blueprint.GetComponent<AbilityEffectStickyTouch>();
                            if ((bool)component2)
                            {
                                spellModified = new AbilityData(component2.TouchDeliveryAbility, spellModified.Spellbook);
                            }
                            spellModified.Blueprint.SpellResistance = false;
                            TargetWrapper targetWrapper = new TargetWrapper(u.UnitAssignment.Unit);
                            AbilityParams abilityParams = spellModified.CalculateParams();
                            AbilityExecutionContext abilityExecutionContext = new AbilityExecutionContext(spellToCast, abilityParams, targetWrapper);
                            if ((bool)component2)
                            {
                                abilityExecutionContext = new AbilityExecutionContext(spellModified, abilityParams, Vector3.zero);
                                Kingmaker.Controllers.AbilityExecutionProcess.ApplyEffectImmediate(abilityExecutionContext, u.UnitAssignment.Unit);
                            }
                            var hasBeenCasted = false;
                            if (settings.spendMaterialComponent && spellToCast.RequireMaterialComponent)
                            {
                                if (spellToCast.HasEnoughMaterialComponent)
                                {
                                    spellToCast.Cast(abilityExecutionContext);
                                    hasBeenCasted = true;
                                    spellToCast.SpendMaterialComponent();
                                }
                            }
                            else
                            {
                                spellToCast.Cast(abilityExecutionContext);
                                hasBeenCasted = true;
                            }
                            if (settings.spendSpellSlot && hasBeenCasted)
                            {
                                var profileSpell = settings.readyForProfileSpells.Where(z => z.Name == spellToCast.Name).FirstOrDefault();
                                if (profileSpell.ParentName != null)
                                {
                                    var spellsInSpellbook = spellToCast.Spellbook.GetAllKnownSpells().Where(v => v.Name == profileSpell.ParentName).Where(o => o.GetAvailableForCastCount() > 0);
                                    var spellbookList = new List<AbilityData>();
                                    foreach (var spell in spellsInSpellbook)
                                    {
                                        spellbookList.Add(spell);
                                    }
                                    if (spellbookList.Empty())
                                    {
                                        var memorizedSpells = spellToCast.Spellbook.GetAllMemorizedSpells().Where(v => v.Spell.Name == profileSpell.ParentName).Where(o => o.Spell.GetAvailableForCastCount() > 0);
                                        foreach (var m in memorizedSpells)
                                        {
                                            spellbookList.Add(m.Spell);
                                        }
                                    }
                                    if (spellbookList.Empty())
                                    {
                                        for (int i = 0; i < maxSpellLevel; i++)
                                        {
                                            var specialSpells = spellToCast.Spellbook.GetSpecialSpells(i).Where(v => v.Name == profileSpell.ParentName).Where(o => o.GetAvailableForCastCount() > 0);
                                            foreach (var m in specialSpells)
                                            {
                                                spellbookList.Add(m);
                                            }
                                        }
                                    }
                                    if (spellbookList.Empty())
                                    {
                                        for (int i = 0; i < maxSpellLevel; i++)
                                        {
                                            var specialSpells = spellToCast.Spellbook.GetCustomSpells(i).Where(v => v.Name == profileSpell.ParentName).Where(o => o.GetAvailableForCastCount() > 0);
                                            foreach (var m in specialSpells)
                                            {
                                                spellbookList.Add(m);
                                            }
                                        }
                                    }
                                    if (spellbookList.Where(v => v.Name == profileSpell.ParentName).FirstOrDefault() != null)
                                    {
                                        spellbookList.Where(v => v.Name == profileSpell.ParentName).FirstOrDefault().SpendFromSpellbook();
                                    }
                                    else
                                    {
                                        Helpers.Log($"Warning: " + profileSpell.ParentName + " was not spent from the spellbook.");
                                    }
                                }
                                else
                                {
                                    spellToCast.SpendFromSpellbook();
                                }

                            }
                        }
                    }
                }
            }

        }

        private static void ExecutionLogicAbilities()
        {
            foreach (var u in unitAssignmentWithSlots.OrderBy(o => o.SlotAssignmentWithSpellProfile.SlotAssignment.CastingOrder))
            {
                foreach (var s in u.SlotAssignmentWithSpellProfile.SpellProfile.Abilities)
                {
                    // Helpers.Log($"Start");
                    GetActivatableAbilitiesBySpellProfiles();
                    //  Helpers.Log($"Activatable Abilities Count: " + abilitiesAvailable.Count.ToString());

                    //First, get all spells that have the same name as the one the profile needs.
                    List<AbilityData> abilities;
                    if (settings.useHighestCl)
                    {
                        abilities = abilitiesAvailable.Where(o => o.Name == s.Name).OrderBy(p => p.Spellbook.CasterLevel).ToList();
                    }
                    else
                    {
                        abilities = abilitiesAvailable.Where(o => o.Name == s.Name).ToList();
                    }
                    //Second, get a list of spells that qualify for metamagics, if any apply.
                    List<AbilityData> validAbilities = new List<AbilityData>();
                    validAbilities = abilities;
                    //  Helpers.Log($"Valid Abilities Count: " + validAbilities.Count.ToString());
                    if (validAbilities.Count > 0)
                    {
                        validAbilities = validAbilities.Where(o => o.CanTarget(u.UnitAssignment.Unit)).ToList();
                        //     Helpers.Log($"Got abilities");
                        AbilityData abilityToCast;
                        if (settings.useHighestCl)
                        {
                            abilityToCast = validAbilities.OrderByDescending(o => o.Spellbook.CasterLevel).FirstOrDefault();
                        }
                        else
                        {
                            abilityToCast = validAbilities.FirstOrDefault();
                        }
                        //     Helpers.Log($"Abilities Sorted");
                        if (abilityToCast != null)
                        {
                            AbilityData abilityModified = new AbilityData(abilityToCast.Blueprint, abilityToCast.Caster);
                            //     Helpers.Log($"new abilitydata created");
                            AbilityEffectStickyTouch component2 = abilityModified.Blueprint.GetComponent<AbilityEffectStickyTouch>();
                            if ((bool)component2)
                            {
                                abilityModified = new AbilityData(component2.TouchDeliveryAbility, abilityModified.Caster);
                            }
                            abilityModified.Blueprint.SpellResistance = false;
                            //       Helpers.Log($"spell Resistance Modified");
                            TargetWrapper targetWrapper = new TargetWrapper(u.UnitAssignment.Unit);
                            AbilityParams abilityParams = abilityModified.CalculateParams();
                            AbilityExecutionContext abilityExecutionContext = new AbilityExecutionContext(abilityToCast, abilityParams, targetWrapper);
                            //       Helpers.Log($"ability execution context");
                            if ((bool)component2)
                            {
                                abilityExecutionContext = new AbilityExecutionContext(abilityModified, abilityParams, Vector3.zero);
                                Kingmaker.Controllers.AbilityExecutionProcess.ApplyEffectImmediate(abilityExecutionContext, u.UnitAssignment.Unit);
                            }
                            //        Helpers.Log($"Ability Modified");
                            //Helpers.Log(abilityExecutionContext.Caster.CharacterName + " | " + abilityExecutionContext.MainTarget.Unit.CharacterName + " | " + abilityExecutionContext.Ability.Name);
                            abilityToCast.CalculateParams();
                            if (abilityToCast.IsAvailableForCast)
                            {
                                abilityToCast.Spend();
                                abilityToCast.Cast(abilityExecutionContext);
                            }
                            //Helpers.Log($"Ability Casted");
                        }
                    }
                }
            }

        }

        private static void Execute()
        {
            GUI.backgroundColor = Color.black;
            GUI.contentColor = Color.green;
            if (GUILayout.Button("Execute", GUILayout.Height(25)))
            {
                //Refresh spell data and unit data. Gui will not show even when you press the button.
                GUILayout.BeginArea(Rect.zero);
                ExecutionManager();
                GUILayout.EndArea();
                ////
                ExecutionLogic();
                ExecutionLogicAbilities();
            }
            GUI.backgroundColor = defaultColor;
            GUI.contentColor = Color.white;
            if (settings.useHighestCl)
            {
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button("Cast spells in order of highest caster level first.", GUILayout.Height(20)))
                {
                    settings.useHighestCl = false;
                }
                GUI.backgroundColor = defaultColor;
            }
            else
            {
                GUI.backgroundColor = Color.blue;
                if (GUILayout.Button("Cast spells by party order.", GUILayout.Height(20)))
                {
                    settings.useHighestCl = true;
                }
                GUI.backgroundColor = defaultColor;
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
                foreach (var sa in spellsAvailable.OrderByDescending(o => o.SpellLevel))
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
            foreach (var sa in settings.slotAssignments.OrderBy(o => o.CastingOrder))
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
            foreach (var saToSp in slotAssignmentWithSpellProfiles.OrderBy(o => o.SlotAssignment.CastingOrder))
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
            HashSet<AbilityData> abilitiesBySpellProfile = new HashSet<AbilityData>();
            foreach (var ua in unitAssignments)
            {
                UnitDescriptor uR = ua.Unit.Descriptor;

                var unitAssignments = Main.unitAssignments;
                var slotAssignments = settings.slotAssignments;

                var slotAssignment = new SlotAssignment();
                var unitAssignment = new UnitAssignment();
                unitAssignment = unitAssignments.Where(f => f.Unit == uR.Unit).FirstOrDefault();
                slotAssignment = slotAssignments.Where(q => q.SlotIndex == unitAssignment.SlotIndex).FirstOrDefault();
                if (slotAssignment.CanCast)
                {
                    foreach (var spellbook in uR.Spellbooks)
                    {
                        foreach (var sp in spellbook.GetAllKnownSpells().Where(o => o.GetAvailableForCastCount() > 0))
                        {
                            AbilityVariants component = sp.Blueprint.GetComponent<AbilityVariants>();
                            ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>? referenceArrayProxy = (component != null) ? new ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>?(component.Variants) : null;
                            if (referenceArrayProxy != null)
                            {
                                foreach (var variant in referenceArrayProxy)
                                {
                                    var variantAbility = new AbilityData(variant, uR, spellbook.Blueprint);
                                    abilitiesBySpellProfile.Add(variantAbility);
                                }
                            }
                            else
                            {
                                abilitiesBySpellProfile.Add(sp);
                            }
                        }
                        foreach (var sp in spellbook.GetAllMemorizedSpells().Where(o => o.Spell.GetAvailableForCastCount() > 0))
                        {
                            AbilityVariants component = sp.Spell.Blueprint.GetComponent<AbilityVariants>();
                            ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>? referenceArrayProxy = (component != null) ? new ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>?(component.Variants) : null;
                            if (referenceArrayProxy != null)
                            {
                                foreach (var variant in referenceArrayProxy)
                                {
                                    var variantAbility = new AbilityData(variant, uR, spellbook.Blueprint);
                                    abilitiesBySpellProfile.Add(variantAbility);
                                }
                            }
                            else
                            {
                                abilitiesBySpellProfile.Add(sp.Spell);
                            }
                        }
                        for (int i = 0; i <= maxSpellLevel; i++)
                        {
                            foreach (var sp in spellbook.GetCustomSpells(i).Where(o => o.GetAvailableForCastCount() > 0))
                            {
                                abilitiesBySpellProfile.Add(sp);
                            }
                        }
                    }
                }
            }
            spellsAvailable = abilitiesBySpellProfile;
        }

        private static void GetActivatableAbilitiesBySpellProfiles()
        {
            HashSet<AbilityData> abilitiesBySpellProfile = new HashSet<AbilityData>();
            foreach (var ua in unitAssignments)
            {
                UnitDescriptor uR = ua.Unit.Descriptor;

                var unitAssignments = Main.unitAssignments;
                var slotAssignments = settings.slotAssignments;

                var slotAssignment = new SlotAssignment();
                var unitAssignment = new UnitAssignment();
                unitAssignment = unitAssignments.Where(f => f.Unit == uR.Unit).FirstOrDefault();
                slotAssignment = slotAssignments.Where(q => q.SlotIndex == unitAssignment.SlotIndex).FirstOrDefault();
                if (slotAssignment.CanCast)
                {
                    foreach (var a in uR.Abilities)
                    {
                        abilitiesBySpellProfile.Add(a.Data);
                    }
                }
            }
            abilitiesAvailable = abilitiesBySpellProfile;
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
            foreach (var u in Game.Instance.Player.PartyCharacters)
            {
                UnitDescriptor uD = u.Value.Descriptor;
                UnitAssignment uA = new UnitAssignment
                {
                    SlotIndex = i,
                    Unit = uD.Unit
                };
                unitAssignmentsTemp.Add(uA);
                i++;
                if (uD.Unit.Pets.Count > 0)
                {
                    foreach (var uP in uD.Unit.Pets)
                    {
                        UnitAssignment uAPet = new UnitAssignment
                        {
                            SlotIndex = i,
                            Unit = uP
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
                foreach (var sa in settings.slotAssignments)
                {
                    using (var horizontalScope = new GUILayout.HorizontalScope())
                    {
                        string characterInSlot = "";
                        if (unitAssignments.FirstOrDefault(o => o.SlotIndex == sa.SlotIndex) != null)
                        {
                            characterInSlot = unitAssignments.FirstOrDefault(o => o.SlotIndex == sa.SlotIndex).Unit.CharacterName;
                        }
                        if (sa.CanCast)
                        {
                            GUI.backgroundColor = Color.green;
                            if (GUILayout.Button("Can Cast", GUILayout.Width(85)))
                            {
                                sa.CanCast = false;
                            }
                            GUI.backgroundColor = defaultColor;
                        }
                        else
                        {
                            GUI.backgroundColor = Color.red;
                            if (GUILayout.Button("Cannot Cast", GUILayout.Width(85)))
                            {
                                sa.CanCast = true;
                            }
                            GUI.backgroundColor = defaultColor;
                        }
                        GUILayout.Label("Slot " + sa.SlotIndex + ": " + characterInSlot, GUILayout.Width(150));
                        GUILayout.Label("Priority: " + sa.CastingOrder + " ", GUILayout.Width(100));
                        sa.CastingOrder = Mathf.RoundToInt(GUILayout.HorizontalSlider(sa.CastingOrder, 1.0f, 13.0f, GUILayout.Width(200)));
                        if (settings.slotAssignments.FirstOrDefault(o => (o.CastingOrder == sa.CastingOrder) && (o.SlotIndex != sa.SlotIndex)) != null)
                        {
                            List<int> currentNum = new List<int>();
                            foreach (var s in settings.slotAssignments)
                            {
                                currentNum.Add(s.CastingOrder);
                            }
                            var result = Enumerable.Range(1, 13).Except(currentNum).ToList();
                            if (result.Count > 0)
                            {
                                settings.slotAssignments.FirstOrDefault(o => (o.CastingOrder == sa.CastingOrder) && (o.SlotIndex != sa.SlotIndex)).CastingOrder = result.FirstOrDefault();
                            }
                        }
                        GUILayout.Label("Profile: ", GUILayout.Width(40));
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
                            spIndex = Mathf.RoundToInt(GUILayout.HorizontalSlider(spIndex, 0.0f, f, GUILayout.Width(150)));
                            sa.SpellProfileId = settings.spellProfiles.ElementAtOrDefault(spIndex).ProfileID;
                            GUILayout.Label(settings.spellProfiles.FirstOrDefault(o => o.ProfileID == sa.SpellProfileId).ProfileName, GUILayout.Width(150));
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
                    CreateRemoveProfile();
                }
            }
        }

        private static void ManageMetamagics()
        {
            using (var verticalScope = new GUILayout.VerticalScope("box"))
            {
                foreach (var sp in settings.spellProfiles)
                {
                    using (var horizontalScope = new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label(sp.ProfileName, GUILayout.Width(150));
                        using (var verticalScope2 = new GUILayout.VerticalScope())
                        {
                            foreach (var s in sp.Spells)
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
                                            GUI.backgroundColor = Color.red;
                                            if (GUILayout.Button("", GUILayout.Width(25)))
                                            {
                                                s.Metamagics.Add(m);
                                            }
                                            GUI.backgroundColor = defaultColor;
                                            GUILayout.Label(Enum.GetName(typeof(Metamagic), m), GUILayout.Width(75));
                                        }
                                        else
                                        {
                                            GUI.backgroundColor = Color.green;
                                            if (GUILayout.Button("", GUILayout.Width(25)))
                                            {
                                                s.Metamagics.Remove(m);
                                            }
                                            GUI.backgroundColor = defaultColor;
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
                foreach (var sp in settings.spellProfiles)
                {
                    using (var horizontalScope2 = new GUILayout.HorizontalScope("box"))
                    {
                        if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                        {
                            foreach (var sa in settings.slotAssignments.Where(o => o.SpellProfileId == sp.ProfileID))
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

        private static void ShowAllSpells()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope("box"))
            {
                using (var verticalScope = new GUILayout.VerticalScope())
                {
                    if (profileSelected != "")
                    {
                        using (var horizontalScopeTextFilter = new GUILayout.HorizontalScope("box"))
                        {
                            filterSpellName = GUILayout.TextField(filterSpellName, 250);
                            if (GUILayout.Button("Filter", GUILayout.Width(60)))
                            {
                                readySpells = settings.readyForProfileSpells.Where(r => r.Name.ToUpper().StartsWith(filterSpellName.ToUpper())).OrderBy(v => v.Name)
                                .Skip(0).Take(settings.pageAmounts).ToList();
                                filterSpellPageName = filterSpellName;
                            }
                        }
                        using (var horizontalScopeTextPages = new GUILayout.HorizontalScope("box"))
                        {
                            double pageCount = settings.readyForProfileSpells.Where(r => r.Name.ToUpper().StartsWith(filterSpellPageName.ToUpper())).OrderBy(v => v.Name).Count();

                            pageCount = pageCount / settings.pageAmounts;
                            pageCount = Math.Ceiling(pageCount);
                            for (var i = 0; i <= pageCount - 1; i++)
                            {
                                if (GUILayout.Button("Page " + (i + 1).ToString(), GUILayout.Width(70)))
                                {
                                    readySpells = settings.readyForProfileSpells.OrderBy(v => v.Name)
                                    .Where(r => r.Name.ToUpper().StartsWith(filterSpellPageName.ToUpper()))
                                    .Skip(i * settings.pageAmounts).Take(settings.pageAmounts).ToList();
                                }
                            }
                        }

                        var amountPerRow = 4;
                        for (var i = 0; i <= Math.Round(readySpells.Count / (double)amountPerRow) * amountPerRow; i += amountPerRow)
                        {
                            using (var horizontalScope2 = new GUILayout.HorizontalScope())
                            {
                                foreach (var sp in readySpells.OrderBy(o => o.Name).Skip(i).Take(amountPerRow))
                                {
                                    var profile = settings.spellProfiles.Where(c => c.ProfileID == profileSelected).FirstOrDefault();
                                    var smallFontSize = 10;
                                    if (profile.Spells.Where(b => b.Name == sp.Name).FirstOrDefault() != null)
                                    {
                                        GUI.backgroundColor = Color.green;
                                        if (sp.Name.Length > 25)
                                        {
                                            GUI.skin.button.fontSize = smallFontSize;
                                        }
                                        if (GUILayout.Button("<b>" + sp.Name + "</b>", GUILayout.Width(220), GUILayout.Height(25)))
                                        {
                                            profile.Spells.Remove(profile.Spells.Where(b => b.Name == sp.Name).FirstOrDefault());
                                        }
                                        GUI.skin.button.fontSize = defaultFontSize;
                                        GUI.backgroundColor = defaultColor;
                                    }
                                    else
                                    {
                                        GUI.backgroundColor = Color.red;
                                        if (sp.Name.Length > 25)
                                        {
                                            GUI.skin.button.fontSize = smallFontSize;
                                        }
                                        if (GUILayout.Button(sp.Name, GUILayout.Width(220), GUILayout.Height(25)))
                                        {
                                            profile.Spells.Add(sp);
                                        }
                                        GUI.skin.button.fontSize = defaultFontSize;
                                        GUI.backgroundColor = defaultColor;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }


        private static void AddKnownSpells()
        {
            foreach (var aks in allKnownSpells.OrderBy(h => h.Name))
            {
                if (settings.readyForProfileSpells.FirstOrDefault(o => o.Name == aks.Name) == null)
                {
                    AbilityVariants component = aks.Blueprint.GetComponent<AbilityVariants>();
                    ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>? referenceArrayProxy = (component != null) ? new ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>?(component.Variants) : null;
                    if (referenceArrayProxy != null)
                    {
                        foreach (var v in referenceArrayProxy)
                        {
                            if (settings.readyForProfileSpells.FirstOrDefault(o => o.Name == v.Name) == null)
                            {
                                SpellInfo rfps = new SpellInfo()
                                {
                                    Name = v.Name,
                                    SpellLevel = aks.SpellLevel,
                                    Metamagics = new List<Metamagic>(),
                                    ParentName = aks.Name
                                };
                                settings.readyForProfileSpells.Add(rfps);
                            }
                        }
                    }
                    else
                    {
                        SpellInfo rfps = new SpellInfo()
                        {
                            Name = aks.Name,
                            SpellLevel = aks.SpellLevel,
                            Metamagics = new List<Metamagic>(),
                            ParentName = null
                        };
                        settings.readyForProfileSpells.Add(rfps);
                    }
                }
            }
        }
        private static void ShowReadyForProfileSpells()
        {
            foreach (var rfps in settings.readyForProfileSpells.OrderBy(h => h.Name).Where(o => o.SpellLevel == readyForProfileSpellLevelMenu))
            {
                using (var horizontalScope = new GUILayout.HorizontalScope())
                {
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
                                    foreach (var sp in settings.spellProfiles.Skip(i).Take(amountPerRow))
                                    {
                                        if (sp.Spells.FirstOrDefault(o => o.Name == rfps.Name) != null)
                                        {
                                            GUI.backgroundColor = Color.green;
                                            if (GUILayout.Button("<b>" + sp.ProfileName + "</b>", GUILayout.Width(125)))
                                            {
                                                sp.Spells.Remove(sp.Spells.FirstOrDefault(o => o.Name == rfps.Name));
                                            }
                                            GUI.backgroundColor = defaultColor;
                                        }
                                        else
                                        {
                                            GUI.backgroundColor = Color.red;
                                            if (GUILayout.Button(sp.ProfileName, GUILayout.Width(125)))
                                            {
                                                sp.Spells.Add(rfps);
                                            }
                                            GUI.backgroundColor = defaultColor;
                                        }
                                        //GUILayout.Label(sp.ProfileName, GUILayout.Width(75));
                                    }
                                    for (var i2 = 0; i2 < (amountPerRow - settings.spellProfiles.Skip(i).Take(amountPerRow).Count()); i2++)
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
        private static void ShowProfiles()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope("box"))
            {
                GUILayout.Label("Select Profile:");
                using (var verticalScope = new GUILayout.VerticalScope())
                {
                    var amountPerRow = 6;
                    for (var i = 0; i <= Math.Round(settings.spellProfiles.Count / (double)amountPerRow) * amountPerRow; i += amountPerRow)
                    {
                        using (var horizontalScope2 = new GUILayout.HorizontalScope())
                        {
                            foreach (var sp in settings.spellProfiles.Skip(i).Take(amountPerRow))
                            {
                                if (profileSelected != sp.ProfileID)
                                {
                                    GUI.backgroundColor = Color.grey;

                                    if (GUILayout.Button(sp.ProfileName, GUILayout.Width(125)))
                                    {
                                        if (menu == 2)
                                        {
                                            profileSelected = sp.ProfileID;
                                            readySpells = settings.readyForProfileSpells.Where(r => r.Name.ToUpper().StartsWith(filterSpellPageName.ToUpper()))
                                                .OrderBy(v => v.Name)
                                                .Skip(0).Take(settings.pageAmounts).ToList();
                                        }
                                        else if (menu == 3)
                                        {
                                            profileSelected = sp.ProfileID;
                                            readyAbilities = settings.readyForProfileAbilities.Where(r => r.Name.ToUpper().StartsWith(filterAbilityPageName.ToUpper()))
                                                .OrderBy(v => v.Name)
                                                .Skip(0).Take(settings.pageAmounts).ToList();
                                        }
                                    }
                                    GUI.backgroundColor = defaultColor;
                                }
                                else
                                {
                                    GUI.backgroundColor = Color.yellow;
                                    if (GUILayout.Button("<b>" + sp.ProfileName + "</b>", GUILayout.Width(125)))
                                    {

                                    }
                                    GUI.backgroundColor = defaultColor;
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void SetSpellLevelButtons()
        {
            using (var verticalScope = new GUILayout.VerticalScope())
            {
                using (var horizontalScope = new GUILayout.HorizontalScope("box"))
                {
                    Helpers.Label("Spell Level:");
                    for (var i = 0; i <= maxSpellLevel; i++)
                    {
                        if (GUILayout.Button(i.ToString()))
                        {
                            readyForProfileSpellLevelMenu = i;
                        }
                    }
                }
            }
        }

        private static void PopulateAllKnownSpells()
        {
            foreach (var ac in Game.Instance.Player.AllCharacters)
            {
                foreach (var sb in ac.Descriptor.Spellbooks)
                {
                    foreach (var s in sb.GetAllKnownSpells())
                    {
                        if (s.SpellLevel != 0)
                        {
                            allKnownSpells.Add(s);
                        }
                    }
                    for (int i = 0; i <= maxSpellLevel; i++)
                    {
                        foreach (var s in sb.GetMemorizedSpells(i))
                        {
                            if (s.Spell.SpellLevel != 0)
                            {
                                allKnownSpells.Add(s.Spell);
                            }
                        }
                    }
                }
            }
        }

        private static void PopulateAllAbilities()
        {
            foreach (var ac in Game.Instance.Player.AllCharacters)
            {
                foreach (var ability in ac.Abilities)
                {
                    allKnownAbilities.Add(ability.Data);
                }
            }
        }
        private static void AddKnownAbilities()
        {
            foreach (var aks in allKnownAbilities.OrderBy(h => h.Name))
            {
                if (settings.readyForProfileAbilities.FirstOrDefault(o => o.Name == aks.Name) == null)
                {
                    AbilityVariants component = aks.Blueprint.GetComponent<AbilityVariants>();
                    ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>? referenceArrayProxy = (component != null) ? new ReferenceArrayProxy<BlueprintAbility, BlueprintAbilityReference>?(component.Variants) : null;
                    if (referenceArrayProxy != null)
                    {
                        foreach (var v in referenceArrayProxy)
                        {
                            if (settings.readyForProfileAbilities.FirstOrDefault(o => o.Name == v.Name) == null)
                            {
                                AbilityInfo rfps = new AbilityInfo()
                                {
                                    Name = v.Name,
                                    ParentName = aks.Name
                                };
                                settings.readyForProfileAbilities.Add(rfps);
                            }
                        }
                    }
                    else
                    {
                        AbilityInfo rfps = new AbilityInfo()
                        {
                            Name = aks.Name,
                            ParentName = aks.Name
                        };
                        settings.readyForProfileAbilities.Add(rfps);
                    }
                }
            }
        }
        private static void ShowAllAbilities()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope("box"))
            {
                using (var verticalScope = new GUILayout.VerticalScope())
                {
                    if (profileSelected != "")
                    {
                        using (var horizontalScopeTextFilter = new GUILayout.HorizontalScope("box"))
                        {
                            filterAbilityName = GUILayout.TextField(filterAbilityName, 250);
                            if (GUILayout.Button("Filter", GUILayout.Width(60)))
                            {
                                readyAbilities = settings.readyForProfileAbilities.Where(r => r.Name.ToUpper().StartsWith(filterAbilityName.ToUpper())).OrderBy(v => v.Name)
                                .Skip(0).Take(settings.pageAmounts).ToList();
                                filterAbilityPageName = filterAbilityName;
                            }
                        }
                        using (var horizontalScopeTextPages = new GUILayout.HorizontalScope("box"))
                        {
                            double pageCount = settings.readyForProfileAbilities.Where(r => r.Name.ToUpper().StartsWith(filterAbilityPageName.ToUpper())).OrderBy(v => v.Name).Count();

                            pageCount = pageCount / settings.pageAmounts;
                            pageCount = Math.Ceiling(pageCount);
                            for (var i = 0; i <= pageCount - 1; i++)
                            {
                                if (GUILayout.Button("Page " + (i + 1).ToString(), GUILayout.Width(70)))
                                {
                                    readyAbilities = settings.readyForProfileAbilities.OrderBy(v => v.Name)
                                    .Where(r => r.Name.ToUpper().StartsWith(filterAbilityPageName.ToUpper()))
                                    .Skip(i * settings.pageAmounts).Take(settings.pageAmounts).ToList();
                                }
                            }
                        }

                        var amountPerRow = 4;
                        for (var i = 0; i <= Math.Round(readyAbilities.Count / (double)amountPerRow) * amountPerRow; i += amountPerRow)
                        {
                            using (var horizontalScope2 = new GUILayout.HorizontalScope())
                            {
                                foreach (var sp in readyAbilities.OrderBy(o => o.Name).Skip(i).Take(amountPerRow))
                                {
                                    var profile = settings.spellProfiles.Where(c => c.ProfileID == profileSelected).FirstOrDefault();
                                    var smallFontSize = 10;
                                    if (profile.Abilities.Where(b => b.Name == sp.Name).FirstOrDefault() != null)
                                    {
                                        GUI.backgroundColor = Color.green;
                                        if (sp.Name.Length > 25)
                                        {
                                            GUI.skin.button.fontSize = smallFontSize;
                                            GUI.skin.button.wordWrap = true;
                                        }
                                        if (GUILayout.Button("<b>" + sp.Name + "</b>", GUILayout.Width(220), GUILayout.Height(25)))
                                        {
                                            profile.Abilities.Remove(profile.Abilities.Where(b => b.Name == sp.Name).FirstOrDefault());
                                        }
                                        GUI.skin.button.fontSize = defaultFontSize;
                                        GUI.skin.button.wordWrap = false;
                                        GUI.backgroundColor = defaultColor;
                                    }
                                    else
                                    {
                                        GUI.backgroundColor = Color.red;
                                        if (sp.Name.Length > 25)
                                        {
                                            GUI.skin.button.fontSize = smallFontSize;
                                            GUI.skin.button.wordWrap = true;
                                        }
                                        if (GUILayout.Button(sp.Name, GUILayout.Width(220), GUILayout.Height(25)))
                                        {
                                            profile.Abilities.Add(sp);
                                        }
                                        GUI.skin.button.fontSize = defaultFontSize;
                                        GUI.skin.button.wordWrap = false;
                                        GUI.backgroundColor = defaultColor;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

    }
}