using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

// A loadout with this effect can only be chosen if the character has picked one of the listed skill options.
public sealed partial class PerkRequirementLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public List<ProtoId<LoadoutPrototype>> AnyOf = new();

    public override bool Validate(
        HumanoidCharacterProfile profile,
        RoleLoadout loadout,
        ICommonSession? session,
        IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var picked = profile.Loadouts.Values
            .Append(loadout)
            .SelectMany(source => source.SelectedLoadouts.Values)
            .SelectMany(selected => selected)
            .Select(selected => selected.Prototype.Id)
            .ToHashSet();

        if (AnyOf.Any(perk => picked.Contains(perk.Id)))
        {
            reason = null;
            return true;
        }

        var names = AnyOf.Select(perk => Loc.TryGetString($"colonist-skill-editor-loadout-{perk.Id}", out var name) ? name : perk.Id);
        reason = FormattedMessage.FromUnformatted(Loc.GetString("loadouts-perk-requirement-not-met",
            ("perks", string.Join(" / ", names))));
        return false;
    }
}
