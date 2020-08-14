using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;
using System.Linq;
using System.Net.Sockets;
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
        }

        public ref struct Request
        {
            public RequestType Type;
            public ReadOnlySpan<char> URL;
            public Dictionary<string, ReadOnlyMemory<char>> Arguments;
            public ReadOnlySpan<char> ProtocolVersion;
            public Dictionary<string, ReadOnlyMemory<char>> Headers;
            //public byte[] Data;
        }

        public class Response
        {
            public int StatusCode;
            public Dictionary<string, string> Headers;
            public byte[] Body;
            public static readonly byte[] crLf = Encoding.ASCII.GetBytes("\r\n");
            public static readonly byte[] colonSpace = Encoding.ASCII.GetBytes(": ");

            public void WriteToStream(ref NetworkStream stream)
            {
                stream.Write(Encoding.ASCII.GetBytes("HTTP/1.1"));
                stream.Write(Encoding.ASCII.GetBytes(StatusCodeString[StatusCode]));
                stream.Write(crLf);

                if (Headers != null)
                {
                    foreach (KeyValuePair<string, string> header in Headers)
                    {
                        stream.Write(Encoding.ASCII.GetBytes(header.Key));
                        stream.Write(colonSpace);
                        stream.Write(Encoding.ASCII.GetBytes(header.Value));
                        stream.Write(crLf);
                    }
                }
                stream.Write(crLf);
            }

            [Obsolete]
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

        public static Request ParseRequest(in string request) //奇技淫巧
        {
            Request res = new Request();

            var span = request.AsSpan();
            int left = 0;
            int right = span.IndexOf(' '); //这两个东西相当于指针，在 span 上面移动
            var requestType = span[left..right]; //先读入第一个空格前面的东西，也就是请求类型
            if (MemoryExtensions.Equals(requestType, "GET", StringComparison.Ordinal))
            {
                res.Type = RequestType.GET;
            }
            else if (MemoryExtensions.Equals(requestType, "HEAD", StringComparison.Ordinal))
            {
                res.Type = RequestType.HEAD;
            }
            else
            {
                throw new NotSupportedException();
            }
            // ?a=b&c=d&e=f
            left = right + 1; //空格的后一个字符
            right = left + span[left..].IndexOf(' ');
            var URL = span[left..right]; //这不代表真正的 URL，因为后面可能还有查询字符串

            int innerRight; //这个之后会用到，用来分隔键值

            if ((left = URL.IndexOf('?')) != -1) //解析查询字符串
            {
                res.URL = span[..left]; //字面上上有点奇怪，但是结合上下文可以知道确切语义
                res.Arguments = new Dictionary<string, ReadOnlyMemory<char>>();
                ++left; //原本 left 的位置是在 ? 上的
                string key;
                while ((right = URL[left..].IndexOf('&')) != -1) //如果不止一个参数
                {
                    right += left; //加上 left，偏移到实际的位置而不是子串的位置
                    //此时 right 的位置在 & 上
                    innerRight = left + URL[left..].IndexOf('=');
                    key = URL[left..innerRight].ToString(); //毕竟 key 还得是 string 所以转回去，谁叫切片不能 GetHashCode
                    left = innerRight + 1;
                    res.Arguments.Add(key, 
                                      // 等号左边的为键
                                      request.AsMemory()[left..right]); // 等号右边的为值，不能从 span 里切了，因为这里的类型是 memory
                    left = right + 1; //左边移到右边的右边，进入下一个 entry
                }
                //下面是最后一个参数或者唯一一个参数的切分，前三句和循环里一样，但是因为在边缘必须分开处理
                innerRight = left + URL[left..].IndexOf('=');
                key = URL[left..innerRight].ToString();
                left = innerRight + 1;
                right = left + URL[left..].IndexOf(' '); //到下一个空格处为止
                res.Arguments.Add(key, request.AsMemory()[left..right]); //最后一个参数
            }
            else
            {
                res.URL = URL; //没有查询字符串是最好的，直接给它就行了
            }

            //这时候到了 HTTP/1.1 的部分
            left = right + 1;
            right = left + span[left..].IndexOf("\r\n"); //下一个分隔应当是回车加换行
            res.ProtocolVersion = span[left..right];

            left = right + 2; //因为回车加换行为两个字符，都跳过
            //下面开始故技重施，和切分查询字符串差不多，只不过边缘情况这里是两个回车加换行（或曰空行），之前的则是 & 变成空格
            right = left + span[left..].IndexOf("\r\n"); //consume 一整行;
            res.Headers = new Dictionary<string, ReadOnlyMemory<char>>();
            while ((innerRight = span[left..right].IndexOf(':')) != -1) //判断标准就是有没有键值分隔符 ：
            {
                innerRight += left; //同理偏移
                string key = span[left..innerRight].ToString(); //同之前的理，没办法，key 仿佛必须是 string
                left = innerRight + 1;
                while (span[left] == ' ') ++left; //跳过空白，原本用 Trim，但这里怎么可能 Trim 呢，我们根本没有独立的字符串，全是切片
                res.Headers.Add(key, request.AsMemory()[left..right]); //同理不解释
                left = right + 2;
                right = left + span[left..].IndexOf("\r\n"); //去下一行
            }
            //如果是 POST 后面还有数据可我不想管它了哈哈哈哈反正目前也没打算支持 POST

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
