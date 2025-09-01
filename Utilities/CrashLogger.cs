// ============================================================================
//  ApolloGUI — CrashLogger.cs
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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ApolloGUI.Utilities
{
    public static class CrashLogger
    {
        private static string? _logPath;
        private static bool _initialized;

        public static void Init(Application app)
        {
            if (_initialized) return;
            _initialized = true;

            _logPath = GetLogPath();

            // UI thread exceptions
            app.DispatcherUnhandledException += (s, e) =>
            {
                try { LogException("DispatcherUnhandledException", e.Exception); }
                catch { /* ignore */ }
                // Let’s show a friendly message and continue if possible.
                MessageBox.Show($"An unexpected error occurred.\n\nDetails were written to:\n{_logPath}",
                                "ApolloGUI Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true; // keep app alive when feasible
            };

            // Non-UI thread exceptions
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = e.ExceptionObject as Exception;
                    LogException("AppDomain.UnhandledException", ex, isTerminating: e.IsTerminating);
                }
                catch { /* ignore */ }
            };

            // Task exceptions
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                try { LogException("TaskScheduler.UnobservedTaskException", e.Exception); }
                catch { /* ignore */ }
                e.SetObserved();
            };
        }

        public static void LogException(string source, Exception? ex, bool isTerminating = false)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GetLogPath())!);
                using var sw = new StreamWriter(GetLogPath(), append: true, Encoding.UTF8);
                sw.WriteLine(new string('=', 80));
                sw.WriteLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                sw.WriteLine($"Source: {source}  Terminating: {isTerminating}");
                sw.WriteLine($"Process: {Process.GetCurrentProcess().ProcessName} ({Environment.ProcessId})");
                sw.WriteLine($"Version: {GetVersionString()}");
                sw.WriteLine($"OS: {Environment.OSVersion}  64bit: {Environment.Is64BitProcess}");
                sw.WriteLine($"CLR: {Environment.Version}");
                if (ex != null)
                {
                    WriteExceptionRecursive(sw, ex, 0);
                }
                else
                {
                    sw.WriteLine("Exception object is null.");
                }
                sw.WriteLine();
            }
            catch
            {
                // As a last resort, try EventLog or Debug
                try { Debug.WriteLine($"[CrashLogger] {source}: {ex}"); } catch { }
            }
        }

        private static void WriteExceptionRecursive(StreamWriter sw, Exception ex, int depth)
        {
            var pad = new string(' ', depth * 2);
            sw.WriteLine($"{pad}{ex.GetType().FullName}: {ex.Message}");
            sw.WriteLine($"{pad}{ex.StackTrace}");
            if (ex.Data != null && ex.Data.Count > 0)
            {
                sw.WriteLine($"{pad}Data:");
                foreach (var key in ex.Data.Keys)
                    sw.WriteLine($"{pad}  {key}: {ex.Data[key]}");
            }
            if (ex.InnerException != null)
            {
                sw.WriteLine($"{pad}Inner:");
                WriteExceptionRecursive(sw, ex.InnerException, depth + 1);
            }
        }

        private static string GetLogPath()
        {
            if (!string.IsNullOrEmpty(_logPath)) return _logPath!;
            try
            {
                var exe = Assembly.GetExecutingAssembly().Location;
                var folder = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(folder))
                    return _logPath = Path.Combine(folder!, "ApolloGUI_Crash.log");
            }
            catch { /* ignore and fall back */ }

            var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                     "ApolloGUI");
            return _logPath = Path.Combine(local, "ApolloGUI_Crash.log");
        }

        private static string GetVersionString()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var v = asm.GetName().Version;
                return v != null ? v.ToString() : "(no version)";
            }
            catch
            {
                return "(unknown)";
            }
        }
    }
}
