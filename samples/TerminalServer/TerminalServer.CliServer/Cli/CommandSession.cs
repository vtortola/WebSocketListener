using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.CliServer.Infrastructure;
using TerminalServer.CliServer.Messaging;
using MassTransit;
using System.Collections.Generic;
using System.Text;

namespace TerminalServer.CliServer.CLI
{
    public class CommandSessionFactory : ICliSessionFactory
    {
        public static readonly String TypeName = "cmd.exe";

        readonly ILogger _log;
        public string Type
        {
            get { return CommandSessionFactory.TypeName; }
        }

        public CommandSessionFactory(ILogger log)
        {
            _log = log;
        }

        public ICliSession Create()
        {
            return new ConsoleSession(_log);
        }
    }

    public class ConsoleSession : ICliSession
    {
        static readonly String _preCDID = "xx_vtortola_xx";
        static readonly String _postCDID = "yy_vtortola_yy";

        readonly Process _proc;
        readonly CancellationTokenSource _cancel;
        readonly ILogger _log;
        readonly List<String> _errorBuffer;
        String _lastCommand = null;
        Boolean _nextIsPath = false;

        public String Type { get { return CommandSessionFactory.TypeName; } }
        public String CurrentPath { get; private set; }
        public Action<String, Int32, Boolean> Output { get; set; }
        public ConsoleSession(ILogger log)
        {
            _log = log;
            _cancel = new CancellationTokenSource();
            _errorBuffer = new List<String>();
            _proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = CurrentPath = "C:\\"
                }
            };

            _proc.Start();
            Task.Run((Func<Task>)ReadAsync);
            Task.Run((Func<Task>)ReadErrorAsync);
        }
        private void Emit(String line)
        {
            if (_nextIsPath)
            {
                CurrentPath = line;
                _nextIsPath = false;
            }
            else if (line == _preCDID)
                _nextIsPath = true;
            else if (line == _postCDID)
            {
                Output(String.Empty, _commandCorrelationId, _errorBuffer.Count == 0);
                _lastCommand = null;
                if (_errorBuffer.Count != 0)
                {
                    for (int i = 0; i < _errorBuffer.Count; i++)
                        Output(_errorBuffer[i], _commandCorrelationId, i == _errorBuffer.Count-1);
                    _errorBuffer.Clear();
                }
            }
            else if (_lastCommand != null && line.EndsWith(_lastCommand))
            {

            }
            else if (Output != null && !String.IsNullOrWhiteSpace(line))
                Output(line, _commandCorrelationId, _lastCommand == null);
        }

        private async Task ReadAsync()
        {
            while (!_cancel.IsCancellationRequested && !_proc.HasExited)
            {
                try
                {
                    var line = await _proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (line != null)
                        Emit(line);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _log.Error("cmd.exe session error", ex);
                    Finish(ex);
                }
            }
        }
        private async Task ReadErrorAsync()
        {
            while (!_cancel.IsCancellationRequested && !_proc.HasExited)
            {
                try
                {
                    var line = await _proc.StandardError.ReadLineAsync().ConfigureAwait(false);
                    _errorBuffer.Add(line);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _log.Error("cmd.exe session error", ex);
                    Finish(ex);
                }
            }
        }

        Int32 _commandCorrelationId;
        public void Input(String value, Int32 commandCorrelationId)
        {
            if (value.ToLowerInvariant() == "exit")
                Finish(null);
            else if (_lastCommand != null)
            {
                _proc.StandardInput.WriteLine(value);
            }
            else
            {
                _commandCorrelationId = commandCorrelationId;
                _lastCommand = value + " & echo " + _preCDID + "& cd & echo " + _postCDID;
                _proc.StandardInput.WriteLine(_lastCommand);
            }
        }
        private void Finish(Exception error)
        {
            _log.Debug(this.GetType().Name + " Finish");
            _cancel.Cancel();
            _proc.Dispose();
        }
        public void Dispose()
        {
            Finish(null);
        }
       ~ConsoleSession()
       {
           _log.Debug(this.GetType().Name + " destroy");
       }


       
    }
}
