using System;
using vtortola.WebSockets;
using Xunit.Abstractions;

namespace WebSocketListener.UnitTests
{
    public class TestLogger : ILogger
    {
        private readonly ITestOutputHelper output;
        /// <inheritdoc />
        public bool IsDebugEnabled { get; set; }
        /// <inheritdoc />
        public bool IsWarningEnabled { get; set; }
        /// <inheritdoc />
        public bool IsErrorEnabled { get; set; }

        public TestLogger(TestLogger other)
            : this(other.output)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));
        }
        public TestLogger(ITestOutputHelper output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            this.IsDebugEnabled = true;
            this.IsWarningEnabled = true;
            this.IsErrorEnabled = true;

            this.output = output;
        }

        /// <inheritdoc />
        public void Debug(string message, Exception error = null)
        {
            if (!this.IsDebugEnabled)
                return;

            if (!string.IsNullOrEmpty(message))
                this.WriteLine(message);

            if (error != null)
                this.WriteLine(error.ToString());
        }
        /// <inheritdoc />
        public void Warning(string message, Exception error = null)
        {
            if (!this.IsWarningEnabled)
                return;

            if (!string.IsNullOrEmpty(message))
                this.WriteLine("[WARN] " + message);
            if (error != null)
                this.WriteLine(error.ToString());
        }
        /// <inheritdoc />
        public void Error(string message, Exception error = null)
        {
            if (!this.IsErrorEnabled)
                return;

            if (!string.IsNullOrEmpty("[ERROR] " + message))
                this.WriteLine(message);
            if (error != null)
                this.WriteLine(error.ToString());
        }
        private void WriteLine(string message)
        {
            this.output.WriteLine(message);
            System.Diagnostics.Debug.WriteLine(message);
        }
    }
}
