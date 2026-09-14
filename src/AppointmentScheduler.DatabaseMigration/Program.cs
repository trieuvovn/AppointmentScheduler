using AppointmentScheduler.DatabaseMigration;
using Microsoft.Extensions.Configuration;

var includeDemoData = args.Any(a =>
    a.Equals("--demo", StringComparison.OrdinalIgnoreCase));

// --demo must not reach AddCommandLine: its default parser has no notion of a value-less flag,
// so it greedily consumes whatever token follows --demo as --demo's "value" — corrupting the
// parse of --connection-string whenever --demo happens to be positioned right before it.
// --demo is already handled above by checking for it directly in args.
var commandLineArgs = args
    .Where(a => !a.Equals("--demo", StringComparison.OrdinalIgnoreCase))
    .ToArray();

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(commandLineArgs, new Dictionary<string, string>
    {
        ["--connection-string"] = "ConnectionStrings:AppointmentScheduler",
        ["-c"] = "ConnectionStrings:AppointmentScheduler",
    })
    .Build();

var connectionString = configuration.GetConnectionString("AppointmentScheduler");

if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine(
        "No connection string. Pass --connection-string \"<value>\", set " +
        "ConnectionStrings__AppointmentScheduler, or add one to appsettings.json.");
    return 2;
}

Console.WriteLine("Applying schema and reference-data scripts.");

var result = DatabaseMigrator.Run(connectionString);

if (!result.Successful)
{
    Console.Error.WriteLine($"Migration failed: {result.Error}");
    return 1;
}

Console.WriteLine(!result.Scripts.Any()
    ? "Database already up to date. No scripts applied."
    : $"Applied {result.Scripts.Count()} script(s).");

if (!includeDemoData)
{
    return 0;
}

// A deliberately separate call, not a flag folded into the run above (plan 8's risk table:
// "a demo script reaches production"). --demo has to be typed explicitly for this to run at all.
Console.WriteLine("Applying demo scripts.");

var demoResult = DatabaseMigrator.RunDemoData(connectionString);

if (!demoResult.Successful)
{
    Console.Error.WriteLine($"Demo data failed: {demoResult.Error}");
    return 1;
}

Console.WriteLine(!demoResult.Scripts.Any()
    ? "Demo data already present. No scripts applied."
    : $"Applied {demoResult.Scripts.Count()} demo script(s).");

return 0;
