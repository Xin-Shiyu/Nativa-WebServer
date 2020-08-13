using System;
using System.Collections.Generic;

namespace WebServer
{
    internal interface IErrorPageHandler
    {
        public HttpHelper.Response GetPage(WebException exception);
    }

    internal class WebException : Exception
    {
        public int ErrorCode { get; }
        public WebException(int errorCode, Exception inner = null)
            : base(HttpHelper.Response.StatusCodeString[errorCode], inner)
        {
            ErrorCode = errorCode;
        }
        private static readonly Dictionary<int, WebException> storehouse = new Dictionary<int, WebException>();

        public static WebException GetException(int errorCode) //防止过多异常被 new 出来
        {
            if (!storehouse.TryGetValue(errorCode, out WebException exception))
            {
                exception = new WebException(errorCode);
                storehouse.Add(errorCode, exception);
            }
            return exception;
        }
    }
}
