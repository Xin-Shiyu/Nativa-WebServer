namespace WebServer
{
    internal struct ServerSettings
    {
        public int port;
        public int compressMinSize;
        public int keepAliveMaxDelay;
        public int keepAliveMaxRequestCount;
        public bool aggressiveChunking;
    }
}
