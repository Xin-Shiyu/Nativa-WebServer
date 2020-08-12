using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WebServer
{
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

            public byte[] ToByteArray()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(string.Format(
                    "HTTP/1.1 {0} {1}\r\n",
                    StatusCode,
                    GetStatusCodeName(StatusCode)));
                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> header in Headers)
                    {
                        sb.Append(string.Format("{0}: {1}\r\n", header.Key, header.Value));
                    }
                }
                sb.Append("\r\n");

                byte[] head = Encoding.UTF8.GetBytes(sb.ToString());
                byte[] res = new byte[head.Length + Body.Length];
                head.CopyTo(res, 0);
                Body.CopyTo(res, head.Length);

                return res;
            }

            public static string GetStatusCodeName(int statusCode)
            {
                return statusCode switch
                {
                    200 => "OK",
                    301 => "Moved Permanently",
                    404 => "Not Found",
                    403 => "Forbidden",
                    500 => "Internal Server Error",
                    _ => ""
                };
            }
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

            res.Headers = new Dictionary<string, string>();
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
            if (!text.Contains('%')) return text;
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
            if (stream.Length != 0) sb.Append(Encoding.UTF8.GetString(stream.ToArray()));
            else sb.Append(text[lastRawPoint..]); //假如 stream 为空则最后一个片段不带 %，一定剩下了生字符串
            stream.Dispose();
            return sb.ToString();
        }
    }
}
