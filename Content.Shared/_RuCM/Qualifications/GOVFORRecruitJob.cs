using Robust.Shared.Configuration;

namespace Content.Shared._RuCM.Qualifications;

/// <summary>The training job is separate from the account's effective military level.</summary>
[CVarDefs]
public sealed class GOVFORRecruitJob
{
    public const string Id = "RuCMJobGOVFORRecruit";
    public static readonly CVarDef<int> Slots = CVarDef.Create(
        "rucm.qualifications.recruit_slots", 64, CVar.SERVERONLY);
}
