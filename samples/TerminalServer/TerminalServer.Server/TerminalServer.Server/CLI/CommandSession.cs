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
        readonly ILogger _log;
        public string Type
        {
            get { return "command"; }
        }

        public ConsoleSessionFactory(ILogger log)
        {
            _log = log;
        }

        public ICliSession Create(String id)
        {
            return new ConsoleSession(id,_log);
        }
    }

    public class ConsoleSession : ICliSession
    {
        readonly Process _proc;
        readonly List<IObserver<EventBase>> _subscriptors;
        readonly CancellationTokenSource _cancel;
        readonly ILogger _log;
        public string Id { get; private set; }
        public ConsoleSession(String id, ILogger log)
        {
            _log = log;
            _cancel = new CancellationTokenSource();
            _subscriptors = new List<IObserver<EventBase>>();

            Id = id;
            _proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true,
                     WorkingDirectory = "C:\\"
                }
            };

            _proc.Start();
            Task.Run((Func<Task>)ReadAsync);
        }

        private async Task ReadAsync()
        {
            while (!_cancel.IsCancellationRequested && !_proc.HasExited)
            {
                try
                {
                    var rline = await _proc.StandardOutput.ReadLineAsync().ConfigureAwait(false);
                    if (rline != null)
                        Propagate(new TerminalOutputEvent(Id,rline));
                }
                catch(Exception ex)
                {
                    _log.Error("Command session error", ex);
                }
            }
        }
        private void Propagate(EventBase output)
        {
            foreach (var subscriptor in _subscriptors)
            {
                subscriptor.OnNext(output);
            }
        }
        public void OnCompleted()
        {
        }
        public void OnError(Exception error)
        {
        }
        public void OnNext(String value)
        {
            _proc.StandardInput.WriteLine(value);
        }

        public IDisposable Subscribe(IObserver<EventBase> observer)
        {
            _subscriptors.Add(observer);
            return new Subscription(() => _subscriptors.Remove(observer));
        }
        public void Dispose()
        {
            _proc.Dispose();
            _cancel.Cancel();
            Console.WriteLine(this.GetType().Name + " dispose");
        }

       ~ConsoleSession()
       {
           Console.WriteLine(this.GetType().Name + " destroy");
       }
    }
}
