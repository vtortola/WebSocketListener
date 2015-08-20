using System;

namespace Common.WebSockets.Services
{
    public static class HttpServicesLocator
    {
        private static IHttpServices _services;
        public static IHttpServices HttpServices
        {
            get
            {
                if (_services == null)
                {
                    throw new InvalidOperationException("You must provide an implementation for common System.Web functions.");
                }

				return _services;
            }
            set { _services = value; }
        }
    }
}