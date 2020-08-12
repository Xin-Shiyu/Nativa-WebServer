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
    }
}
