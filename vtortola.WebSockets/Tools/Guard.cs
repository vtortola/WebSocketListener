using System;

namespace vtortola.WebSockets
{
    public static class Guard
    {
        public static void ParameterCannotBeNull<T>(T obj, String paramenterName)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(paramenterName);
            }
        }
    }
}
