using System;
using Content.Server.EUI;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.Eui;

namespace Content.Server._RuCM.Qualifications;

public sealed class QualificationEui : BaseEui
{
    private readonly QualificationSystem _system;
    private Guid _target;
    private string _error = "";
    private Guid _responseId;
    public QualificationEui(QualificationSystem system, Guid? target = null) { _system = system; _target = target ?? Guid.Empty; }
    public override void Opened() { if (_target == Guid.Empty) _target = Player.UserId; StateDirty(); }
    public override void Closed() { _system.Remove(this); }
    public override EuiStateBase GetNewState()
    { var view = _system.View(Player, _target, _error); view.ResponseId = _responseId; return new QualificationEuiState(view); }
    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (msg is not QualificationEuiRequest request) return;
        _system.Submit(Player, request.Action, request.Request, error => { if (IsShutDown) return; _error = error; _responseId = request.Request?.RequestId ?? Guid.Empty; StateDirty(); }, target => _target = target);
    }
}
