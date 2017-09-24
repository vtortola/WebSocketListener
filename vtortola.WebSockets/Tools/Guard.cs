using System;

namespace vtortola.WebSockets
{
    internal static class Guard
    {
        public static void ParameterCannotBeNull<T>(T obj, string paramenterName)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(paramenterName);
            }
        }
    }
}
