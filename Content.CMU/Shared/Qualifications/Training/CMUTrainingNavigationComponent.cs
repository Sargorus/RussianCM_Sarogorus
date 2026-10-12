using Robust.Shared.GameStates;
using Robust.Shared.Map;
namespace Content.Shared.CMU14.Qualifications.Training;
/// <summary>Only the owner's currently assigned teacher/selected recruit location is replicated.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUTrainingNavigationComponent : Component
{
    public override bool SendOnlyToOwner => true;
    [DataField, AutoNetworkedField] public MapCoordinates? Coordinates;
    [DataField, AutoNetworkedField] public string TargetName = "";
    [DataField, AutoNetworkedField] public bool Instructor;
}
