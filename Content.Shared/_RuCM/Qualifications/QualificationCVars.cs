using Robust.Shared.Configuration;

namespace Content.Shared._RuCM.Qualifications;

[CVarDefs]
public sealed class QualificationCVars
{
    public static readonly CVarDef<bool> Enabled = CVarDef.Create("rucm.qualifications.enabled", true, CVar.SERVERONLY);
    public static readonly CVarDef<bool> Enforce = CVarDef.Create("rucm.qualifications.enforce", true, CVar.SERVERONLY);
    public static readonly CVarDef<bool> FailOpen = CVarDef.Create("rucm.qualifications.fail_open", false, CVar.SERVERONLY);
    public static readonly CVarDef<string> ServerId = CVarDef.Create("rucm.qualifications.server_id", "RussianCM", CVar.SERVERONLY);
}
