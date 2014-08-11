using log4net;
using System;
using System.Diagnostics;

namespace TerminalServer.Server.Infrastructure
{
    public class Log4NetLogger:ILogger
    {
        private readonly ILog _logger = LogManager.GetLogger("Main");

        public bool IsDebugEnabled
        {
            get { return _logger.IsDebugEnabled; }
        }

        public void Debug(String format, params Object[] args)
        {
            try
            {
                if (_logger.IsDebugEnabled)
                {
                    _logger.DebugFormat(format, args);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
            }
        }

        public void Info(String message)
        {
            try
            {
                if (_logger.IsInfoEnabled)
                {
                    _logger.Info(message);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
            }
        }

        public void Info(String format, params Object[] args)
        {
            try
            {
                if (_logger.IsInfoEnabled)
                {
                    _logger.InfoFormat(format, args);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
            }
        }

        public void Warn(String format, params Object[] args)
        {
            try
            {
                if (_logger.IsWarnEnabled)
                {
                    _logger.WarnFormat(format, args);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
            }
        }

        public String Warn(String message, Exception exception)
        {
            try
            {
                if (_logger.IsWarnEnabled)
                {
                    String guid = Guid.NewGuid().ToString();
                    _logger.Warn(message, exception);
                    return guid;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
                return "[Cannot be logged]";
            }
        }

        public String Error(String format, params Object[] args)
        {
            try
            {
                if (_logger.IsErrorEnabled)
                {
                    String guid = Guid.NewGuid().ToString();
                    _logger.ErrorFormat("[ErrorTicket:" + guid + "]" + format, args);
                    return guid;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
                return "[Cannot be logged]";
            }
        }

        public String Error(String message, Exception exception)
        {
            try
            {

                if (_logger.IsErrorEnabled)
                {
                    String guid = Guid.NewGuid().ToString();
                    _logger.Error("[ErrorTicket:" + guid + "]" + message, exception);
                    return guid;
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
                return "[Cannot be logged]";
            }
        }

        public String Warn(String message, String controller, String action, Exception error)
        {
            return Warn(String.Format("[controller: {0}] [action: {1}] : {2}", controller, action, message), error);
        }

        public void Fatal(String format, params Object[] args)
        {
            try
            {
                if (_logger.IsFatalEnabled)
                {
                    _logger.FatalFormat(format, args);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
            }
        }

        public void Fatal(String message, Exception exception)
        {
            try
            {
                if (_logger.IsFatalEnabled)
                {
                    _logger.Fatal(message, exception);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("LOG ERROR: " + ex.Message);
            }
        }
    }
}
