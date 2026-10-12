using Content.Shared.Corvax.TTS;
using Content.Shared.Humanoid;
using Robust.Shared.Random;

namespace Content.Server.Corvax.TTS;

public sealed partial class TTSSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private void InitializeVoices()
    {
        SubscribeLocalEvent<TTSComponent, MapInitEvent>(OnVoiceMapInit);
        SubscribeLocalEvent<TTSComponent, CMUTTSVoiceAppliedEvent>(OnVoiceApplied);
    }

    private void OnVoiceMapInit(Entity<TTSComponent> ent, ref MapInitEvent args)
    {
        EnsureVoiceAssigned(ent);
    }

    private void OnVoiceApplied(Entity<TTSComponent> ent, ref CMUTTSVoiceAppliedEvent args)
    {
        EnsureVoiceAssigned(ent);
    }

    private void EnsureVoiceAssigned(Entity<TTSComponent> ent)
    {
        if (!string.IsNullOrWhiteSpace(ent.Comp.VoicePrototypeId) &&
            ent.Comp.VoicePrototypeId != CMUTTSVoiceSelection.RandomVoice)
            return;

        var sex = GetSpeakerSex(ent);
        var voices = CMUTTSVoiceSelection.GetRandomVoices(_prototypeManager.EnumeratePrototypes<TTSVoicePrototype>(), sex);
        if (voices.Length > 0)
            ent.Comp.VoicePrototypeId = _random.Pick(voices).ID;
    }

    private Sex GetSpeakerSex(EntityUid? uid)
    {
        return TryComp<HumanoidProfileComponent>(uid, out var profile) ? profile.Sex : Sex.Unsexed;
    }
}
