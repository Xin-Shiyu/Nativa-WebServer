using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebServer
{
    internal class DefaultPageHandler : IPageHandler
    {
        public string DefaultPage = "index.html";
        public readonly string PhysicalBasePath;

        HttpHelper.Response IPageHandler.GetPage(string URI, bool onlyHead)
        {
            HttpHelper.Response res = new HttpHelper.Response();
            string actualPath = GetActualPath(URI);
            string contentType = GetContentType(actualPath);

            res.StatusCode = File.Exists(actualPath) ? 200 : throw new FileNotFoundException();
            res.Headers = new Dictionary<string, string>
            {
                { "Content-Type", contentType },
                //{ "Cache-Control", "max-age=31536000" }
            };

            if (!onlyHead)
            {
                switch (contentType)
                {
                    case "text/plain":
                    case "text/html":
                    case "text/xml":
                    case "text/css":
                    case "text/javascript":
                        res.Body = Encoding.UTF8.GetBytes(File.ReadAllText(actualPath)); //不管三七二十一先转成 UTF-8 再说
                        break;
                    default:
                        res.Body = File.ReadAllBytes(actualPath);
                        break;
                }
            }

            return res;
        }

        public DefaultPageHandler(string physicalBasePath)
        {
            PhysicalBasePath = physicalBasePath;
        }

        private string GetActualPath(string URI)
        {
            if (URI == "/")
            {
                URI = DefaultPage;
            }

            int i = 0;
            for (; i < URI.Length; ++i)
            {
                if (URI[i] != '/')
                {
                    break;
                }
            }
            return Path.Combine(PhysicalBasePath, URI[i..]);
        }

        private string GetContentType(string filePath)
        {
            return filePath[(filePath.LastIndexOf('.') + 1)..].ToLower() switch
            {
                "txt" => "text/plain",
                "xml" => "text/xml",
                "html" => "text/html",
                "css" => "text/css",
                "js" => "text/javascript",
                "png" => "image/png",
                "gif" => "image/gif",
                "jpg" => "image/jpeg",
                "ico" => "image/x-icon",
                _ => "application/octet-stream"
            };
        }
    }
}
