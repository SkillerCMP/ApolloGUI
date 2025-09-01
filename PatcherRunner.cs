// ============================================================================
//  ApolloGUI — PatcherRunner.cs
//  Purpose: TODO: brief purpose of this file.
//  Key types: TODO: key types/classes used.
//  Notes: TODO: important usage and gotchas.
//  Version: v1.0.0   Date: 2025-08-31
//  Copyright (c) 2025 Skiller S
// ============================================================================
//  Change Log:
//   - v1.0.0 (2025-08-31): Repository-ready header added.
// ============================================================================

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ApolloGUI
{
    public static class PatcherRunner
    {
        public sealed class Result
        {
            public int ExitCode { get; init; }
            public string StdOut { get; init; } = "";
            public string StdErr { get; init; } = "";
            public bool Success => ExitCode == 0;
        }

        public static async Task<Result> RunAsync(
            string patcherExePath,
            string savepatchPath,
            string selection,
            string? dataFilePath,
            string workingDirectory)
        {
            patcherExePath = Path.GetFullPath(patcherExePath, workingDirectory);
            savepatchPath  = Path.GetFullPath(savepatchPath,  workingDirectory);
            if (dataFilePath != null) dataFilePath = Path.GetFullPath(dataFilePath, workingDirectory);

            if (!File.Exists(patcherExePath)) throw new FileNotFoundException("Patcher not found.", patcherExePath);
            if (!File.Exists(savepatchPath)) throw new FileNotFoundException("Savepatch not found.", savepatchPath);
            if (dataFilePath != null && !File.Exists(dataFilePath)) throw new FileNotFoundException("Data file not found.", dataFilePath);

            var psi = new ProcessStartInfo
            {
                FileName = patcherExePath,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            psi.ArgumentList.Add(savepatchPath);
            psi.ArgumentList.Add(selection);
            if (dataFilePath != null) psi.ArgumentList.Add(dataFilePath);

            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
            proc.ErrorDataReceived  += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
            proc.Exited += (_, __) => tcs.TrySetResult(proc.ExitCode);

            if (!proc.Start()) throw new InvalidOperationException("Failed to start patcher process.");
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMinutes(5));
            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(-1, cts.Token));
            }
            catch (TaskCanceledException)
            {
                try { if (!proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                throw new TimeoutException("Patcher process timed out.");
            }

            var exit = await tcs.Task;
            return new Result { ExitCode = exit, StdOut = stdout.ToString(), StdErr = stderr.ToString() };
        }
    }
}
