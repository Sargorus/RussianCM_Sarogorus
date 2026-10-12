using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

// A loadout with this effect can only be chosen by a character who is old enough.
public sealed partial class AgeRequirementLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public int MinAge;

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (profile.Age >= MinAge)
        {
            reason = null;
            return true;
        }

        reason = FormattedMessage.FromUnformatted(Loc.GetString("loadouts-age-requirement-not-met", ("age", MinAge)));
        return false;
    }
}
