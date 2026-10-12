using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;

namespace Content.Shared.Roles;

public sealed partial class JobPrototype
{
    /// <summary>Character-setup preset tabs containing this job. Empty means all normal tabs.</summary>
    [DataField]
    public HashSet<string> CharacterSetupPresets { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsAvailableInCharacterSetup(string preset) =>
        CharacterSetupPresets.Count == 0 || CharacterSetupPresets.Contains(preset);
}
