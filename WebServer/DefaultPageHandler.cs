using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebServer
{
    internal class DefaultPageHandler : IPageHandler
    {
        public string DefaultPage = "index.html";
        public readonly string PhysicalBasePath;
        private readonly FileCache cache = new FileCache();

        HttpHelper.Response IPageHandler.GetPage(string URI, bool onlyHead)
        {
            HttpHelper.Response res = new HttpHelper.Response();
            string actualPath = GetActualPath(URI);
            if (Directory.Exists(actualPath) && !File.Exists(actualPath))
            {
                actualPath = Path.Combine(actualPath, DefaultPage);
            }
            CheckPathAccessibility(ref actualPath);
            string contentType = GetContentType(actualPath);

            res.StatusCode = File.Exists(actualPath) ? 200 : throw new FileNotFoundException();
            res.Headers = new Dictionary<string, string>
            {
                { "Content-Type", contentType },
                { "Cache-Control", "max-age=31536000" } //不知道到底应该怎么安排这个比较好，先写上试试
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
                        res.Body = cache.ReadTextFile(actualPath);
                        break;
                    default:
                        res.Body = cache.ReadFile(actualPath);
                        break;
                }
            }

            return res;
        }

        public DefaultPageHandler(string physicalBasePath)
        {
            if (!Directory.Exists(physicalBasePath))
            {
                Directory.CreateDirectory(physicalBasePath);
            }
            PhysicalBasePath = physicalBasePath;
        }

        private string GetActualPath(string URI)
        {
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

        private void CheckPathAccessibility(ref string path)
        {
            if (!path.Contains(PhysicalBasePath)) throw new UnauthorizedAccessException();
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
