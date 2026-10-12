namespace Content.Shared.CMU14.Callsigns;

// A Fluent ID or literal word used for the squad radio callsigns.
[RegisterComponent]
public sealed partial class AU14SquadCallsignComponent : Component
{
    [DataField(required: true)]
    public string Word = string.Empty;
}
