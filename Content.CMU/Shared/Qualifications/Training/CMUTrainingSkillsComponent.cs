using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
namespace Content.Shared.CMU14.Qualifications.Training;
/// <summary>A replicated overlay, never written into SkillsComponent or persisted.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUTrainingSkillsComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<EntProtoId<SkillDefinitionComponent>, int> Skills = new();
}
[ByRefEvent]
public record struct CMUTrainingSkillsQueryEvent
{
    public bool Cancelled;
}
