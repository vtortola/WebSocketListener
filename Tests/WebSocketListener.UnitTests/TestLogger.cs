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
        public void LogDebug(string message, Exception error = null)
        {
            if (!string.IsNullOrEmpty(message))
                this.output.WriteLine(message);
            if (error != null)
                this.output.WriteLine(error.ToString());
        }
        /// <inheritdoc />
        public void LogWarning(string message, Exception error = null)
        {
            if (!string.IsNullOrEmpty(message))
                this.output.WriteLine("[WARN] " + message);
            if (error != null)
                this.output.WriteLine(error.ToString());
        }
        /// <inheritdoc />
        public void LogError(string message, Exception error = null)
        {
            if (!string.IsNullOrEmpty("[ERROR] " + message))
                this.output.WriteLine(message);
            if (error != null)
                this.output.WriteLine(error.ToString());
        }
    }
}
