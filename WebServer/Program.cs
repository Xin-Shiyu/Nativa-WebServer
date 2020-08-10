//玩具性质，练手用的

namespace WebServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //这里应该加载配置文件，以后再实现
            Server server = new Server();
            server.Run();
        }
    }
}
