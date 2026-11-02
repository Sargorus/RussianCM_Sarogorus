using Content.Shared.CMU14.ColonyEconomy;

namespace Content.Server.CharacterInfo;

public sealed partial class CharacterInfoSystem
{
    private void AddLiaisonSafePin(List<string> lines, EntityUid entity)
    {
        if (!TryComp<CMULiaisonPinComponent>(entity, out var pin) || pin.Pin <= 0)
            return;

        lines.Add(Loc.GetString("cmu-liaison-safe-pin-line", ("pin", pin.Pin)));
    }
}
