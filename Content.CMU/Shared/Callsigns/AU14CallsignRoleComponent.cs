namespace Content.Shared.CMU14.Callsigns;

// Preferred station number from the job radio data (game conventions, not rank codes).
[RegisterComponent]
public sealed partial class AU14CallsignRoleComponent : Component
{
    // empty = keep the automatic numbered suffix, only the element applies
    [DataField]
    public string Suffix = string.Empty;

    [DataField]
    public bool CommandElement;

    // directory console section this role is listed under (AIR, MP, MEDICAL, INTEL);
    // null = command element or squad as usual
    [DataField]
    public string? Category;

    // diplomats and similar roles get no callsign and never appear on the net directory
    [DataField]
    public bool Exempt;
}
