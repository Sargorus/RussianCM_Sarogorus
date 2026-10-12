using Content.Shared.CMU14.Qualifications.Training;
using Robust.Shared.Prototypes;
namespace Content.Shared._RMC14.Marines.Skills;
public sealed partial class SkillsSystem
{
    private int CMUTrainingLevel(EntityUid entity, EntProtoId<SkillDefinitionComponent> skill, int normal)
    {
        if (!TryComp<CMUTrainingSkillsComponent>(entity, out var training) ||
            !training.Skills.TryGetValue(skill, out var level) || level <= normal)
            return normal;
        var query = new CMUTrainingSkillsQueryEvent();
        RaiseLocalEvent(entity, ref query);
        return query.Cancelled ? normal : level;
    }
}
