using Nativa;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;

namespace WebServer
{
    internal class DefaultPageHandler : IPageHandler
    {
        private readonly FileCache cache;
        private readonly Logger logger;
        private readonly DefaultPageHandlerSettings settings;

        HttpHelper.Response IPageHandler.GetPage(string URI, bool onlyHead)
        {
            HttpHelper.Response res = new HttpHelper.Response();
            string actualPath = GetActualPath(URI);
            if (Directory.Exists(actualPath) && !File.Exists(actualPath))
            {
                actualPath = Path.Combine(actualPath, settings.DefaultPage);
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

        public DefaultPageHandler(DefaultPageHandlerSettings handlerSettings, Logger logger, FileCacheSettings cacheSettings)
        {
            if (!Directory.Exists(handlerSettings.PhysicalBasePath))
            {
                Directory.CreateDirectory(handlerSettings.PhysicalBasePath);
            }
            settings = handlerSettings;
            this.logger = logger;
            cache = new FileCache(logger, cacheSettings);
            logger.Log("使用默认页面处理模块。");
            logger.Log(string.Format("网站物理路径位于：{0}", settings.PhysicalBasePath));
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
            return Path.Combine(settings.PhysicalBasePath, URI[i..]);
        }

        private void CheckPathAccessibility(ref string path)
        {
            if (!path.Contains(settings.PhysicalBasePath)) throw new UnauthorizedAccessException();
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
