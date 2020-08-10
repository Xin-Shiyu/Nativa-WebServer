namespace WebServer
{
    internal interface IPageHandler
    {
        public HttpHelper.Response GetPage(string URI, bool onlyHead = false);
    }
}
