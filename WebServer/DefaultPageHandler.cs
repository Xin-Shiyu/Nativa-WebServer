using Nativa;
using System;
using System.Collections.Generic;
using System.IO;

namespace WebServer
{
    internal class DefaultPageHandler : IPageHandler
    {
        private readonly FileCache cache;
        private readonly Logger logger;
        private readonly DefaultPageHandlerSettings settings;
        private readonly Dictionary<string, string> contentTypeDictionary =
            new Dictionary<string, string>
            {
                { "txt" , "text/plain" },
                { "xml" , "text/xml" },
                { "html", "text/html" },
                { "css" , "text/css" },
                { "js" , "text/javascript" },
                { "png" , "image/png" },
                { "gif" , "image/gif" },
                { "jpg" , "image/jpeg" },
                { "ico" , "image/x-icon" },
            };

        HttpHelper.Response IPageHandler.GetPage(string URI, bool onlyHead)
        {
            HttpHelper.Response res = new HttpHelper.Response();
            string actualPath = GetActualPath(URI);
            if (Directory.Exists(actualPath) && !File.Exists(actualPath))
            {
                actualPath = Path.Combine(actualPath, settings.DefaultPage);
            }
            CheckPathAccessibility(ref actualPath);
            string contentType = GetContentType(ref actualPath);

            res.StatusCode = File.Exists(actualPath) ? 200 : throw new FileNotFoundException();
            res.Headers = new Dictionary<string, string>
            {
                { "Content-Type", contentType },
            };

            if (!onlyHead)
            {
                //if (contentType[..5] == "text")
                //{
                //    res.Body = cache.ReadTextFile(actualPath);
                //}
                //else
                //{
                res.Body = cache.ReadFile(actualPath);
                //}
            }

            res.Headers.Add("Cache-Control", String.Format("max-age={0}", cache.GetFileLife(actualPath)));

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

        private string GetContentType(ref string filePath)
        {
            if (contentTypeDictionary.TryGetValue(filePath[(filePath.LastIndexOf('.') + 1)..].ToLower(), out var ret))
            {
                return ret;
            }
            return "application/octet-stream";
        }
    }
}
