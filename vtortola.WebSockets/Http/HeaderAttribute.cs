using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vtortola.WebSockets.Http
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class HeaderAttribute : Attribute
    {
        public string Name { get; private set; }
        public bool IsAtomic { get; set; }

        public HeaderAttribute()
        {
            
        }
        public HeaderAttribute(string headerName)
        {
            if (headerName == null) throw new ArgumentNullException(nameof(headerName));

            this.Name = headerName;
        }
    }
}
