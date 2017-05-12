using System;

namespace vtortola.WebSockets
{
    public interface ILogger
    {
        bool IsDebugEnabled { get; }
        bool IsWarningEnabled { get; }
        bool IsErrorEnabled { get; }

        void LogDebug(string message, Exception error = null);
        void LogWarning(string message, Exception error = null);
        void LogError(string message, Exception error = null);
    }
}
