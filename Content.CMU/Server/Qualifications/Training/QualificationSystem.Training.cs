using Content.Server.CMU14.Qualifications.Training;
namespace Content.Server._RuCM.Qualifications;
public sealed partial class QualificationSystem
{
    [Dependency] private CMUGovforTrainingSystem _training = default!;
    public void RefreshTrainingViews() => RefreshOpenViews();
}
