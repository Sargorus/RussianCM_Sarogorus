using System;
using Robust.Shared.Serialization;
namespace Content.Shared.CMU14.Qualifications.Training;
[Serializable, NetSerializable]
public sealed class CMUTrainingRosterEntry
{
    public Guid Recruit;
    public Guid? Instructor;
    public string Name = "";
    public string Topic = "";
    public string TopicName = "";
    public float? Distance;
    public int Completed;
    public int Required;
    public bool Available;
}
[Serializable, NetSerializable]
public sealed class CMUTrainingTopicView
{
    public string Id = "";
    public string Name = "";
    public string Qualification = "";
    public string Item = "";
    public Dictionary<string, int> Skills = new();
}
