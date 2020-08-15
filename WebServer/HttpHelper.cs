using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.Design;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
        internal const string AcceptRanges = "Accept-Ranges";
        internal const string Gzip = "gzip";
        internal const string ContentEncoding = "Content-Encoding";
        internal const string ContentLength = "Content-Length";
        internal const string ContentRange = "Content-Range";
        internal const string Close = "Close";
        internal const string Range = "Range";
        internal const string KeepAlive = "keep-alive";
        internal const string Bytes = "bytes";
        public static readonly byte[] TransferEncoding = Encoding.ASCII.GetBytes("Transfer-Encoding");
        public static readonly byte[] Chunked = Encoding.ASCII.GetBytes("chunked");
        public static readonly byte[] Server = Encoding.ASCII.GetBytes("Server");
        public static readonly byte[] NWS = Encoding.ASCII.GetBytes("Nativa Web Server");
        public static readonly byte[] Date = Encoding.ASCII.GetBytes("Date");
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

        public class ResponseStream
        {
            private static readonly byte[] http11 = Encoding.ASCII.GetBytes("HTTP/1.1");
            private static readonly byte[] crLf = Encoding.ASCII.GetBytes("\r\n");
            private static readonly byte[] colonSpace = Encoding.ASCII.GetBytes(": ");
            private static readonly byte[] space = Encoding.ASCII.GetBytes(" ");
            private static readonly byte[] zero = Encoding.ASCII.GetBytes("0");
            public static Dictionary<int, byte[]> StatusCodeString = new Dictionary<int, byte[]>
            {
                { 200, Encoding.ASCII.GetBytes("200 OK") },
                { 206, Encoding.ASCII.GetBytes("206 Partial Content") },
                { 301, Encoding.ASCII.GetBytes("301 Moved Permanently") },
                { 404, Encoding.ASCII.GetBytes("404 Not Found") },
                { 403, Encoding.ASCII.GetBytes("403 Forbidden") },
                { 500, Encoding.ASCII.GetBytes("500 Internal Server Error") },
            };

            private enum Stage
            {
                Status,
                Headers,
                Body,
            }
            private Stage stage = Stage.Status;

            private Stream stream;
            private bool useCompression;
            private int status;

            public ResponseStream(Stream stream, bool useCompression)
            {
                this.stream = stream;
                this.useCompression = useCompression;
            }

            public void WriteStatus(int status)
            {
                if (stage == Stage.Status)
                {
                    this.status = status;
                    stream.Write(http11);
                    stream.Write(space);
                    stream.Write(StatusCodeString[status]);
                    stream.Write(crLf);
                    stream.Flush();
                    stage = Stage.Headers;
                }
            }

            public void WriteHeader(string key, string value)
            {
                if (stage == Stage.Headers)
                {
                    WriteHeader(Encoding.ASCII.GetBytes(key), Encoding.ASCII.GetBytes(value));
                }
            }

            public void WriteHeader(byte[] key, byte[] value)
            {
                stream.Write(key);
                stream.Write(colonSpace);
                stream.Write(value);
                stream.Write(crLf);
            }

            public void WriteBody(ReadOnlyMemory<byte> body)
            {
                if (stage == Stage.Headers)
                {
                    WriteCommonHeaders();
                    if (useCompression && status != 206)
                    {
                        WriteHeader(HeaderStrings.ContentEncoding, HeaderStrings.Gzip);
                        //WriteHeader("Vary", "Accept-Encoding");
                        using MemoryStream compressStream = new MemoryStream();
                        using (GZipStream zipStream = new GZipStream(compressStream, CompressionMode.Compress))
                        {
                            zipStream.Write(body.Span);
                        }
                        body = compressStream.ToArray();
                    }
                    if (status != 206) WriteHeader(HeaderStrings.ContentLength, body.Length.ToString());

                    stream.Write(crLf);
                    stage = Stage.Body;
                    stream.Write(body.Span);
                }
            }

            public delegate IEnumerable<Memory<byte>> ChunkingProvider();

            public void WriteBody(ChunkingProvider provider)
            {
                if (stage == Stage.Headers)
                {
                    WriteCommonHeaders();
                    stage = Stage.Body;
                    WriteHeader(HeaderStrings.TransferEncoding, HeaderStrings.Chunked);
                    stream.Write(crLf);
                    foreach (var chunk in provider())
                    {
                        stream.Write(Encoding.ASCII.GetBytes(chunk.Length.ToString("X")));
                        stream.Write(crLf);
                        stream.Write(chunk.Span);
                        stream.Write(crLf);
                        stream.Flush();
                    }
                    stream.Write(zero);
                    stream.Write(crLf);
                    stream.Write(crLf);
                }
            }

            private void WriteCommonHeaders()
            {
                WriteHeader(HeaderStrings.Server, HeaderStrings.NWS);
                WriteHeader(HeaderStrings.Date, Encoding.ASCII.GetBytes(DateTime.Now.ToUniversalTime().ToString("R")));
            }

            public void FinishSession()
            {
                switch (stage)
                {
                    case Stage.Headers:
                        WriteCommonHeaders();
                        break;
                    default:
                        break;
                }
                stage = Stage.Status;
            }
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

        public static bool TryParseRange(ReadOnlyMemory<char> text, out int begin, out int end)
        {
            begin = 0;
            end = 0;

            var span = text.Span;

            if (span.IndexOf(',') != -1) //暂不支持多段区间
            {
                return false; 
            }

            var range = span[(span.IndexOf('=') + 1)..];
            return
                int.TryParse(range[..range.IndexOf('-')], out begin) &&
                int.TryParse(range[(range.IndexOf('-') + 1)..], out end);
        }
    }
}
