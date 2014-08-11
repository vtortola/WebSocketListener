using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.TerminalControl.Events;

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
        readonly Process _proc;
        readonly SubscriptionManager<String> _subscriptors;
        readonly CancellationTokenSource _cancel;
        readonly ILogger _log;

        public String Type { get { return ConsoleSessionFactory.TypeName; } }
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
                    WorkingDirectory = "C:\\"
                }
            };

            _proc.Start();
            Task.Run((Func<Task>)ReadAsync);
            Task.Run((Func<Task>)ReadErrorAsync);
        }

        private async Task ReadAsync()
        {
            while (!_cancel.IsCancellationRequested && !_proc.HasExited)
            {
                try
                {
                    var rline = await _proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (rline != null)
                        _subscriptors.OnNext(rline);
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
                        _subscriptors.OnNext(rline);
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
            _log.Debug(this.GetType().Name + " OnCompleted");
        }
        public void OnError(Exception error)
        {
            _cancel.Cancel();
            _proc.Dispose();
            _log.Debug(this.GetType().Name + " OnError");
        }
        public void OnNext(String value)
        {
            _proc.StandardInput.WriteLine(value);
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
