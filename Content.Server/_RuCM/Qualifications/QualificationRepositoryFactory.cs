using System;
using Robust.Shared.Configuration;
using Content.Shared.CCVar;

namespace Content.Server._RuCM.Qualifications;

/// <summary>Selects only the game's configured database, never a qualification-specific database.</summary>
public static class QualificationRepositoryFactory
{
    public static IRuCMQualificationRepository? Create(IConfigurationManager configuration, string? userDataRoot)
    {
        var engine = configuration.GetCVar(CCVars.DatabaseEngine);
        if (string.Equals(engine, "postgres", StringComparison.OrdinalIgnoreCase))
            return new PostgresQualificationRepository(PostgresQualificationRepository.GameConnectionString(configuration)!);
        if (string.Equals(engine, "sqlite", StringComparison.OrdinalIgnoreCase) &&
            SqliteQualificationRepository.GameConnectionString(configuration, userDataRoot) is { } connection)
            return new SqliteQualificationRepository(connection);

        // The upstream anonymous :memory: connection is private; a second connection would be another DB.
        return null;
    }
}
