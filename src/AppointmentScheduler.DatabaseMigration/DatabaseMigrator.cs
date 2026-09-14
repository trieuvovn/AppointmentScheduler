using System.Reflection;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;

namespace AppointmentScheduler.DatabaseMigration;

public static class DatabaseMigrator
{
    private const string ScriptsNamespace = "AppointmentScheduler.DatabaseMigration.Scripts.";
    private const string DemoScriptsNamespace = "AppointmentScheduler.DatabaseMigration.Scripts.Demo.";

    public static DatabaseUpgradeResult Run(string connectionString, IUpgradeLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        log ??= new ConsoleUpgradeLog();

        // Idempotent: a no-op when the database is already there, which is the usual case.
        EnsureDatabase.For.SqlDatabase(connectionString, log);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.StartsWith(ScriptsNamespace, StringComparison.Ordinal)
                    && !name.StartsWith(DemoScriptsNamespace, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .WithVariablesDisabled()
            .LogTo(log)
            .Build();

        return upgrader.PerformUpgrade();
    }

    public static DatabaseUpgradeResult RunDemoData(string connectionString, IUpgradeLog? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        log ??= new ConsoleUpgradeLog();

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                name => name.StartsWith(DemoScriptsNamespace, StringComparison.Ordinal))
            .WithTransactionPerScript()
            .WithVariablesDisabled()
            .LogTo(log)
            .Build();

        return upgrader.PerformUpgrade();
    }
}
