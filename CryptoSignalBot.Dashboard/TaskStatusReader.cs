using Microsoft.Win32.TaskScheduler;

namespace CryptoSignalBot.Dashboard;

internal sealed record ScheduledTaskStatus(
    string Name,
    bool Exists,
    string? State,
    DateTime? LastRunTime,
    DateTime? NextRunTime,
    int? LastTaskResult);

internal static class TaskStatusReader
{
    private static readonly string[] TaskNames =
    [
        "CryptoSignalBot Report Watchlist",
        "CryptoSignalBot Cleanup DB",
        "CryptoSignalBot Dashboard"
    ];

    public static IReadOnlyList<ScheduledTaskStatus> Read(string taskPath = @"\CryptoSignalBot\")
    {
        try
        {
            using var service = new TaskService();
            return TaskNames.Select(name => ReadTask(service, taskPath, name)).ToArray();
        }
        catch
        {
            return TaskNames
                .Select(name => new ScheduledTaskStatus(name, false, "unavailable", null, null, null))
                .ToArray();
        }
    }

    private static ScheduledTaskStatus ReadTask(TaskService service, string taskPath, string name)
    {
        var task = service.GetTask($"{taskPath.TrimEnd('\\')}\\{name}");
        if (task is null)
        {
            return new ScheduledTaskStatus(name, false, null, null, null, null);
        }

        return new ScheduledTaskStatus(
            name,
            true,
            task.State.ToString(),
            task.LastRunTime == DateTime.MinValue ? null : task.LastRunTime,
            task.NextRunTime == DateTime.MinValue ? null : task.NextRunTime,
            task.LastTaskResult);
    }
}
