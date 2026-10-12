using Content.Server._RMC14.Chat.Chat;
using Content.Shared._RMC14.Language.Prototypes;
using Content.Shared.Chat;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    /// <summary>
    /// Local speech listeners who may receive an intelligible TTS recording.
    /// Reuses chat recipient expansion and restrictions, but excludes distant observers.
    /// </summary>
    public Dictionary<ICommonSession, EntityUid> GetLocalTTSRecipients(
        EntityUid source,
        ProtoId<LanguagePrototype> language,
        ChatTransmitRange range,
        bool whisper = false,
        bool muffledOnly = false,
        bool ignoreXenos = false)
    {
        var result = new Dictionary<ICommonSession, EntityUid>();
        if (!_prototypeManager.TryIndex(language, out var prototype) || !prototype.NeedsSpeech)
            return result;

        var maxRange = whisper ? WhisperMuffledRange : VoiceRange;
        foreach (var (session, data) in GetRecipients(
                     source,
                     maxRange,
                     ChatRecipientPurpose.Speech,
                     ignoreXenos))
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } listener ||
                data.Range < 0 ||
                MessageRangeCheck(session, data, range) != MessageRangeCheckResult.Full ||
                !CanHearYautjaLocalSpeech(source, session, data))
                continue;

            if (whisper && (data.Observer || data.Range <= WhisperClearRange) == muffledOnly)
                continue;

            if (prototype.NeedsLOS && !data.Observer && listener != source && !data.HasLOS)
                continue;

            if (listener != source && !_language.CanUnderstand(listener, language))
                continue;

            result.Add(session, listener);
        }

        return result;
    }
}
