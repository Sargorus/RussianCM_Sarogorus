# TTS

Character profiles support an explicit voice or `random`. New profiles default to
`random`; the server resolves that preference when applying the profile, using
the humanoid's physical sex rather than grammatical gender. Randomized humanoid
profiles, including ghost roles, receive a concrete random voice. The assigned
voice remains stable while the entity speaks.

The editor and server share the same selection rules: round-start voices only,
no sponsor-only voices, matching sex or neutral voices. Automatic assignment
prefers voices matching the character's sex and uses neutral voices when needed.
Unsexed characters can use either sex. Custom recordings have no sex metadata
and remain explicit choices. Existing compatible choices are preserved; invalid
or incompatible saved choices become `random`. Entity prototypes can still set
fixed voices, including special voices unavailable in the editor.

Synthesis shares identical pending requests and caches WAV responses. Up to 16
requests execute concurrently, with another 64 waiting. Requests waiting at
least 8 seconds are discarded before calling the API. Restarting the round or
disabling TTS cancels active synthesis and clears waiting work. Delivery follows
speech order even when API responses finish in a different order. Whisper and
muffled-whisper synthesis can run concurrently. Recordings ready more than 15
seconds after the speech event are not delivered. Radio speech uses voice masks.

Playback queues local speech per speaker, radio speech in one radio lane, and
announcements in a separate lane. A new preview replaces the previous preview.
There are at most 6 waiting messages per lane, 48 in total, and 32 MiB of queued
audio. Messages waiting at least 12 seconds are discarded. Disabling TTS,
muting it, or restarting the round stops audio and clears playback queues.
Streams are disposed after playback, including playback in the lobby.

The cache uses least-recently-used eviction and is bounded by `tts.max_cache`
and 64 MiB. A zero entry limit disables caching. Each API response is bounded to
8 MiB and checked for valid WAV structure before caching. Resetting the cache
also invalidates cache inserts from requests that started before the reset.

## Verification

Unit regressions: `CMUTTSVoiceSelectionTest`, `CMUTTSSynthesisQueueTest`,
`CMUTTSDeliveryOrderTest`, `CMUTTSPlaybackQueueTest`, `CMUTTSManagerTest`.

Game integration regressions: `CMUTTSVoiceAssignmentTest` covers random ghost-role
profiles, map initialization, applying a profile with a new sex, and preserving
fixed or explicitly selected voices.

Manual checks with a configured TTS API:

1. Create male and female characters, select random voices, and spawn. Check that
   voices match their sex and remain stable across multiple messages.
2. Join randomized humanoid ghost roles and compare their voices.
3. Change the character's sex in the editor. Check filtering, categories, search,
   previews, and saving; custom recordings remain available.
4. Speak several short messages rapidly. Check their order and lack of overlap
   from one speaker; a second nearby speaker can speak independently.
5. Check local speech, clear and muffled whispers, radio with a voice mask, and
   announcements. Radio should have a mild effect without echo or pitch wobble.
6. During playback, mute TTS, disable it, or restart the round. No old speech
   should start afterward. Exercise API timeouts and a brief traffic burst.
