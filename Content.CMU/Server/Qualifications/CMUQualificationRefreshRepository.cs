using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RuCM.Qualifications;

namespace Content.Server._RuCM.Qualifications;

/// <summary>Null means an existing stored revision is unchanged; a missing row is a storage failure.</summary>
public interface ICMUQualificationRefreshRepository
{
    Task<QualificationStore?> LoadIfChanged(long revision, CancellationToken cancel = default);
}
