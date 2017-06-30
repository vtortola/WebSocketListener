﻿using System;

namespace vtortola.WebSockets.Http
{
    [AttributeUsage(AttributeTargets.Field)]
    internal class HeaderAttribute : Attribute
    {
        public string Name { get; }
        public HeaderFlags Flags { get; set; }

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
