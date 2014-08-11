using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.Server.Infrastructure;

namespace TerminalServer.Server.CLI
{
    public class ConsoleSessionFactory : ICliSessionFactory
    {
        public static readonly String TypeName = "cmd.exe";

        readonly ILogger _log;
        public string Type
        {
            get { return ConsoleSessionFactory.TypeName; }
        }

        public ConsoleSessionFactory(ILogger log)
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

        readonly Process _proc;
        readonly SubscriptionManager<String> _subscriptors;
        readonly CancellationTokenSource _cancel;
        readonly ILogger _log;
        String _lastCommand = null;
        Boolean _nextIsPath = false;

        public String Type { get { return ConsoleSessionFactory.TypeName; } }
        public String CurrentPath { get; private set; }
        public ConsoleSession(ILogger log)
        {
            _log = log;
            _cancel = new CancellationTokenSource();
            _subscriptors = new SubscriptionManager<String>(this);

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
            else if (_lastCommand != null && line.EndsWith(_lastCommand))
                return;
            else
                _subscriptors.OnNext(line);
        }

        private async Task ReadAsync()
        {
            while (!_cancel.IsCancellationRequested && !_proc.HasExited)
            {
                try
                {
                    var rline = await _proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (rline != null)
                        Emit(rline);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _cancel.Cancel();
                    _log.Error("cmd.exe session error", ex);
                    _subscriptors.OnError(ex);
                }
            }
        }
        private async Task ReadErrorAsync()
        {
            while (!_cancel.IsCancellationRequested && !_proc.HasExited)
            {
                try
                {
                    var rline = await _proc.StandardError.ReadLineAsync().ConfigureAwait(false);
                    if (rline != null)
                        Emit(rline);
                }
                catch (TaskCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _cancel.Cancel();
                    _log.Error("cmd.exe session error", ex);
                    _subscriptors.OnError(ex);
                }
            }
        }
        public void OnCompleted()
        {
            _cancel.Cancel();
            _proc.Dispose();
            //_subscriptors.OnCompleted();
            _log.Debug(this.GetType().Name + " OnCompleted");
        }
        public void OnError(Exception error)
        {
            _cancel.Cancel();
            _proc.Dispose();
            _subscriptors.OnError(error);
            _log.Debug(this.GetType().Name + " OnError");
        }
        public void OnNext(String value)
        {
            if (value.ToLowerInvariant() == "exit")
                OnCompleted();
            else
            {
                _lastCommand = value + " & echo " + _preCDID + "& cd";
                _proc.StandardInput.WriteLine(_lastCommand);
            }
        }

        public IDisposable Subscribe(IObserver<String> observer)
        {
            return _subscriptors.Subscribe(observer);
        }

       ~ConsoleSession()
       {
           _log.Debug(this.GetType().Name + " destroy");
       }
    }
}
