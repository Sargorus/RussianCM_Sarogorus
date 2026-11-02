using System.Numerics;
using Content.Shared.CMU14.Robotics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client.CMU14.Robotics;

public sealed class CMUMedibotConfigurationWindow : DefaultWindow
{
    public event Action<int, bool, bool, bool>? OnApply;

    private readonly Label _beaker;
    private readonly SpinBox _amount;
    private readonly CheckBox _brute;
    private readonly CheckBox _burn;
    private readonly CheckBox _critical;

    public CMUMedibotConfigurationWindow()
    {
        Title = Loc.GetString("cmu-medibot-config-title");
        MinSize = new Vector2(420, 300);
        SetSize = new Vector2(460, 340);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };
        Contents.AddChild(root);

        _beaker = new Label();
        root.AddChild(_beaker);

        root.AddChild(new Label { Text = Loc.GetString("cmu-medibot-config-amount") });
        _amount = new SpinBox
        {
            Value = 15,
            IsValid = value => value is >= 1 and <= 50,
        };
        root.AddChild(_amount);

        _brute = new CheckBox
        {
            Text = Loc.GetString("cmu-medibot-config-brute"),
            Pressed = true,
        };
        root.AddChild(_brute);

        _burn = new CheckBox
        {
            Text = Loc.GetString("cmu-medibot-config-burn"),
            Pressed = true,
        };
        root.AddChild(_burn);

        _critical = new CheckBox
        {
            Text = Loc.GetString("cmu-medibot-config-critical"),
            Pressed = true,
        };
        root.AddChild(_critical);

        var apply = new Button { Text = Loc.GetString("cmu-medibot-config-apply") };
        apply.OnPressed += _ => OnApply?.Invoke(_amount.Value, _brute.Pressed, _burn.Pressed, _critical.Pressed);
        root.AddChild(apply);
    }

    public void UpdateState(CMUMedibotConfigurationBuiState state)
    {
        _beaker.Text = state.BeakerText;
        _amount.OverrideValue(state.InjectAmount);
        _brute.Pressed = state.InjectOnBrute;
        _burn.Pressed = state.InjectOnBurn;
        _critical.Pressed = state.InjectOnCritical;
    }
}
