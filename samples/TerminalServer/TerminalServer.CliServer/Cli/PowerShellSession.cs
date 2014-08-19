using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.CliServer.CLI;
using TerminalServer.CliServer.Infrastructure;

namespace TerminalServer.CliServer
{
    public class PowerShellFactory : ICliSessionFactory
    {
        readonly ILogger _log;
        public String Type { get { return "powershell"; } }
        public PowerShellFactory(ILogger log)
        {
            _log = log;
        }
        public ICliSession Create()
        {
            return new PowerShellSession(_log);
        }
    }

    public class PowerShellSession:ICliSession
    {
        readonly PowerShell _proc;
        readonly ILogger _log;
        Action<String, Int32, Boolean> _output;
        public String Type { get { return "powershell"; } }
        public String CurrentPath { get; private set; }
        public Action<String, Int32, Boolean> Output
        {
            get { return _output; }
            set
            {
                _output = value;
                _output("Welcome to Powershell (System.Management.Automation.dll)", 0, true);
            }
        }
        public PowerShellSession(ILogger log)
        {
            _proc = PowerShell.Create();
            _log = log;
            _proc.Commands.Clear();
            _proc.AddCommand("Get-Location");
            _proc.AddCommand("Out-String");
            CurrentPath = _proc.Invoke()
                        .First()
                        .ToString()
                        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[2].Trim();
        }
        public void Input(String value, Int32 commandCorrelationId)
        {
            List<String> lines = new List<String>();
            try
            {
                _proc.Commands.Clear();
                _proc.AddCommand(value);
                _proc.AddCommand("Out-String");
                
                foreach (PSObject result in _proc.Invoke())
                    lines.Add(result.ToString());
            }
            catch (Exception ex)
            {
                lines.Add(ex.Message);
            }
            _proc.Commands.Clear();
            _proc.AddCommand("Get-Location");
            _proc.AddCommand("Out-String");
            CurrentPath = _proc.Invoke()
                        .First()
                        .ToString()
                        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[2].Trim();

            foreach (var line in lines)
            {
                if (Output != null)
                    Output(line, commandCorrelationId, line == lines.Last());
            }
        }
        public void Dispose()
        {
            _proc.Dispose();
        }
    }
 }
