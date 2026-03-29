#nullable enable

using Rampastring.Tools;
using System;
using System.IO;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    internal sealed class MatchmakingLogger
    {
        private readonly Func<string>? localPlayerNameProvider;
        private readonly string logFilePath;
        private readonly object fileLock = new object();

        public MatchmakingLogger(Func<string>? localPlayerNameProvider)
        {
            this.localPlayerNameProvider = localPlayerNameProvider;
            
            // Log to Client/Logs/Matchmaking.log
            string logDir = Path.Combine(ClientCore.ProgramConstants.GamePath, "Client", "Logs");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);
                
            logFilePath = Path.Combine(logDir, "Matchmaking.log");
            
            // Write a start session marker
            LogToFile("--- NEW MATCHMAKING SESSION STARTED ---");
        }

        public void Info(string eventName, string? details = null) =>
            Log("INFO", eventName, details);

        public void Warn(string eventName, string? details = null) =>
            Log("WARN", eventName, details);

        public void Error(string eventName, Exception ex, string? details = null) =>
            Log("ERROR", eventName, $"{details} :: {ex}");

        private void Log(string level, string eventName, string? details)
        {
            string formatted = Format(level, eventName, details);
            
            // Also log to the main client.log for redundancy
            Logger.Log(formatted);
            
            // Log to our dedicated file
            LogToFile(formatted);
        }

        private void LogToFile(string message)
        {
            try
            {
                lock (fileLock)
                {
                    string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                    File.AppendAllText(logFilePath, $"[{timestamp}] {message}{Environment.NewLine}");
                }
            }
            catch { /* Ignore logging failures to prevent app crashes */ }
        }

        private string Format(string level, string eventName, string? details)
        {
            string localPlayerName = localPlayerNameProvider?.Invoke() ?? string.Empty;

            if (string.IsNullOrEmpty(details))
                return $"[MM][{level}][{localPlayerName}] {eventName}";

            return $"[MM][{level}][{localPlayerName}] {eventName} :: {details}";
        }
    }
}

