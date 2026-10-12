using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.CCCVars;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

// CMU14 class: TTS voice selection, ordered delivery and playback.
public sealed partial class HumanoidProfileEditor
{
    private TTSTab? _ttsTab;

    private void InitializeTTSControls()
    {
        _ttsTab = new TTSTab();
        _ttsTab.OnVoiceSelected += voice =>
        {
            if (Profile == null)
                return;
            Profile = Profile.WithTTSVoice(voice);
            _ttsTab.SetSelectedVoice(voice);
            SetDirty();
        };
        _ttsTab.OnPreviewRequested += voice => _entManager.System<TTSSystem>().RequestPreviewTTS(voice);
        TabContainer.AddChild(_ttsTab);
        TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("humanoid-profile-editor-voice-tab"));
    }

    private void UpdateTTSControls()
    {
        if (_ttsTab == null)
            return;

        var enabled = _cfgManager.GetCVar(CCCVars.TTSEnabled);
        var tabIndex = _ttsTab.GetPositionInParent();

        TabContainer.SetTabVisible(tabIndex, enabled);

        if (!enabled && TabContainer.CurrentTab == tabIndex)
            TabContainer.CurrentTab = 0;

        if (Profile != null)
        {
            var voice = HumanoidCharacterProfile.ValidateTTSVoice(Profile.TTSVoice, _prototypeManager, Profile.Sex);
            if (voice != Profile.TTSVoice)
            {
                Profile = Profile.WithTTSVoice(voice);
                SetDirty();
            }
            _ttsTab.UpdateControls(Profile, Profile.Sex);
        }
    }
}
