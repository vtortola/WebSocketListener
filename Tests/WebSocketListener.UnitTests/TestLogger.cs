using System;
using vtortola.WebSockets;
using Xunit.Abstractions;

namespace WebSocketListener.UnitTests
{
    public class TestLogger : ILogger
    {
        private readonly ITestOutputHelper output;
        /// <inheritdoc />
        public bool IsDebugEnabled => true;
        /// <inheritdoc />
        public bool IsWarningEnabled => true;
        /// <inheritdoc />
        public bool IsErrorEnabled => true;

        public TestLogger(ITestOutputHelper output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));

            this.output = output;
        }

        /// <inheritdoc />
        public void Debug(string message, Exception error = null)
        {
            if (!string.IsNullOrEmpty(message))
                this.WriteLine(message);

            if (error != null)
                this.WriteLine(error.ToString());
        }
        /// <inheritdoc />
        public void Warning(string message, Exception error = null)
        {
            if (!string.IsNullOrEmpty(message))
                this.WriteLine("[WARN] " + message);
            if (error != null)
                this.WriteLine(error.ToString());
        }
        /// <inheritdoc />
        public void Error(string message, Exception error = null)
        {
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
