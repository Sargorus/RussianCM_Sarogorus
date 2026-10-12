using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.CMU14.Qualifications.Training;
using Robust.Shared.Localization;
namespace Content.Client._RuCM.Qualifications;

public sealed partial class QualificationWindow
{
    private string _trainingTopic = "";
    private string _trainingInstructor = "";
    private static string T(string id) => Loc.GetString("cmu-training-" + id);

    private void RenderCMURecruitStatus()
    {
        var recruit = _view.TrainingRecruits.FirstOrDefault(r => r.Recruit == _view.Target);
        if (recruit == null) return;
        var status = Card(_content, T("current-topic"));
        Text(status, recruit.Instructor is { } instructor
            ? Loc.GetString("cmu-training-assigned-to", ("name", _view.TrainingInstructors.GetValueOrDefault(instructor, AccountName(instructor))))
            : T("unassigned"));
        if (recruit.Topic.Length > 0) Text(status, Loc.GetString(recruit.TopicName));
    }

    private void RenderCMUTrainingAssignments()
    {
        if (!_view.TrainingInstructor && !_view.Management && !_view.TrainingRecruits.Any(r => r.Recruit == _view.Viewer))
            return;
        var mine = Card(_content, T(_view.Management ? "all-recruits" : "my-recruits"));
        var free = Card(_content, T("free-recruits"));
        foreach (var recruit in _view.TrainingRecruits)
        {
            var list = recruit.Instructor == null ? free : mine;
            var row = Card(list, recruit.Name);
            Text(row, recruit.Distance is { } distance ? Loc.GetString("cmu-training-distance", ("distance", (int) distance)) : T("distance-unavailable"));
            Text(row, Loc.GetString("cmu-training-progress", ("completed", recruit.Completed), ("required", recruit.Required)));
            Text(row, recruit.Topic.Length > 0 ? T("state-training") : recruit.Available ? T("state-available") : T("state-unavailable"));
            if (recruit.Topic.Length > 0)
                Text(row, Loc.GetString(recruit.TopicName));
            if (recruit.Instructor is { } instructor)
                Text(row, Loc.GetString("cmu-training-assigned-to", ("name", _view.TrainingInstructors.GetValueOrDefault(instructor, AccountName(instructor)))));
            if (_view.TrainingInstructor || _view.Management)
                Button(row, T("select"), () => Send(QualificationAction.View, new() { Target = recruit.Recruit }), () => Ready, name: "training-select-" + recruit.Recruit);
        }
        var selected = _view.TrainingRecruits.FirstOrDefault(r => r.Recruit == _view.Target);
        if (selected == null) return;
        var controls = Card(_content, T("selected-recruit"));
        Text(controls, selected.Name);
        if (_view.Management)
        {
            var instructors = _view.TrainingInstructors.Select(i => new KeyValuePair<string, string>(i.Key.ToString(), i.Value)).ToArray();
            if (!instructors.Any(i => i.Key == _trainingInstructor)) _trainingInstructor = instructors.FirstOrDefault().Key ?? "";
            Select(controls, instructors, _trainingInstructor, id => _trainingInstructor = id);
            Button(controls, T("reassign"), () => Send(QualificationAction.TrainingAssign,
                new() { Target = _view.Target, Instructor = Guid.Parse(_trainingInstructor) }),
                () => Ready && Guid.TryParse(_trainingInstructor, out _), name: "training-reassign");
        }
        if (selected.Instructor == null && _view.TrainingInstructor)
            Button(controls, T("take"), () => Send(QualificationAction.TrainingAssign, Request()), () => Ready, name: "training-take");
        var owns = selected.Instructor == _view.Viewer && _view.TrainingInstructor;
        if (owns || _view.Management)
            Button(controls, T("release"), () => Send(QualificationAction.TrainingRelease, Request()), () => Ready && selected.Instructor != null, name: "training-release");
        if (!owns) return;
        Button(controls, T("track"), () => Send(QualificationAction.TrainingTrack, Request()), () => Ready, name: "training-track");
        if (selected.Topic.Length > 0)
        {
            Text(controls, T("current-topic") + ": " + Loc.GetString(selected.TopicName));
            Button(controls, T("finish"), () => Send(QualificationAction.TrainingFinish, Request()), () => Ready, name: "training-finish");
        }
        if (!_view.TrainingTopics.Any()) return;
        var topics = _view.TrainingTopics.Select(t => new KeyValuePair<string, string>(t.Id, Loc.GetString(t.Name))).ToArray();
        if (!topics.Any(t => t.Key == _trainingTopic)) _trainingTopic = topics[0].Key;
        Select(controls, topics, _trainingTopic, id => { _trainingTopic = id; _checks.Remove(_scope + "/training-confirm"); Render(); });
        var topic = _view.TrainingTopics.Single(t => t.Id == _trainingTopic);
        Text(controls, T("temporary-access"));
        foreach (var (skill, level) in topic.Skills) Text(controls, skill + " (" + level + ")");
        Text(controls, T("temporary-help"));
        var confirm = Check(controls, "training-confirm", T("confirm"), false);
        Button(controls, T("start"), () => Send(QualificationAction.TrainingStart,
            new() { Target = _view.Target, Qualification = _trainingTopic }),
            () => Ready && confirm.Pressed, name: "training-start");
    }
}
