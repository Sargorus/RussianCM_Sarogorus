using System;
using System.IO;
using Content.Server._RuCM.Qualifications;
using Content.Shared.CCVar;
using Moq;
using Microsoft.Data.Sqlite;
using Npgsql;
using NUnit.Framework;
using Robust.Shared.Configuration;

namespace Content.Tests._RuCM.Qualifications;

[TestFixture]
public sealed class QualificationDatabaseConfigurationTests
{
    [TestCase("postgres")]
    [TestCase("POSTGRES")]
    public void ConnectionUsesExactlyTheGameDatabaseAndCredentials(string engine)
    {
        var configuration = new Mock<IConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseEngine)).Returns(engine);
        configuration.Setup(c => c.GetCVar(CCVars.DatabasePgHost)).Returns("game-db.internal");
        configuration.Setup(c => c.GetCVar(CCVars.DatabasePgPort)).Returns(55432);
        configuration.Setup(c => c.GetCVar(CCVars.DatabasePgDatabase)).Returns("cmu_existing_game");
        configuration.Setup(c => c.GetCVar(CCVars.DatabasePgUsername)).Returns("game_user");
        configuration.Setup(c => c.GetCVar(CCVars.DatabasePgPassword)).Returns("test;quoted=value");

        var connection = new NpgsqlConnectionStringBuilder(PostgresQualificationRepository.GameConnectionString(configuration.Object));
        Assert.Multiple(() =>
        {
            Assert.That(connection.Host, Is.EqualTo("game-db.internal"));
            Assert.That(connection.Port, Is.EqualTo(55432));
            Assert.That(connection.Database, Is.EqualTo("cmu_existing_game"));
            Assert.That(connection.Username, Is.EqualTo("game_user"));
            Assert.That(connection.Password, Is.EqualTo("test;quoted=value"));
        });
        configuration.VerifyAll();
        configuration.VerifyNoOtherCalls();
    }

    [TestCase("sqlite")]
    [TestCase("SQLITE")]
    public void SqliteUsesTheGameFileAndCannotCreateAnAlternateFile(string engine)
    {
        var root = Path.Combine(Path.GetTempPath(), "game user data");
        var configuration = new Mock<IConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseEngine)).Returns(engine);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseSqliteDbPath)).Returns("preferences.db");
        var connection = new SqliteConnectionStringBuilder(SqliteQualificationRepository.GameConnectionString(configuration.Object, root));
        Assert.Multiple(() =>
        {
            Assert.That(connection.DataSource, Is.EqualTo(Path.Combine(root, "preferences.db")));
            Assert.That(connection.Mode, Is.EqualTo(SqliteOpenMode.ReadWrite));
            Assert.That(connection.Pooling, Is.False);
            Assert.That(QualificationRepositoryFactory.Create(configuration.Object, root), Is.TypeOf<SqliteQualificationRepository>());
        });
        configuration.VerifyAll();
        configuration.VerifyNoOtherCalls();
    }

    [Test]
    public void RootedCustomGameSqlitePathIsPreserved()
    {
        var custom = Path.Combine(Path.GetTempPath(), "shared game storage", "preferences;quoted.db");
        var configuration = new Mock<IConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseEngine)).Returns("sqlite");
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseSqliteDbPath)).Returns(custom);
        var connection = new SqliteConnectionStringBuilder(SqliteQualificationRepository.GameConnectionString(configuration.Object, Path.GetTempPath()));
        Assert.That(connection.DataSource, Is.EqualTo(custom));
        configuration.VerifyAll();
        configuration.VerifyNoOtherCalls();
    }

    [Test]
    public void PrivateAnonymousSqliteMemoryCannotBecomeADifferentDatabase()
    {
        var configuration = new Mock<IConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseEngine)).Returns("sqlite");
        Assert.That(QualificationRepositoryFactory.Create(configuration.Object, null), Is.Null);
        configuration.VerifyAll();
        configuration.VerifyNoOtherCalls();
    }

    [Test]
    public void UnknownEngineDoesNotSelectAnyRepository()
    {
        var configuration = new Mock<IConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseEngine)).Returns("unknown");
        Assert.That(QualificationRepositoryFactory.Create(configuration.Object, Path.GetTempPath()), Is.Null);
        configuration.VerifyAll();
        configuration.VerifyNoOtherCalls();
    }

    [TestCase("sqlite")]
    [TestCase("unknown")]
    public void PostgresAdapterCannotFallBackToAnotherGameEngine(string engine)
    {
        var configuration = new Mock<IConfigurationManager>(MockBehavior.Strict);
        configuration.Setup(c => c.GetCVar(CCVars.DatabaseEngine)).Returns(engine);
        Assert.That(PostgresQualificationRepository.GameConnectionString(configuration.Object), Is.Null);
        configuration.Verify(c => c.GetCVar(CCVars.DatabaseEngine), Times.Once);
        configuration.VerifyNoOtherCalls();
    }
}
