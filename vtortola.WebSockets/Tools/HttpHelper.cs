using System;
using System.Globalization;
using System.Net;

namespace vtortola.WebSockets.Tools
{
    public static class HttpHelper
    {
        public static readonly string WebSocketHttp10Version = "HTTP/1.0";
        public static readonly string WebSocketHttp11Version = "HTTP/1.1";

        public static bool TryParseHttpResponse(string responseLine, out HttpStatusCode statusCode, out string statusCodeDescription)
        {
            if (responseLine == null) throw new ArgumentNullException(nameof(responseLine));

            var responseLineStartIndex = 0;
            var responseLineLength = responseLine.Length;
            HeadersHelper.TrimInPlace(responseLine, ref responseLineStartIndex, ref responseLineLength);

            if (string.CompareOrdinal(responseLine, responseLineStartIndex, WebSocketHttp11Version, 0, WebSocketHttp11Version.Length) != 0 &&
                string.CompareOrdinal(responseLine, responseLineStartIndex, WebSocketHttp10Version, 0, WebSocketHttp11Version.Length) != 0)
            {
                statusCode = 0;
                statusCodeDescription = "Malformed Response";
                return false;
            }

            var responseCodeStartIndex = Math.Min(responseLineStartIndex + WebSocketHttp11Version.Length + 1, responseLine.Length);
            HeadersHelper.Skip(responseLine, ref responseCodeStartIndex, UnicodeCategory.SpaceSeparator);
            var responseCodeEndIndex = responseCodeStartIndex;
            HeadersHelper.Skip(responseLine, ref responseCodeEndIndex, UnicodeCategory.DecimalDigitNumber);

            if (responseCodeEndIndex == responseCodeStartIndex)
            {
                statusCode = 0;
                statusCodeDescription = "Missing Response Code";
                return false;
            }
            var responseStatus = ushort.Parse(responseLine.Substring(responseCodeStartIndex, responseCodeEndIndex - responseCodeStartIndex));

            var descriptionStartIndex = responseCodeEndIndex;
            var descriptionLength = responseLine.Length - descriptionStartIndex;

            HeadersHelper.TrimInPlace(responseLine, ref descriptionStartIndex, ref descriptionLength);

            statusCode = (HttpStatusCode)responseStatus;
            statusCodeDescription = descriptionLength > 0 ? responseLine.Substring(descriptionStartIndex, descriptionLength) : string.Empty;
            return true;
        }
    }
}
