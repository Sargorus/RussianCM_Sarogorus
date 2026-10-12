using Content.Shared.CMU14.Qualifications.Training;
using Robust.Client.Player;
using Robust.Shared.Utility;
using Robust.Client.UserInterface.Controls;
namespace Content.Client.UserInterface.Systems.Alerts.Controls;

public sealed partial class AlertControl
{
    private Label? _trainingLabel;

    private void CMUUpdateTrainingText()
    {
        if (Alert.ID is not ("CMUTrainingInstructor" or "CMUTrainingRecruit")) return;
        var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
        if (player == null || !_entityManager.TryGetComponent<CMUTrainingNavigationComponent>(player.Value, out var navigation))
        { DynamicMessage = null; if (_trainingLabel != null) _trainingLabel.Visible = false; return; }
        var location = navigation.Coordinates;
        var transform = _entityManager.System<SharedTransformSystem>();
        var own = transform.GetMapCoordinates(player.Value);
        var distance = location is { } target && target.MapId == own.MapId
            ? Loc.GetString("cmu-training-distance", ("distance", (int) (target.Position - own.Position).Length()))
            : Loc.GetString("cmu-training-distance-unavailable");
        // Alert tooltips parse markup; character names are untrusted.
        DynamicMessage = FormattedMessage.EscapeText(navigation.TargetName + " · " + distance);
        if (_trainingLabel == null)
        {
            _trainingLabel = new Label { Margin = new Thickness(66, 0, 0, 0),
                MaxWidth = 180, ClipText = true, MouseFilter = MouseFilterMode.Ignore };
            AddChild(_trainingLabel);
        }
        _trainingLabel.Visible = true;
        _trainingLabel.Text = Loc.GetString(Alert.Name) + "\n" + distance + "\n" + navigation.TargetName;
    }
}
