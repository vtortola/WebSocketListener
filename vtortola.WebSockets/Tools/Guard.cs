using System;
using JetBrains.Annotations;

namespace vtortola.WebSockets
{
    public static class Guard
    {
        /// <summary>
        /// TODO : replace this by something like https://github.com/StefH/System.Linq.Dynamic.Core/tree/master/src/System.Linq.Dynamic.Core/Validation
        /// </summary>
        public static void ParameterCannotBeNull<T>([NoEnumeration] [CanBeNull] T obj, [InvokerParameterName] [NotNull] string parameterName)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }
    }
}