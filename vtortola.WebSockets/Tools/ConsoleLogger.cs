using System;
using System.Diagnostics;

namespace vtortola.WebSockets.Tools
{
    public sealed class ConsoleLogger : ILogger
    {
        public static ConsoleLogger Instance = new ConsoleLogger();

        /// <inheritdoc />
        public bool IsDebugEnabled { get; set; }
        /// <inheritdoc />
        public bool IsWarningEnabled { get; set; }
        /// <inheritdoc />
        public bool IsErrorEnabled { get; set; }

        public ConsoleLogger()
        {
            this.IsDebugEnabled = true;
            this.IsWarningEnabled = true;
            this.IsErrorEnabled = true;
        }

        /// <inheritdoc />
        public void LogDebug(string message, Exception error = null)
        {
            if (this.IsDebugEnabled == false)
                return;

            if (string.IsNullOrEmpty(message) == false)
                Console.WriteLine(message);

            if (error != null)
                Console.WriteLine(error);
        }
        /// <inheritdoc />
        public void LogWarning(string message, Exception error = null)
        {
            if (this.IsWarningEnabled == false)
                return;

            if (string.IsNullOrEmpty(message) == false)
                Debug.WriteLine("[WARN] " + message);

            if (error != null)
                Console.WriteLine(error);
        }
        /// <inheritdoc />
        public void LogError(string message, Exception error = null)
        {
            if (this.IsErrorEnabled == false)
                return;

            if (string.IsNullOrEmpty(message) == false)
                Debug.WriteLine("[ERROR] " + message);

            if (error != null)
                Console.WriteLine(error);
        }
    }
}
