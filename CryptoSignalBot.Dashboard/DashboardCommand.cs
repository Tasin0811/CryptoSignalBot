using System.Diagnostics;

namespace CryptoSignalBot.Dashboard;

internal sealed record CommandResult(
    bool Ok,
    string Command,
    int ExitCode,
    string Output,
    string Error);

internal sealed class DashboardCommand(string name, string projectRoot, string[] workerArgs)
{
    public static bool TryCreate(string commandName, string projectRoot, out DashboardCommand command)
    {
        var normalized = commandName.Trim().ToLowerInvariant();
        string[] args = normalized switch
        {
            "report-watchlist" => ["--report-watchlist", "--force-report"],
            "paper-trade-report" => ["--paper-trade-report"],
            "backtest-report" => ["--backtest-report"],
            "cleanup-db" => ["--cleanup-db"],
            _ => []
        };

        command = new DashboardCommand(normalized, projectRoot, args);
        return args.Length > 0;
    }

    public async Task<CommandResult> RunAsync(CancellationToken cancellationToken)
    {
        var workerProject = Path.Combine(projectRoot, "CryptoSignalBot.Worker", "CryptoSignalBot.Worker.csproj");
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(workerProject);
        startInfo.ArgumentList.Add("--");
        foreach (var arg in workerArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossibile avviare il worker.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;
        return new CommandResult(
            process.ExitCode == 0,
            name,
            process.ExitCode,
            TrimForUi(output),
            TrimForUi(error));
    }

    private static string TrimForUi(string value)
    {
        const int maxLength = 6000;
        return value.Length <= maxLength
            ? value
            : value[^maxLength..];
    }
}
