using System;
using System.Collections.Generic;
using System.Net;
using System.Web;

namespace vtortola.WebSockets
{
    public class CookieParser
    {
        public IEnumerable<Cookie> Parse(String cookieString)
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
                else if (c == ';' && !String.IsNullOrWhiteSpace(name))
                {
                    yield return CreateCookie(name, part);
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
        private Cookie CreateCookie(String key, String value)
        {
            return new Cookie(key.Trim(), HttpUtility.UrlDecode(value.Trim()));
        }
    }
}
