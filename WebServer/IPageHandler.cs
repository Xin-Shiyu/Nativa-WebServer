namespace WebServer
{
    internal interface IPageHandler
    {
        public void WritePage(string URI, in HttpHelper.ResponseStream stream);
        public void WritePage(string URI, int begin, int end, in HttpHelper.ResponseStream stream);
        public void WriteHead(string URI, HttpHelper.ResponseStream stream);
    }
}
