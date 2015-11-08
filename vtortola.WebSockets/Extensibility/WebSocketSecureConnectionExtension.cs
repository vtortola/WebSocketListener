using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace vtortola.WebSockets
{
    public sealed class WebSocketSecureConnectionExtension : IWebSocketConnectionExtension
    {
        readonly X509Certificate2 _certificate;
        readonly RemoteCertificateValidationCallback _validation;

        public WebSocketSecureConnectionExtension(X509Certificate2 certificate)
        {
            _certificate = certificate;
        }

        public WebSocketSecureConnectionExtension(X509Certificate2 certificate, RemoteCertificateValidationCallback validation)
        {
            _certificate = certificate;
            _validation = validation;
        }

        public Stream ExtendConnection(Stream stream)
        {
            var ssl = new SslStream(stream, false, _validation);
            ssl.AuthenticateAsServer(_certificate, _validation != null, SslProtocols.Default, false);
            return ssl;
        }

        public async Task<Stream> ExtendConnectionAsync(Stream stream)
        {
            var ssl = new SslStream(stream, false, _validation);
            await ssl.AuthenticateAsServerAsync(_certificate, _validation != null, SslProtocols.Default, false).ConfigureAwait(false);
            return ssl;
        }
    }
}
