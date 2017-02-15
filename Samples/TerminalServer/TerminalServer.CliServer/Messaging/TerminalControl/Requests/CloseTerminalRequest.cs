﻿using System;

namespace TerminalServer.CliServer
{
    [Serializable]
    public class CloseTerminalRequest : ITerminalRequest
    {
        public Guid TerminalId { get; set; }
        public Guid ConnectionId { get; set; }
    }
}
