using System;
using System.Collections.Generic;
using System.Text;

namespace WebServer
{
    class DefaultErrorPageHandler : IErrorPageHandler
    {
        HttpHelper.Response IErrorPageHandler.GetPage(WebException exception)
        {
            return new HttpHelper.Response
            {
                StatusCode = exception.ErrorCode,
                Headers = new Dictionary<string, string>
                            {
                                { HeaderStrings.ContentType, "text/html" }
                            },
                Body = Encoding.UTF8.GetBytes(CreateErrorPage(exception.Message, exception.InnerException?.Message))
            };
        }

        private static string CreateErrorPage(string errorType, string furtherInformation)
        {
            return string.Format(
                "<html>" +
                    "<head><title>{0}</title></head>" +
                    "<body>" +
                        "<center>" +
                            "<h1>{0}</h1>" +
                            "<hr/>" +
                            "<p>Nativa WebServer</p>" +
                            "<pre>{1}</pre>" +
                        "</center>" +
                    "</body>" +
                "</html>",
                errorType,
                furtherInformation);
        }
    }
}
