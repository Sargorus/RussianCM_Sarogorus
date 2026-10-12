namespace Content.Shared._RuCM.Qualifications;

public static partial class QualificationRules
{
    /// <summary>Admission polling needs only a boolean, without sorting/copying a missing-requirement list.</summary>
    public static bool IsJobAllowed(PlayerTrainingState? player, RoleRequirement? role,
        InstructorAccreditation? accreditation = null)
    {
        if (role == null || role.Synthetic)
            return true;
        var instructor = IsDrillInstructor(role.JobId);
        if (!role.Enabled && !instructor)
            return true;
        if (instructor && accreditation is not { Active: true })
            return false;
        var minimum = instructor && role.MinimumLevel < MilitaryLevel.Sergeant
            ? MilitaryLevel.Sergeant : role.MinimumLevel;
        if (EffectiveLevel(player) < minimum)
            return false;
        foreach (var id in role.Professional)
        {
            if (player == null || !player.Grants.TryGetValue(id, out var grant) || grant.Status != QualificationStatus.Active)
                return false;
        }
        return true;
    }
}
