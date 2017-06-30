/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
using System;

namespace vtortola.WebSockets
{
    public sealed class NullLogger : ILogger
    {
        public static NullLogger Instance = new NullLogger();

        /// <inheritdoc />
        public bool IsDebugEnabled => false;
        /// <inheritdoc />
        public bool IsWarningEnabled => false;
        /// <inheritdoc />
        public bool IsErrorEnabled => false;
        /// <inheritdoc />
        public void Debug(string message, Exception error = null)
        {

        }
        /// <inheritdoc />
        public void Warning(string message, Exception error = null)
        {

        }
        /// <inheritdoc />
        public void Error(string message, Exception error = null)
        {

        }
    }
}
