using Content.Server._RuCM.Qualifications;
using Content.Server.CMU14.Marines.Roles.Chevrons;
using Content.Server.CMU14.Round;
using Content.Server.Station.Systems;
using Content.Shared._RMC14.Marines;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Roles;

namespace Content.Server.CMU14.Qualifications;

/// <summary>Equips native platoon training uniforms before the CMU rank insignia is attached.</summary>
public sealed class CMUTrainingAppearanceSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private QualificationSystem _qualifications = default!;
    [Dependency] private StationSpawningSystem _spawning = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete,
            before: new[] { typeof(ChevronSystem) });
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId == null || !_qualifications.IsInsurgency)
            return;

        var recruit = ev.JobId == GOVFORRecruitJob.Id;
        var instructor = QualificationRules.IsDrillInstructor(ev.JobId);
        if (!recruit && !instructor)
            return;

        var platoon = _platoons.SelectedGovforPlatoon;
        var gearId = recruit ? platoon?.RecruitGear : platoon?.InstructorGear;
        if (gearId is { } id && ProtoMan.TryIndex(id, out var gear))
        {
            // The profile and loadout have already been applied. Keep a selected hat, but replace
            // the initial generic uniform/boots with the selected platoon's unarmed training gear.
            var outfit = new StartingGearPrototype { Equipment = new(gear.Equipment) };
            foreach (var slot in gear.Equipment.Keys)
            {
                if (!_inventory.TryGetSlotEntity(ev.Mob, slot, out var old))
                    continue;
                if (slot == "head")
                {
                    outfit.Equipment.Remove(slot);
                    continue;
                }

                if (!_inventory.TryUnequip(ev.Mob, slot, force: true))
                {
                    outfit.Equipment.Remove(slot);
                    continue;
                }
                QueueDel(old.Value);
            }
            _spawning.EquipStartingGear(ev.Mob, outfit, raiseEvent: false);
        }

        if (instructor && TryComp<JobPrefixComponent>(ev.Mob, out var prefix))
        {
            prefix.Prefix = "cmu-job-prefix-drill-instructor";
            Dirty(ev.Mob, prefix);
        }
    }
}
