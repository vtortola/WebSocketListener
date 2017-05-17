using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vtortola.WebSockets;

// ReSharper disable once CheckNamespace
namespace vtortola.WebSockets
{
    public sealed class Log4NetLogger : ILogger
    {
        private log4net.ILog log;

        /// <inheritdoc />
        public bool IsDebugEnabled => this.log.IsDebugEnabled;
        /// <inheritdoc />
        public bool IsWarningEnabled => this.log.IsWarnEnabled;
        /// <inheritdoc />
        public bool IsErrorEnabled => this.log.IsErrorEnabled;
        /// <inheritdoc />

        public Log4NetLogger(Type loggerType = null)
        {
            this.log = log4net.LogManager.GetLogger(loggerType ?? typeof(WebSocketListener));
        }

        public void Debug(string message, Exception error = null)
        {
            this.log.Debug(message, error);
        }
        /// <inheritdoc />
        public void Warning(string message, Exception error = null)
        {
            this.log.Warn(message, error);
        }
        /// <inheritdoc />
        public void Error(string message, Exception error = null)
        {
            this.log.Error(message, error);
        }
    }
}
