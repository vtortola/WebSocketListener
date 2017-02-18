using System;
using System.Collections.Generic;
using System.Net;


namespace vtortola.WebSockets
{
    public static class CookieParser
    {
        public static IEnumerable<Cookie> Parse(String cookieString)
        {
            if (String.IsNullOrWhiteSpace(cookieString))
                yield break;

            String part = String.Empty, name = String.Empty;
            for (int i = 0; i < cookieString.Length; i++)
            {
                Char c = cookieString[i];
                if (c == '=' && String.IsNullOrWhiteSpace(name))
                {
                    name = part;
                    part = String.Empty;
                    continue;
                }
                else if (c == ';')
                {
                    if (!String.IsNullOrWhiteSpace(name))
                        yield return CreateCookie(name, part);
                    else
                        yield return CreateCookie(part, String.Empty);

                    name = String.Empty;
                    part = String.Empty;
                    continue;
                }
                part += c;
            }
            if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(part))
            {
                yield return CreateCookie(name, part);
            }
        }
        static Cookie CreateCookie(String key, String value)
        {
            return new Cookie(key.Trim(), WebUtility.UrlDecode(value.Trim()));
        }
    }
}
