using Ninject;
using Ninject.Extensions.NamedScope;
using Ninject.Modules;
using System;
using TerminalServer.Server.CLI;
using TerminalServer.Server.Messaging;
using TerminalServer.Server.Session;

namespace TerminalServer.Server.Infrastructure
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

                this.Bind<SessionHub>().ToSelf().InCallScope();
                this.Bind<CliSessionAbstractFactory>().ToSelf().InSingletonScope();

                this.Bind(typeof(IMessageBusWrite),typeof(IMessageBusReceive),typeof(IMessageBus))
                    .To<WebSocketMessageBus>().InCallScope();

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
