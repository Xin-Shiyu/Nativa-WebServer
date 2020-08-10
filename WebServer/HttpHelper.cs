using System;
using System.Collections.Generic;
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
            res.URL = requestLine[1];
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
    }
}
