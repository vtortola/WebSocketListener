using System;
using System.Collections.Generic;
using System.Linq;

namespace vtortola.WebSockets
{
    internal static class HttpStatusCodeExtensions
    {
        static readonly Dictionary<HttpStatusCode, string> _descriptions =
            ((HttpStatusCode[])Enum.GetValues(typeof(HttpStatusCode)))
                .ToDictionary(x => x, x => GenerateDescription(x));

        static string GenerateDescription(HttpStatusCode code)
        {
            var list = new List<char>();
            var str = code.ToString();
            for (int i = 0; i < str.Length; i++)
            {
                list.Add(str[i]);
                if(i < str.Length -1 && char.IsUpper(str[i+1]))
                {
                    list.Add(' ');
                }
            }
            return new string(list.ToArray());
        }

        internal static string GetDescription(this HttpStatusCode status)
            => _descriptions[status];
    }
}
