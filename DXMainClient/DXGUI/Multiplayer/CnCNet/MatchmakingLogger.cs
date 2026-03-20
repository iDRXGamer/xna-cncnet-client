using Rampastring.Tools;
using System;

namespace DTAClient.DXGUI.Multiplayer.CnCNet
{
    internal sealed class MatchmakingLogger
    {
        private readonly Func<string> localPlayerNameProvider;

        public MatchmakingLogger(Func<string> localPlayerNameProvider)
        {
            this.localPlayerNameProvider = localPlayerNameProvider;
        }

        public void Info(string eventName, string details = null) =>
            Logger.Log(Format("INFO", eventName, details));

        public void Warn(string eventName, string details = null) =>
            Logger.Log(Format("WARN", eventName, details));

        public void Error(string eventName, Exception ex, string details = null) =>
            Logger.Log(Format("ERROR", eventName, $"{details} :: {ex}"));

        private string Format(string level, string eventName, string details)
        {
            string localPlayerName = localPlayerNameProvider?.Invoke() ?? string.Empty;

            if (string.IsNullOrEmpty(details))
                return $"[MM][{level}][{localPlayerName}] {eventName}";

            return $"[MM][{level}][{localPlayerName}] {eventName} :: {details}";
        }
    }
}
