using System;
using System.Collections.Generic;
using System.Text;

namespace WebServer
{
    interface IErrorPageHandler
    {
        public HttpHelper.Response GetPage(WebException exception);
    }

    class WebException : Exception {
        public int ErrorCode { get; }
        public WebException(int errorCode, Exception inner = null) 
            : base(string.Format("{0} {1}", errorCode, HttpHelper.Response.GetStatusCodeName(errorCode)), inner)
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
