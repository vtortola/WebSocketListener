using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketSecureConnectionExtension : IWebSocketConnectionExtension
    {
        private readonly X509Certificate2 _certificate;
        private readonly RemoteCertificateValidationCallback _validation;
        private readonly SslProtocols _protocols;

        public WebSocketSecureConnectionExtension(X509Certificate2 certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            _certificate = certificate;
            _protocols = SslProtocols.Tls12;
        }

        public WebSocketSecureConnectionExtension(X509Certificate2 certificate, RemoteCertificateValidationCallback validation)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            _certificate = certificate;
            _validation = validation;
            _protocols = SslProtocols.Tls12;
        }

        public WebSocketSecureConnectionExtension(X509Certificate2 certificate, RemoteCertificateValidationCallback validation, SslProtocols supportedSslProtocols)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));

            _certificate = certificate;
            _validation = validation;
            _protocols = supportedSslProtocols;
        }

        public Stream ExtendConnection(Stream stream)
        {
            var ssl = new SslStream(stream, false, _validation);
#if (UAP10_0  || NETSTANDARD || NETSTANDARDAPP)
            ssl.AuthenticateAsServerAsync(_certificate, _validation != null, _protocols, false).Wait();
#else
            ssl.AuthenticateAsServer(_certificate, _validation != null, _protocols, false);
#endif
            return ssl;
        }

        public async Task<Stream> ExtendConnectionAsync(Stream stream)
        {
            var ssl = new SslStream(stream, false, _validation);
            await ssl.AuthenticateAsServerAsync(_certificate, _validation != null, _protocols, false).ConfigureAwait(false);
            return ssl;
        }
    }
}
