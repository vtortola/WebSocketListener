using System;

namespace vtortola.WebSockets.Tools
{
    internal sealed class NullLogger : ILogger
    {
        public static NullLogger Instance = new NullLogger();

        /// <inheritdoc />
        public bool IsDebugEnabled => false;
        /// <inheritdoc />
        public bool IsWarningEnabled => false;
        /// <inheritdoc />
        public bool IsErrorEnabled => false;
        /// <inheritdoc />
        public void LogDebug(string message, Exception error = null)
        {

        }
        /// <inheritdoc />
        public void LogWarning(string message, Exception error = null)
        {

        }
        /// <inheritdoc />
        public void LogError(string message, Exception error = null)
        {

        }
    }
}
