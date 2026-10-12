using Content.Shared._RMC14.Marines.Skills;
using Robust.Shared.Prototypes;
namespace Content.Shared.CMU14.Qualifications.Training;
/// <summary>Server-owned teaching access for one existing checklist step.</summary>
[Prototype("cmuTrainingTopic")]
public sealed partial class CMUTrainingTopicPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public string Name { get; private set; } = "";
    [DataField(required: true)] public string Qualification { get; private set; } = "";
    [DataField(required: true)] public string Item { get; private set; } = "";
    [DataField] public Dictionary<EntProtoId<SkillDefinitionComponent>, int> TemporarySkills { get; private set; } = new();
}
