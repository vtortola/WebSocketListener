using System;

namespace TerminalServer.CliServer.Infrastructure
{
    public interface ILogger
    {
        Boolean IsDebugEnabled { get; }

        void Debug(String format, params Object[] args);
        String Error(String format, params Object[] args);
        String Error(String message, Exception exception);
        void Fatal(String format, params Object[] args);
        void Fatal(String message, Exception exception);
        void Info(String format, params Object[] args);
        void Info(String message);
        void Warn(String format, params Object[] args);
        String Warn(String message, Exception exception);
        String Warn(String message, String controller, String action, Exception error);
    }
}
