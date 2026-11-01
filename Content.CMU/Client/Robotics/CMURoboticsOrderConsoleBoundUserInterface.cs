using Content.Shared.CMU14.Robotics;
using Robust.Client.UserInterface;

namespace Content.Client.CMU14.Robotics;

public sealed class CMURoboticsOrderConsoleBoundUserInterface : BoundUserInterface
{
    private CMURoboticsOrderConsoleWindow? _window;

    public CMURoboticsOrderConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CMURoboticsOrderConsoleWindow>();
        _window.OnAccept += id => SendMessage(new CMURoboticsAcceptOrderMessage(id));
        _window.OnSubmit += () => SendMessage(new CMURoboticsSubmitOrderMessage());
        _window.OnCancel += () => SendMessage(new CMURoboticsCancelOrderMessage());
        _window.OnRefresh += () => SendMessage(new CMURoboticsRefreshOrdersMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is CMURoboticsOrderConsoleState consoleState)
            _window?.UpdateState(consoleState);
    }
}
