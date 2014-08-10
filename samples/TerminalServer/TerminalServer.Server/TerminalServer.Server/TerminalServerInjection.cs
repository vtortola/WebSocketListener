using Ninject;
using Ninject.Activation;
using Ninject.Modules;
using Ninject.Parameters;
using Ninject.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerminalServer.Server.CLI;
using TerminalServer.Server.CLI.Control;
using TerminalServer.Server.Infrastructure;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Messaging.Serialization;
using Ninject.Extensions.NamedScope;
using log4net;

namespace TerminalServer.Server
{
    public class TerminalServerInjection:StandardKernel
    {
        public TerminalServerInjection(SessionManager sessionManager)
            : base(new Module(sessionManager))
        {
            this.Settings.ActivationCacheDisabled = true;
        }

        class Module:NinjectModule
        {
            SessionManager _sessionManager;
            public Module(SessionManager sessionManager)
            {
                _sessionManager = sessionManager;
            }
            public override void Load()
            {
                this.Bind<ILogger>().To<Log4NetLogger>().InSingletonScope();

                this.Bind<SessionManager>().ToConstant(_sessionManager);
                this.Bind<ISystemInfo>().To<SystemInfo>().InSingletonScope();

                this.Bind<CliControl>().ToSelf().InCallScope();
                this.Bind<CliSessionAbstractFactory>().ToSelf().InSingletonScope();

                this.Bind<IMessageBus>().To<WebSocketMessageBus>().InCallScope();
                this.Bind<IEventSerializator>().To<DefaultEventSerializator>().InCallScope();

                this.Bind<ICliSessionFactory>().To<ConsoleSessionFactory>().InCallScope();
                //this.Bind<ICliSessionFactory>().To<PowerShellSessionFactory>().InScope(Scope);

                this.Bind<IObserver<RequestBase>>().To<CreateTerminalRequestHandler>().InCallScope();
                this.Bind<IObserver<RequestBase>>().To<InputTerminalRequestHandler>().InCallScope();
                this.Bind<IObserver<RequestBase>>().To<CloseTerminalRequestHandler>().InCallScope();
            }
        }

    }
}
