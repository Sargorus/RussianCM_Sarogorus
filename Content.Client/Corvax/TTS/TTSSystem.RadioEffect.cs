using System.Numerics;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Effects;

namespace Content.Client.Corvax.TTS;

// CMU14 class: TTS voice selection, ordered delivery and playback.
public sealed partial class TTSSystem
{
    private EntityUid? _radioAuxiliary;
    private EntityUid? _radioEffect;

    private void ApplyRadioEffect((EntityUid Entity, AudioComponent Component) audio)
    {
        // Фильтруем непосредственно голос.
        // Чем больше значение — тем сильнее "зажата" полоса.
        audio.Component.Occlusion = 1.0f;

        if (!EnsureRadioEffect())
            return;

        if (_radioAuxiliary == null)
            return;

        try
        {
            _audio.SetAuxiliary(
                audio.Entity,
                audio.Component,
                _radioAuxiliary.Value);
        }
        catch (Exception e)
        {
            Logger.Warning($"Failed to apply TTS radio effect: {e.Message}");
        }
    }

    private bool EnsureRadioEffect()
    {
        if (_radioEffect != null &&
            _radioAuxiliary != null &&
            !TerminatingOrDeleted(_radioEffect.Value) &&
            !TerminatingOrDeleted(_radioAuxiliary.Value))
        {
            return true;
        }

        CleanupRadioEffect();

        try
        {
            var effect = _audio.CreateEffect();
            _radioEffect = effect.Entity;

            _audio.SetEffectPreset(
                effect.Entity,
                effect.Component,
                CreateRadioPreset());

            var auxiliary = _audio.CreateAuxiliary();
            _radioAuxiliary = auxiliary.Entity;

            _audio.SetEffect(
                auxiliary.Entity,
                auxiliary.Component,
                effect.Entity);

            return true;
        }
        catch (Exception e)
        {
            Logger.Warning($"Failed to initialize TTS radio effect: {e.Message}");

            CleanupRadioEffect();
            return false;
        }
    }

    private static ReverbProperties CreateRadioPreset()
    {
        return new ReverbProperties(
            density: 1.0f,
            diffusion: 0.1f,

            gain: 0.12f,
            gainHF: 0.15f,
            gainLF: 0.1f,

            decayTime: 0.15f,
            decayHFRatio: 0.9f,
            decayLFRatio: 0.1f,

            reflectionsGain: 0.02f,
            reflectionsDelay: 0.005f,
            reflectionsPan: new Vector3(0.1f, 0f, 0f),

            lateReverbGain: 0.05f,
            lateReverbDelay: 0.02f,
            lateReverbPan: new Vector3(-0.1f, 0f, 0f),

            echoTime: 0.1f,
            echoDepth: 0f,

            modulationTime: 0.25f,
            modulationDepth: 0f,

            airAbsorptionGainHF: 0.994f,

            hfReference: 2000f,
            lfReference: 100f,

            roomRolloffFactor: 0f,
            decayHFLimit: 0);
    }

    private void CleanupRadioEffect()
    {
        if (_radioAuxiliary is { } auxiliary &&
            !TerminatingOrDeleted(auxiliary))
        {
            Del(auxiliary);
        }

        if (_radioEffect is { } effect &&
            !TerminatingOrDeleted(effect))
        {
            Del(effect);
        }

        _radioAuxiliary = null;
        _radioEffect = null;
    }
}
