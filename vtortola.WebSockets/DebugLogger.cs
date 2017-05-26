/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;

namespace vtortola.WebSockets
{
    public sealed class DebugLogger : ILogger
    {
        public static DebugLogger Instance = new DebugLogger();

        /// <inheritdoc />
        public bool IsDebugEnabled { get; set; }
        /// <inheritdoc />
        public bool IsWarningEnabled { get; set; }
        /// <inheritdoc />
        public bool IsErrorEnabled { get; set; }

        public DebugLogger()
        {
            this.IsDebugEnabled = true;
            this.IsWarningEnabled = true;
            this.IsErrorEnabled = true;
        }

        /// <inheritdoc />
        public void Debug(string message, Exception error = null)
        {
            if (this.IsDebugEnabled == false)
                return;

            if (string.IsNullOrEmpty(message) == false)
                System.Diagnostics.Debug.WriteLine(message);

            if (error != null)
            {
                System.Diagnostics.Debug.Indent();
                System.Diagnostics.Debug.WriteLine(error);
                System.Diagnostics.Debug.Unindent();
            }
        }
        /// <inheritdoc />
        public void Warning(string message, Exception error = null)
        {
            if (this.IsWarningEnabled == false)
                return;

            if (string.IsNullOrEmpty(message) == false)
                System.Diagnostics.Debug.WriteLine("[WARN] " + message);

            if (error != null)
            {
                System.Diagnostics.Debug.Indent();
                System.Diagnostics.Debug.WriteLine(error);
                System.Diagnostics.Debug.Unindent();
            }
        }
        /// <inheritdoc />
        public void Error(string message, Exception error = null)
        {
            if (this.IsErrorEnabled == false)
                return;

            if (string.IsNullOrEmpty(message) == false)
                System.Diagnostics.Debug.WriteLine("[ERROR] " + message);

            if (error != null)
            {
                System.Diagnostics.Debug.Indent();
                System.Diagnostics.Debug.WriteLine(error);
                System.Diagnostics.Debug.Unindent();
            }
        }
    }
}
