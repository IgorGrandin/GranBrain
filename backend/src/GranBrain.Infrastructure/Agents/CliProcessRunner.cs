using System.Diagnostics;
using GranBrain.Core.Interfaces;
using GranBrain.Core.Security;

namespace GranBrain.Infrastructure.Agents;

public sealed class CliProcessRunner : ICliProcessRunner
{
    private readonly VaultPathGuard _guard;

    public CliProcessRunner(VaultPathGuard guard)
    {
        _guard = guard;
    }

    public bool IsExecutableAvailable(string executableOrCommand)
    {
        if (string.IsNullOrWhiteSpace(executableOrCommand))
        {
            return false;
        }

        if (Path.IsPathRooted(executableOrCommand))
        {
            return File.Exists(executableOrCommand);
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var extensions = OperatingSystem.IsWindows()
            ? new[] { "", ".exe", ".cmd", ".bat" }
            : new[] { "" };

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var ext in extensions)
            {
                try
                {
                    var full = Path.Combine(dir.Trim(), executableOrCommand + ext);
                    if (File.Exists(full))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore malformed PATH entries
                }
            }
        }

        return false;
    }

    public async Task<CliProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        var safeWorkingDir = _guard.ResolveSafePath(workingDirectory);
        if (!IsExecutableAvailable(executable))
        {
            return new CliProcessResult(false, -1, string.Empty, $"Executable '{executable}' is not available.");
        }

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = safeWorkingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = psi };
        try
        {
            if (!process.Start())
            {
                return new CliProcessResult(false, -1, string.Empty, "Failed to start CLI process.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds)));

            var stdOutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stdErrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);
            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            return new CliProcessResult(true, process.ExitCode, stdOut, stdErr);
        }
        catch (Exception ex)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            return new CliProcessResult(false, -1, string.Empty, ex.Message);
        }
    }
}
