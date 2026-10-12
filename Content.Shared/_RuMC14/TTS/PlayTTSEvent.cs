using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.TTS;

[Serializable, NetSerializable]
// ReSharper disable once InconsistentNaming
// CMU14 class: TTS voice selection, ordered delivery and playback.
public sealed class PlayTTSEvent : EntityEventArgs
{
    public byte[] Data { get; }
    public NetEntity? SourceUid { get; }
    /// <summary>Speaker identity for playback queues, even when the spatial source is outside PVS.</summary>
    public NetEntity? SpeakerUid { get; }
    public bool IsWhisper { get; }

    public bool IsRadio { get; }
    public bool IsAnnouncement { get; }
    public uint PlaybackId { get; }

    public PlayTTSEvent(
        byte[] data,
        NetEntity? sourceUid = null,
        bool isWhisper = false,
        bool isRadio = false,
        uint playbackId = 0,
        NetEntity? speakerUid = null,
        bool isAnnouncement = false)
    {
        Data = data;
        SourceUid = sourceUid;
        IsWhisper = isWhisper;
        IsRadio = isRadio;
        PlaybackId = playbackId;
        SpeakerUid = speakerUid ?? sourceUid;
        IsAnnouncement = isAnnouncement;
    }
}

[Serializable, NetSerializable]
// CMU14 class: TTS voice selection, ordered delivery and playback.
public sealed class TTSPlaybackFinishedEvent(uint playbackId, NetEntity? sourceUid, bool played) : EntityEventArgs
{
    public uint PlaybackId { get; } = playbackId;
    public NetEntity? SourceUid { get; } = sourceUid;
    public bool Played { get; } = played;
}
