//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Management.Automation;
//using System.Text;
//using System.Threading.Tasks;
//using TerminalServer.Server.Infrastructure;

//namespace TerminalServer.Server.CLI
//{
//    public class PowerShellSessionFactory : ICliSessionFactory
//    {
//        public string Type
//        {
//            get { return "powershell"; }
//        }

//        public ICliSession Create(String id)
//        {
//            return new PowerShellSession(id);
//        }
//    }

//    public class PowerShellSession : ICliSession
//    {
//        PowerShell _proc;
//        public String Path { get; private set; }
//        public string Id { get; private set; }
//        public PowerShellSession(String id)
//        {
//            Id = id;
//            _proc = PowerShell.Create();
//        }

//        public void Execute(String line)
//        {
//            _proc.Commands.Clear();
//            _proc.AddCommand(line);
//            _proc.AddCommand("Out-String");
//            var list = new List<String>();
//            foreach (PSObject result in _proc.Invoke())
//            {
//                line = result.ToString();
//                list.Add(line);
//            }
//            _proc.Commands.Clear();
//            _proc.AddCommand("Get-Location");
//            _proc.AddCommand("Out-String");
//            Path = _proc.Invoke()
//                        .First()
//                        .ToString()
//                        .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[2].Trim();

//            return Task.FromResult(list.ToArray());
//        }
//    }
//}
