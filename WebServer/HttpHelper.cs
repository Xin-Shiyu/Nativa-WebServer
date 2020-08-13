using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WebServer
{
    internal static class HeaderStrings
    {
        internal const string CacheControl = "Cache-Control";
        internal const string ContentType = "Content-Type";
        internal const string Connection = "Connection";
        internal const string UserAgent = "User-Agent";
        internal const string AcceptEncoding = "Accept-Encoding";
        internal const string Gzip = "gzip";
        internal const string ContentEncoding = "Content-Encoding";
        internal const string ContentLength = "Content-Length";
        internal const string Server = "Server";
        internal const string Date = "Date";
        internal const string Close = "Close";
    }

    internal class HttpHelper
    {
        public enum RequestType
        {
            GET,
            HEAD,
            POST,
            PUT,
            DELETE,
            CONNECT,
            OPTIONS,
            TRACE,
            PATCH,
        }

        public class Request
        {
            public RequestType Type;
            public string URL;
            public Dictionary<string, string> Arguments;
            public string ProtocolVersion;
            public Dictionary<string, string> Headers;
            //public byte[] Data;
        }

        public class Response
        {
            public int StatusCode;
            public Dictionary<string, string> Headers;
            public byte[] Body;

            public byte[] HeadToByteArray()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("HTTP/1.1 ");
                sb.Append(StatusCodeString[StatusCode]);
                sb.Append("\r\n");

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> header in Headers)
                    {
                        sb.Append(header.Key);
                        sb.Append(": ");
                        sb.Append(header.Value);
                        sb.Append("\r\n");
                    }
                }
                sb.Append("\r\n");

                return Encoding.ASCII.GetBytes(sb.ToString()); //HTTP 头应该是 ASCII 编码
            }

            public static Dictionary<int, string> StatusCodeString = new Dictionary<int, string>
            {
                { 200, "200 OK" },
                { 301, "301 Moved Permanently" },
                { 404, "404 Not Found" },
                { 403, "403 Forbidden" },
                { 500, "500 Internal Server Error" },
            };
        }

        public static Request ParseRequest(string request)
        {
            Request res = new Request();
            string[] lines = request.Split("\r\n"); //Encoding.ASCII.GetString(request).Split("\r\n");

            string[] requestLine = lines[0].Split(' ');
            res.Type = requestLine[0] switch
            {
                "GET" => RequestType.GET,
                "HEAD" => RequestType.HEAD,
                "POST" => RequestType.POST,
                "PUT" => RequestType.POST,
                "DELETE" => RequestType.DELETE,
                "CONNECT" => RequestType.CONNECT,
                "OPTIONS" => RequestType.OPTIONS,
                "TRACE" => RequestType.TRACE,
                "PATCH" => RequestType.PATCH,
                _ => throw new NotSupportedException("意料之外的请求类型 " + requestLine[0])
            };
            if (requestLine[1].Contains('?'))
            {
                res.URL = requestLine[1][..requestLine[1].IndexOf('?')];
                res.Arguments = requestLine[1][(requestLine[1].IndexOf('?') + 1)..]
                    .Split('&')
                    .ToDictionary(part => DecodeURL(part[..part.IndexOf('=')]), part => DecodeURL(part[(part.IndexOf('=') + 1)..]));
            }
            else
            {
                res.URL = DecodeURL(requestLine[1]);
            }
            res.ProtocolVersion = requestLine[2];

            res.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string line in lines[1..])
            {
                if (line.Length == 0)
                {
                    break; //之后是数据部分
                }

                string field = line[..line.IndexOf(':')];
                string value = line[(field.Length + 1)..];
                res.Headers.Add(field, value.Trim());
            }

            return res;
        }

        public static string DecodeURL(string text) //这个算法可能不是很干净，也是默认 UTF8
        {
            text = text.Replace('+', ' ');
            if (!text.Contains('%'))
            {
                return text;
            }

            MemoryStream stream = new MemoryStream();
            StringBuilder sb = new StringBuilder();
            bool inCode = false;
            int lastRawPoint = 0;
            for (int i = 0; i < text.Length; ++i)
            {
                if (text[i] == '%')
                {
                    if (!inCode)
                    {
                        sb.Append(text[lastRawPoint..i]);
                        inCode = true;
                    }
                    stream.WriteByte(byte.Parse(text.Substring(i + 1, 2), System.Globalization.NumberStyles.HexNumber));
                    i += 2;
                }
                else
                {
                    if (inCode)
                    {
                        lastRawPoint = i;
                        sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
                        stream.SetLength(0); //清空字节流
                        inCode = false;
                    }
                }
            }
            if (stream.Length != 0)
            {
                sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
            }
            else
            {
                sb.Append(text[lastRawPoint..]); //假如 stream 为空则最后一个片段不带 %，一定剩下了生字符串
            }

            stream.Dispose();
            return sb.ToString();
        }
    }
}
