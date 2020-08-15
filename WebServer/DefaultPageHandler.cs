using Nativa;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Net;

namespace WebServer
{
    internal class DefaultPageHandler : IPageHandler
    {
        private readonly FileCache cache;
        private readonly Logger logger;
        private readonly DefaultPageHandlerSettings settings;
        private readonly Dictionary<string, string> contentTypeDictionary =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
        /*
        HttpHelper.Response IPageHandler.GetPage(string URI)
        {
            string actualPath = GetActualPath(URI);
            if (cache.GetFileLength(actualPath) >= maxWholeFileLength)
            {
                return GetPartialPage(ref actualPath, 0, maxWholeFileLength);
            }

            return new HttpHelper.Response
            {
                StatusCode = 200,
                Body = cache.ReadFile(actualPath),
                Headers = new Dictionary<string, string>
                {
                    { HeaderStrings.ContentType , GetContentType(ref actualPath) },
                    { HeaderStrings.CacheControl, string.Format("max-age={0}", cache.GetFileLife(actualPath)) },
                    { HeaderStrings.AcceptRanges, HeaderStrings.Bytes }
                }
            };
        }

        HttpHelper.Response IPageHandler.GetHead(string URI)
        {
            string actualPath = GetActualPath(URI);
            return new HttpHelper.Response
            {
                StatusCode = 200,
                Headers = new Dictionary<string, string>
                {
                    { HeaderStrings.ContentType , GetContentType(ref actualPath) },
                    { HeaderStrings.ContentLength, cache.GetFileLength(actualPath).ToString() },
                    { HeaderStrings.AcceptRanges, HeaderStrings.Bytes }
                }
            };
        }

        HttpHelper.Response IPageHandler.GetPage(string URI, int begin, int end)
        {
            string actualPath = GetActualPath(URI);
            return GetPartialPage(ref actualPath, begin, end);
        }

        private HttpHelper.Response GetPartialPage(ref string actualPath, int begin, int end)
        {
            return new HttpHelper.Response
            {
                StatusCode = 206,
                Body = cache.ReadPartialFile(actualPath, begin, end, out var fullLength),
                Headers = new Dictionary<string, string>
                    {
                        { HeaderStrings.ContentType , GetContentType(ref actualPath) },
                        { HeaderStrings.CacheControl, string.Format("max-age={0}", cache.GetFileLife(actualPath)) },
                        { HeaderStrings.AcceptRanges, HeaderStrings.Bytes },
                        { HeaderStrings.ContentRange, string.Format("bytes {0}-{1}/{2}", begin, end, fullLength) }
                    }
            };
        }
        */
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
            var res = Path.Combine(settings.PhysicalBasePath, URI[i..]);
            if (Directory.Exists(res) && !File.Exists(res))
            {
                res = Path.Combine(res, settings.DefaultPage);
            }
            if (!File.Exists(res)) throw WebException.GetException(404);
            CheckPathAccessibility(ref res);
            return res;
        }

        private void CheckPathAccessibility(ref string path)
        {
            if (!path.Contains(settings.PhysicalBasePath))
            {
                throw WebException.GetException(403);
            }
        }

        private string GetContentType(ref string filePath)
        {
            if (contentTypeDictionary.TryGetValue(filePath[(filePath.LastIndexOf('.') + 1)..].ToLower(), out string ret))
            {
                return ret;
            }
            return "application/octet-stream";
        }

        public void WritePage(string URI, in HttpHelper.ResponseStream stream)
        {
            WriteHeadInternal(URI, in stream, out var actualPath);
            if (cache.GetFileLength(actualPath) > 1024 * 1024)
            {
                //不经过 cache 了，直接读，因为 ReadPartialFile 另有它用
                stream.WriteBody(provider);
                IEnumerable<Memory<byte>> provider()
                {
                    using var fileStream = new FileStream(actualPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    byte[] buffer = new byte[1024];
                    while (fileStream.Read(buffer) != 0)
                    {
                        yield return buffer;
                    }
                    yield break;
                }
            }
            else
            {
                stream.WriteBody(cache.ReadFile(actualPath));
            }
        }

        public void WritePage(string URI, int begin, int end, in HttpHelper.ResponseStream stream)
        {
            WriteHeadInternal(URI, in stream, out var actualPath, 206);
            var provider = cache.ReadPartialFileChunked(actualPath, begin, ref end, out var fullLength);
            stream.WriteHeader(HeaderStrings.ContentRange, string.Format("byte {0}-{1}/{2}", begin, end, fullLength));
            stream.WriteBody(provider);
        }

        public void WriteHead(string URI, HttpHelper.ResponseStream stream)
        {
            WriteHeadInternal(URI, in stream, out _);
            stream.FinishSession();
        }

        private void WriteHeadInternal(string URI, in HttpHelper.ResponseStream stream, out string actualPath, int status = 200)
        {
            actualPath = GetActualPath(URI);
            stream.WriteStatus(status);
            stream.WriteHeader(HeaderStrings.ContentType, GetContentType(ref actualPath));
            //stream.WriteHeader(HeaderStrings.ContentLength, cache.GetFileLength(actualPath).ToString());
        }
    }
}
