/*
	Copyright (c) 2017 Denis Zykov
	License: https://opensource.org/licenses/MIT
*/
#if !NAMED_PIPES_DISABLE
using System;
using System.Net;
using System.Text;

namespace vtortola.WebSockets.Transports.NamedPipes
{
    public class NamedPipeEndPoint : EndPoint
    {
        public string PipeName { get; }

        public NamedPipeEndPoint(string pipeName)
        {
            this.PipeName = pipeName;
        }

        /// <inheritdoc />
        public override EndPoint Create(SocketAddress socketAddress)
        {
            if (socketAddress == null) throw new ArgumentNullException(nameof(socketAddress));

            var pipeNameBytes = new byte[socketAddress.Size];
            for (var i = 0; i < pipeNameBytes.Length; i++)
                pipeNameBytes[i] = socketAddress[i];

            return new NamedPipeEndPoint(Encoding.UTF8.GetString(pipeNameBytes));
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.PipeName;
        }
    }
}
#endif