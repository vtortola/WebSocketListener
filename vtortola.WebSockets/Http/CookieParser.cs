using System;
using System.Collections.Generic;
using System.Net;


namespace vtortola.WebSockets
{
    public static class CookieParser
    {
        public static IEnumerable<Cookie> Parse(string cookieString)
        {
            if (string.IsNullOrWhiteSpace(cookieString))
                yield break;

            string part = string.Empty, name = string.Empty;
            for (int i = 0; i < cookieString.Length; i++)
            {
                char c = cookieString[i];
                if (c == '=' && string.IsNullOrWhiteSpace(name))
                {
                    name = part;
                    part = string.Empty;
                    continue;
                }
                else if (c == ';')
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        yield return CreateCookie(name, part);
                    else
                        yield return CreateCookie(part, string.Empty);

                    name = string.Empty;
                    part = string.Empty;
                    continue;
                }
                part += c;
            }
            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(part))
            {
                yield return CreateCookie(name, part);
            }
        }
        private static Cookie CreateCookie(string key, string value)
        {
            return new Cookie(key.Trim(), WebUtility.UrlDecode(value.Trim()));
        }
    }
}
