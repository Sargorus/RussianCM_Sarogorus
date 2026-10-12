using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Content.Client._RuCM.Qualifications;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests._RuCM.Qualifications;
using Content.Shared._RuCM.Qualifications;
using Content.Shared.CMU14.Qualifications.Training;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.CMU14.Qualifications;

[TestFixture, NonParallelizable]
public sealed class CMUTrainingUiTests : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task TrainingRosterAndTopicContractsRoundtripThroughNativeSerialization()
    {
        var recruit = Guid.NewGuid();
        var instructor = Guid.NewGuid();
        var view = new QualificationView { Viewer = instructor, Target = recruit, TrainingInstructor = true };
        view.TrainingRecruits.Add(new()
        {
            Recruit = recruit,
            Instructor = instructor,
            Name = "Petrov",
            Distance = 27,
            Topic = "CMUBasicEngineeringTraining",
            TopicName = "cmu-training-topic-engineering",
            Completed = 2,
            Required = 9
        });
        view.TrainingTopics.Add(new()
        {
            Id = "CMUBasicEngineeringTraining",
            Name = "cmu-training-topic-engineering",
            Skills = new() { ["Engineering"] = 2 }
        });
        view.TrainingInstructors[instructor] = "Ivanov";
        byte[] bytes = null!;
        await Server.WaitAssertion(() =>
        {
            using var stream = new System.IO.MemoryStream();
            Server.ResolveDependency<IRobustSerializer>().Serialize(stream, new QualificationBoundView(view));
            bytes = stream.ToArray();
        });
        await Client.WaitAssertion(() =>
        {
            using var stream = new System.IO.MemoryStream(bytes);
            var decoded = Client.ResolveDependency<IRobustSerializer>().Deserialize<QualificationBoundView>(stream).View;
            Assert.That(decoded.TrainingInstructor, Is.True);
            Assert.That(decoded.TrainingRecruits.Single().Instructor, Is.EqualTo(instructor));
            Assert.That(decoded.TrainingRecruits.Single().Distance, Is.EqualTo(27));
            Assert.That(decoded.TrainingTopics.Single().Skills["Engineering"], Is.EqualTo(2));
            Assert.That(decoded.TrainingInstructors[instructor], Is.EqualTo("Ivanov"));
        });
    }

    [Test]
    public async Task StartRequiresConfirmationAndNamesExactlyOneRecruitAndTopic()
    {
        await Client.WaitAssertion(() =>
        {
            var view = QualificationPreviewCommand.CreateView(Client.ResolveDependency<IPrototypeManager>());
            view.TrainingInstructor = true;
            view.TrainingRecruits.Add(new CMUTrainingRosterEntry
            { Recruit = view.Target, Instructor = view.Viewer, Name = "Petrov", Available = true });
            view.TrainingTopics.Add(new CMUTrainingTopicView
            {
                Id = "CMUBasicEngineeringTraining",
                Name = "cmu-training-topic-engineering",
                Skills = new() { ["Engineering"] = 2, ["Construction"] = 2 }
            });
            var sent = new List<(QualificationAction Action, QualificationRequest Request)>();
            using var window = new QualificationWindow((action, request) => sent.Add((action, request)));
            window.Open();
            window.Update(view);
            Layout(window);
            QualificationUiTests.Click(Children(window).OfType<Button>().Single(b => b.Name == "nav-training"));
            Layout(window);
            var start = Children(window).OfType<Button>().Single(b => b.Name == "training-start");
            Assert.That(start.Disabled, Is.True);
            QualificationUiTests.Click(Children(window).OfType<CheckBox>().Single(b => b.Name == "training-confirm"));
            Assert.That(start.Disabled, Is.False);
            QualificationUiTests.Click(start);
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].Action, Is.EqualTo(QualificationAction.TrainingStart));
            Assert.That(sent[0].Request.Target, Is.EqualTo(view.Target));
            Assert.That(sent[0].Request.Qualification, Is.EqualTo("CMUBasicEngineeringTraining"));
            Assert.That(sent[0].Request.Payload, Is.Empty, "Client never supplies gameplay skills");
            Assert.That(start.Disabled, Is.True);
            view.ResponseId = sent[0].Request.RequestId;
            window.Update(view);
            Assert.That(Children(window).OfType<CheckBox>().Single(b => b.Name == "training-confirm").Pressed, Is.False);
        });
    }

    private static IEnumerable<Control> Children(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }

    private static void Layout(QualificationWindow window)
    {
        var size = new Vector2(1080, 740);
        foreach (var control in Children(window).Prepend(window))
        {
            control.InvalidateMeasure();
            control.InvalidateArrange();
        }
        window.SetSize = size;
        window.Measure(size);
        window.Arrange(new UIBox2(Vector2.Zero, size));
    }
}
