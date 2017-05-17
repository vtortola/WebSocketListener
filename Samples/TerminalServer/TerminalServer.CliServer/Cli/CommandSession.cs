using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TerminalServer.CliServer
{
    public class CommandSessionFactory : ICliSessionFactory
    {
        readonly ILogger _log;
        public String Type { get { return ConsoleSession.TypeName; } }
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
        public static readonly String TypeName = "cmd.exe";

        readonly Process _proc;
        readonly CancellationTokenSource _cancel;
        readonly ILogger _log;
        readonly List<String> _errorBuffer;
        String _lastCommand;
        Boolean _nextIsPath;
        Int32 _commandCorrelationId;

        public String Type { get { return ConsoleSession.TypeName; } }
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
        private void Push(String line)
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
                    for (var i = 0; i < _errorBuffer.Count; i++)
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
                        Push(line);
                }
                catch (TaskCanceledException){}
                catch (Exception ex)
                {
                    _log.Error("cmd.exe session error", ex);
                    _cancel.Cancel();
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
                catch (TaskCanceledException){}
                catch (Exception ex)
                {
                    _log.Error("cmd.exe session error", ex);
                    _cancel.Cancel();
                }
            }
        }
        public void Input(String value, Int32 commandCorrelationId)
        {
            if (value.ToLowerInvariant() == "exit")
            {
                _cancel.Cancel();
            }
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
        private void Dispose(Boolean disposing)
        {
            if (disposing)
                GC.SuppressFinalize(this);

            _proc.Dispose();
            _cancel.Cancel();
        }
        public void Dispose()
        {
            Dispose(true);
        }
       ~ConsoleSession()
       {
           Dispose(false);
       }
    }
}
