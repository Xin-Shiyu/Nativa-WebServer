namespace WebServer
{
    internal class ServerSettings
    {
        public int port;
        public int compressMinSize;
        public int keepAliveMaxDelay;
        public int keepAliveMaxRequestCount;
        public bool aggressiveChunking;
    }
}
