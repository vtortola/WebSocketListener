using System;

namespace vtortola.WebSockets.Http
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class HeaderAttribute : Attribute
    {
        public string Name { get; }
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
