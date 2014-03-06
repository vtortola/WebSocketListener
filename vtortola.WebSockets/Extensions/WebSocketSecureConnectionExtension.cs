using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    [Obsolete("It does not work",false)]
    public sealed class WebSocketSecureConnectionExtension : IWebSocketConnectionExtension
    {
        readonly X509Certificate2 _certificate;

        public WebSocketSecureConnectionExtension(X509Certificate2 certificate)
        {
            _certificate = certificate;
        }

        public Stream ExtendConnection(Stream stream)
        {
            var ssl = new SslStream(stream, false);
            ssl.AuthenticateAsServer(_certificate, false, SslProtocols.Default, true);
            return ssl;
        }

        public int Order
        {
            get { return 0; }
        }
    }
}
