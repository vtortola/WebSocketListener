using System;
using System.Globalization;
using System.Net;

namespace vtortola.WebSockets.Tools
{
    public static class HttpHelper
    {
        public static readonly string WebSocketHttp10Version = "HTTP/1.0";
        public static readonly string WebSocketHttp11Version = "HTTP/1.1";

        public static bool TryParseHttpResponse(string headline, out HttpStatusCode statusCode, out string statusCodeDescription)
        {
            if (headline == null) throw new ArgumentNullException(nameof(headline));

            var headlineStartIndex = 0;
            var headlineLength = headline.Length;
            HeadersHelper.TrimInPlace(headline, ref headlineStartIndex, ref headlineLength);

            if (string.CompareOrdinal(headline, headlineStartIndex, WebSocketHttp11Version, 0, WebSocketHttp11Version.Length) != 0 &&
                string.CompareOrdinal(headline, headlineStartIndex, WebSocketHttp10Version, 0, WebSocketHttp11Version.Length) != 0)
            {
                statusCode = 0;
                statusCodeDescription = "Malformed Response";
                return false;
            }

            var responseCodeStartIndex = Math.Min(headlineStartIndex + WebSocketHttp11Version.Length + 1, headline.Length);
            HeadersHelper.Skip(headline, ref responseCodeStartIndex, UnicodeCategory.SpaceSeparator);
            var responseCodeEndIndex = responseCodeStartIndex;
            HeadersHelper.Skip(headline, ref responseCodeEndIndex, UnicodeCategory.DecimalDigitNumber);

            if (responseCodeEndIndex == responseCodeStartIndex)
            {
                statusCode = 0;
                statusCodeDescription = "Missing Response Code";
                return false;
            }
            var responseStatus = ushort.Parse(headline.Substring(responseCodeStartIndex, responseCodeEndIndex - responseCodeStartIndex));

            var descriptionStartIndex = responseCodeEndIndex;
            var descriptionLength = headline.Length - descriptionStartIndex;

            HeadersHelper.TrimInPlace(headline, ref descriptionStartIndex, ref descriptionLength);

            statusCode = (HttpStatusCode)responseStatus;
            statusCodeDescription = descriptionLength > 0 ? headline.Substring(descriptionStartIndex, descriptionLength) : string.Empty;
            return true;
        }
    }
}
