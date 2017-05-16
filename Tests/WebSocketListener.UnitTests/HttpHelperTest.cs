using System.Net;
using vtortola.WebSockets.Tools;
using Xunit;

namespace WebSocketListener.UnitTests
{
    public class HttpHelperTest
    {
        [Theory,
        InlineData("HTTP/1.1 101 Web Socket Protocol Handshake", HttpStatusCode.SwitchingProtocols, "Web Socket Protocol Handshake"),
        InlineData("HTTP/1.0 200 OK", HttpStatusCode.OK, "OK"),
        InlineData("HTTP/1.1 200 OK", HttpStatusCode.OK, "OK"),
        InlineData("HTTP/1.1 404 Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1 404 Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData("  HTTP/1.1 404 Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1  404 Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1   404 Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1   404  Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1   404   Not Found", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1   404   Not Found ", HttpStatusCode.NotFound, "Not Found"),
        InlineData(" HTTP/1.1   404   Not Found  ", HttpStatusCode.NotFound, "Not Found"),
        InlineData("HTTP/1.1 200", HttpStatusCode.OK, ""),
        InlineData("HTTP/1.1 ", (HttpStatusCode)0, "Missing Response Code"),
        InlineData("HTTP/1.1 WRONG", (HttpStatusCode)0, "Missing Response Code"),
        InlineData("HTTP/1.1", (HttpStatusCode)0, "Missing Response Code"),
        InlineData("HTTP/1", (HttpStatusCode)0, "Malformed Response"),
        InlineData("", (HttpStatusCode)0, "Malformed Response"),
        InlineData("200 OK", (HttpStatusCode)0, "Malformed Response")]
        public void TryParseAndAddRequestHeaderTest(string headline, HttpStatusCode statusCode, string description)
        {
            var actualStatusCode = default(HttpStatusCode);
            var actualDescription = default(string);
            HttpHelper.TryParseHttpResponse(headline, out actualStatusCode, out actualDescription);

            Assert.Equal(statusCode, actualStatusCode);
            Assert.Equal(description, actualDescription);
        }
    }
}
