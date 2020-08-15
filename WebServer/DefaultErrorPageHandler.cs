using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace WebServer
{
    //恶心地带
    internal class DefaultErrorPageHandler : IErrorPageHandler
    {
        //这是我有史以来写过最恶心的实现，没有之一，我都想不出来名字了
        private static byte[] pageTemplatePart1 = Encoding.ASCII.GetBytes("<html><head><title>");
        private static byte[] pageTemplatePart2 = Encoding.ASCII.GetBytes("</title></head><body><center><h1>");
        private static byte[] pageTemplatePart3 = Encoding.ASCII.GetBytes("</h1><hr/><p>Nativa WebServer</p><pre>");
        private static byte[] pageTemplatePart4 = Encoding.ASCII.GetBytes("</pre></center></body></html>");

        void IErrorPageHandler.WritePage(WebException exception, HttpHelper.ResponseStream stream)
        {
            stream.WriteStatus(exception.ErrorCode);
            stream.WriteHeader(HeaderStrings.ContentType, "text/html");
            stream.WriteBody(provider);
            IEnumerable<Memory<byte>> provider() //连我自己都没想到还能这么用 :(
            {
                yield return pageTemplatePart1; //对！我都准备好了！可我就是要这样 yield 过去因为我只提供了这个 API，我好贱
                yield return HttpHelper.ResponseStream.StatusCodeString[exception.ErrorCode];
                yield return pageTemplatePart2;
                yield return HttpHelper.ResponseStream.StatusCodeString[exception.ErrorCode];
                yield return pageTemplatePart3;
                if (exception.InnerException != null) yield return Encoding.ASCII.GetBytes(exception.InnerException.Message);
                yield return pageTemplatePart4;
                yield break;
            }
            stream.FinishSession();
        }
    }
}
