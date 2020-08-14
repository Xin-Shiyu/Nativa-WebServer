namespace WebServer
{
    internal interface IPageHandler
    {
        public HttpHelper.Response GetPage(string URI);
        public HttpHelper.Response GetPage(string URI, int begin, int end);
        public HttpHelper.Response GetHead(string URI);
    }
}
