using Content.Client._RuCM.Guidebook;
using Content.Shared._RuCM.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;

namespace Content.Client._RuCM.Qualifications;

public sealed partial class QualificationWindow
{
    private void AddTrainingMaterials(BoxContainer parent, string qualification, string item)
    {
        var guides = GOVFORTrainingGuides.ForChecklistItem(qualification, item);
        if (guides.Length == 0)
            return;

        Text(parent, L("reference-materials"));
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        foreach (var id in guides)
        {
            if (!prototypes.TryIndex<GuideEntryPrototype>(id, out var guide))
                continue;

            Text(parent, Name(guide.Name));
            Button(parent, L("open-material"),
                () => IoCManager.Resolve<IEntityManager>().System<GOVFORGuidebookSystem>().OpenDocument(id),
                name: "qualification-guide-" + qualification + "-" + item + "-" + id);
        }
    }
}
