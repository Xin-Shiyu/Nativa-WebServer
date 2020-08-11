using System;
using System.Collections.Generic;
using System.Text;

namespace WebServer
{
    struct ServerSettings
    {
        public int port;
        public int compressMinSize;
        public int keepAliveMaxDelay;
        public bool aggressiveChunking;
    }
}
