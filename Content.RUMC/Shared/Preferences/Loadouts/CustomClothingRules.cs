using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Inventory;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Webbing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Clothing.Components;
using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts.Effects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Shared.Preferences.Loadouts;

// Rules for which clothing a player may pick in the custom clothing menu.
public static class CustomClothingRules
{
    // Longest custom item name that is kept.
    public const int MaxNameLength = 64;

    // Plain uniforms have a little armor, so small values are allowed.
    private const int MaxToleratedArmor = 10;

    // Inventory slots that can be customized.
    public static readonly IReadOnlyDictionary<string, SlotFlags> Slots = new Dictionary<string, SlotFlags>
    {
        ["head"] = SlotFlags.HEAD,
        ["eyes"] = SlotFlags.EYES,
        ["mask"] = SlotFlags.MASK,
        ["neck"] = SlotFlags.NECK,
        ["jumpsuit"] = SlotFlags.INNERCLOTHING,
        ["outerClothing"] = SlotFlags.OUTERCLOTHING,
        ["gloves"] = SlotFlags.GLOVES,
        ["shoes"] = SlotFlags.FEET,
    };

    // Items with any of these components give a real bonus or cannot be taken off, so they are not offered.
    private static readonly string[] ForbiddenComponents =
    {
        "Armor", "CMHardArmor", "CMArmorPiercing", "SquadArmor", "SmartGunArmor", "RMCArmorModifier",
        "RMCMagneticArmor", "RMCBulkyArmor", "MoveOrderArmor", "FirewalkArmor", "RMCArmorSpeedTier",
        "ClothingSpeedModifier", "ClothingSlowOnDamageModifier", "SpeedModifierContactCapClothing", "StaminaResistance",
        "ExplosionResistance", "FireProtection", "TemperatureProtection", "PressureProtection", "CMURadProtection",
        "CMURadCoverage", "MycotoxinProtection", "ParasiteResistance", "ZombificationResistance", "EmpResistance",
        "EyeProtection", "FlashImmunity", "WeldingVision", "RequiresEyeProtection", "RMCEarProtection",
        "CMUCombatEarProtection", "NoiseProtection", "BreathMask", "GasTank",
        "NightVisionItem", "NightVision", "LightingNightVision", "ThermalSight", "ThermalCloak", "GhillieSuit",
        "ShowHealthBars", "ShowHealthIcons", "ShowSyndicateIcons", "ShowCriminalRecordIcons", "ShowMindShieldIcons",
        "ShowJobIcons", "HolocardScanner", "GrantMarineIcons", "GrantSquadLeaderTracker", "GrantAreaInfo",
        "OverwatchCamera", "RMCHealthIcons", "Headset", "RMCHeadset", "EncryptionKeyHolder", "HeadsetMultiBroadcast",
        "Magboots", "Insulated", "RandomInsulation", "NoSlip", "Skates", "SpikeBoots",
        "Jetpack", "AntiGravityClothing", "NinjaSuit", "NinjaGloves", "Thieving", 
        "ClothingGrantComponents", "ClothingGrantTag", "ActionGrant", "ItemActionGrant", "ToggleableClothing",
        "ToggleClothing", "ComponentToggler", "SelectableComponentAdder", "IntegratedVisors", "CycleableVisor",
        "ChameleonClothing", "VoiceMask", "FixedIdentity", "AgentIDCard", "StorageFill", "ContainerFill",
        "EntityTableContainerFill", "Reflect",
        "Gun", "MeleeWeapon", "ExtraHandsEquipment", "SelfUnremovableClothing", "Unremoveable", "CursedMask",
        "BindItemOnEquip", "ChangelingFleshClothing", "FleetingClothing", "WizardClothes", "YautjaTechItem",
        "YautjaMask", "PilotedClothing", "FactionClothing", "RMCUnstrippable", "RMCSynthItemRestriction",
        "PointLight", "HandheldLight", "RMCSuitLight", "ItemTogglePointLight", "UnpoweredFlashlight", "Blindfold",
        "ClothingBlockBackpack", "ClothingBlockWebbing", "Access", "Battery", "PowerCellSlot",
        "Respirator", "SmartGun", "TargetingLaser", "RMCItemToggleClothingVisuals",
        "Whistle", "RMCWhistle", "RMCMegaphone", "Clock", "RMCClock", "Instrument", "Stethoscope", "RMCStethoscope", "Smokable",
        "Cigar", "SmokingPipe", "EmitSoundOnUse", "ActiveListener", "PDTBracelet", "BlockMovement", "EmoteBlocker", "Explosive", "ExplodeOnTrigger", "TimerTrigger", "Sticky", "Defibrillator",
        "RequiresSkill", "Scope", "CursorOffsetRequiresWield", "Handcuff", "RMCDefibrillatorBlocked",
    };

    // Items whose id contains one of these words are test, admin, donor, ambrosia, synthetic, Working Joe, gadget, armor, spacesuit, Yautja, xenomorph, Syndicate or PMC items.
    private static readonly string[] ForbiddenIdFragments = { "Debug", "Test", "Admin", "StripMerge", "MergeBlocking", "Donor", "Ambrosia", "Synth", "AU14Joe", "WorkingJoe",
        "Whistle", "Watch", "Cigar", "SmokingPipe", "Dogtag", "Harmonica", "TennisBall", "ToyNuke", "Armor", "CatEars", "DogEars", "Commando", "ERT",
        "Hardsuit", "Spacesuit", "SpaceSuit", "Softsuit", "SuitEmergency", "HelmetEmergency", "SuitEVA", "HelmetEVA", "CBRN", "Hazmat",
        "PlagueSuit", "Towel", "Bedsheet", "Blanket", "LACNGloves", "Yautja", "BadBlood", "Xeno", "Parasite", "Facehugger",
        "Syndicate", "Syndie", "Cybersun", "Operative", "VestWebElite", "Nuke", "PMC", "Merc", "AU14WY", "WYSec" };

    // Commander, captain and leader gear is recognised by its id.
    private static readonly Regex CommandItemPattern =
        new(@"Command(?!o)|Captain|Leader|Teamlead|Lead$|(Coat|Beret|Cap|Jumpsuit)CO(?![a-z])", RegexOptions.Compiled);

    // Uniforms with a rig, webbing, bandolier or drop pouch are recognised by their id.
    private static readonly Regex RigUniformPattern = new(@"(?<!No)Webbing|Bandolier|Pouch|Harness|Holster|Chestrig|Rig$", RegexOptions.Compiled);

    // Only RMC, CMU and RuCM clothing is offered. CM and AU14 are legacy prefixes used by those projects.
    private static readonly string[] AllowedIdPrefixes = { "CM", "RMC", "AU14", "RuCM", "RUMC" };

    // Dress and parade uniforms, service jackets and peaked officer caps are recognised by their id.
    private static readonly Regex DressUniformPattern = new(
        @"DressBlues|UniformDress|Parade|Peaked|CoatCO(?![A-Za-z])|CoatCOFormal|CoatXOFormal|CoatService|HandsWhiteDress|" +
        @"HeadCapOfficer|CapOfficer|OfficerCap|CommandingOfficerCap|HeadCapGeneral|HeadCapCMO|SPPPol|ParaCOJacket",
        RegexOptions.Compiled);

    // Police, security, military police and marshal gear is recognised by its id.
    private static readonly Regex LawEnforcementPattern = new(
        @"Security|Secsoft|Sec(?![a-z])|Police|Provost|Warden|Marshal|Riot|Detective|Brig|Sheriff|CMB|Bureau|PolKom|SWAT|Swat|Deputy|MP(?![a-z])",
        RegexOptions.Compiled);

    // Items that are always hidden (head cloaks, generals' gear and a few special items).
    private static readonly HashSet<string> ForbiddenIds = new()
    {
        "ClothingNeckCloakCap", "ClothingNeckCloakCapFormal", "ClothingNeckCloakPirateCap", "ClothingNeckCloakCe",
        "ClothingCloakCmo", "ClothingNeckCloakHop", "ClothingNeckCloakHos",
        "ClothingNeckCloakQm", "ClothingNeckCloakRd", "ClothingNeckCloakCentcom", "ClothingNeckCloakNanotrasen",
        "RMCGeneralFormalCloak", "CMU14ClothingHeadWalkerMarker", "Binoculars", "C4", "RMCHeadMilitiaBucket",
        "CMJumpsuitGeneral", "CMCoatDressBluesGeneral", "RMCCoatJacketGeneral", "RMCCoatJacketGeneralFilled", "RMCHeadBeretGeneral",
        "RMCHeadCapGeneral", "RMCMarineUniformDressGeneral", "RMCArmorM3General", "CMArmorHelmetM11CGeneral",
    };

    public static bool TryGetFlags(string slot, out SlotFlags flags)
    {
        return Slots.TryGetValue(slot, out flags);
    }

    // True if the item may be picked for this slot.
    public static bool IsEligible(EntityPrototype proto, string slot, IComponentFactory factory)
    {
        if (proto.Abstract || proto.HideSpawnMenu || ForbiddenIds.Contains(proto.ID))
            return false;

        foreach (var fragment in ForbiddenIdFragments)
        {
            if (proto.ID.Contains(fragment, StringComparison.Ordinal))
                return false;
        }

        if (CommandItemPattern.IsMatch(proto.ID))
            return false;

        if (DressUniformPattern.IsMatch(proto.ID))
            return false;

        // The original RMC marine uniform predates the RMC/CM prefix convention.
        if (proto.ID != "JumpsuitMarine" &&
            !AllowedIdPrefixes.Any(prefix => proto.ID.StartsWith(prefix, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!TryGetFlags(slot, out var flags))
            return false;

        if (!proto.TryGetComponent<ClothingComponent>(out var clothing, factory) || (clothing.Slots & flags) == 0)
            return false;

        if (proto.TryGetComponent<CMArmorComponent>(out var armor, factory))
        {
            var strongest = Math.Max(Math.Max(armor.Melee, armor.Bullet), Math.Max(Math.Max(armor.Bio, armor.ExplosionArmor), armor.XenoArmor));

            // Law enforcement gear is only allowed without any armor at all.
            var limit = LawEnforcementPattern.IsMatch(proto.ID) ? 0 : MaxToleratedArmor;
            if (strongest > limit)
                return false;
        }

        // Storage is fine, but clothing that comes with something inside (filled pockets, loaded holsters) is not offered.
        if (proto.TryGetComponent<ItemSlotsComponent>(out var itemSlots, factory) &&
            itemSlots.Slots.Values.Any(itemSlot => itemSlot.StartingItem != null))
        {
            return false;
        }

        // Uniforms that come with patches, emblems or pouches already attached are not offered.
        if (proto.TryGetComponent<UniformAccessoryHolderComponent>(out var accessories, factory) &&
            accessories.StartingAccessories is { Count: > 0 })
        {
            return false;
        }

        // Uniforms sold with a rig, webbing, bandolier or pouch are not offered.
        if (slot == "jumpsuit" && RigUniformPattern.IsMatch(proto.ID))
            return false;

        // Anything else that comes with webbing already attached is not offered either.
        if (proto.TryGetComponent<WebbingClothingComponent>(out var webbing, factory) && webbing.StartingWebbing != null)
            return false;

        if (proto.TryGetComponent<CMItemSlotsComponent>(out var cmSlots, factory) &&
            (cmSlots.StartingItem != null || cmSlots.StartingItems is { Count: > 0 }))
        {
            return false;
        }

        // Anything built on a Yautja or xenomorph component is that species' gear.
        if (proto.Components.Keys.Any(name => name.StartsWith("Yautja", StringComparison.Ordinal) || name.StartsWith("Xeno", StringComparison.Ordinal)))
            return false;

        foreach (var name in ForbiddenComponents)
        {
            if (proto.Components.ContainsKey(name))
                return false;
        }

        return true;
    }

    // Trims the name, removes control characters and cuts it to the max length.
    public static string? SanitizeName(string? name)
    {
        if (name == null)
            return null;

        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length > MaxNameLength)
            cleaned = cleaned[..MaxNameLength].TrimEnd();

        return cleaned.Length == 0 ? null : cleaned;
    }

    public static bool TryGetEffect(LoadoutPrototype proto, out CustomClothingLoadoutEffect effect)
    {
        effect = proto.Effects.OfType<CustomClothingLoadoutEffect>().FirstOrDefault()!;
        return effect != null;
    }

    public static bool IsPaintable(LoadoutPrototype proto)
    {
        return proto.Effects.OfType<PaintableLoadoutEffect>().Any();
    }

    // Checks and cleans the custom data of a loadout. False means it must be dropped.
    public static bool Validate(Loadout loadout, LoadoutPrototype proto, IDependencyCollection collection)
    {
        if (!TryGetEffect(proto, out var effect))
        {
            loadout.CustomEntity = null;
            loadout.CustomName = null;
            loadout.CustomColor = IsPaintable(proto) ? loadout.CustomColor?.WithAlpha(1f) : null;
            return true;
        }

        var protoMan = collection.Resolve<IPrototypeManager>();
        var factory = collection.Resolve<IComponentFactory>();

        if (loadout.CustomEntity == null ||
            !protoMan.TryIndex<EntityPrototype>(loadout.CustomEntity, out var entity) ||
            !IsEligible(entity, effect.Slot, factory))
        {
            return false;
        }

        loadout.CustomName = SanitizeName(loadout.CustomName);
        loadout.CustomColor = loadout.CustomColor?.WithAlpha(1f);
        return true;
    }
}
