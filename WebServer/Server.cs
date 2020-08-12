using Nativa;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WebServer
{
    internal class Server
    {
        private readonly IPageHandler handler;
        private TcpListener listener;
        private readonly Logger logger;
        private ServerSettings settings;
        //private bool isRunning;

        public void Run()
        {
            IPAddress localAddr = IPAddress.Parse("0.0.0.0"); //必须在所有地址上侦听不然只有自己可以连
            listener = new TcpListener(localAddr, settings.port);
            //isRunning = true;
            listener.Start();
            logger.Log(string.Format("开始在端口 {0} 上侦听", settings.port));
            for (; ; )
            {
                TcpClient client = listener.AcceptTcpClient();
                Task.Run(() => HandleClient(client));
            }
        }

        private void HandleClient(TcpClient client)
        {
            bool keepAlive = false;
            NetworkStream stream = client.GetStream();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int requestCount = 0;

            for (; ; )
            {
                byte[] buffer = new byte[256];
                StringBuilder sb = new StringBuilder();
                int numberOfBytesRead;

                while (sw.ElapsedMilliseconds < settings.keepAliveMaxDelay) //等待一定时长
                {
                    if (stream.DataAvailable)
                    {
                        break; //若请求来了则停止等待
                    }
                }
                if (!stream.DataAvailable)
                {
                    break; //若超时则结束连接
                }
                
                do
                {
                    numberOfBytesRead = stream.Read(buffer, 0, buffer.Length);
                    sb.Append(Encoding.ASCII.GetString(buffer, 0, numberOfBytesRead));
                } while (stream.DataAvailable);
                ++requestCount;

                bool doCompress = false;
                HttpHelper.Response response;
                try
                {
                    HttpHelper.Request request = HttpHelper.ParseRequest(sb.ToString());
                    if (request.Headers.ContainsKey("Connection") &&
                        request.Headers["Connection"] == "keep-alive")
                    {
                        keepAlive = true;
                    }
                    logger.Log(
                        string.Format(
                            "{0} {1} {2} [{3}] {4}",
                            client.Client.RemoteEndPoint,
                            request.Type,
                            request.URL,
                            request.Arguments != null ?
                            string.Join("; ",request.Arguments.ToList()) :
                            "N/A",
                            request.Headers.ContainsKey("User-Agent") ? 
                            request.Headers["User-Agent"] :
                            "N/A"
                            ));
                    response = request.Type switch
                    {
                        HttpHelper.RequestType.GET => handler.GetPage(request.URL),
                        HttpHelper.RequestType.HEAD => handler.GetPage(request.URL, onlyHead: true),
                        _ => throw new NotImplementedException(),
                    };
                    if (request.Headers.ContainsKey("Accept-Encoding") &&
                        request.Headers["Accept-Encoding"].Contains("gzip"))
                    {
                        doCompress = true;
                    }
                }
                catch (Exception ex)
                {
                    logger.Err(ex.ToString());
                    switch (ex.GetType().Name)
                    {
                        case "FileNotFoundException":
                        case "DirectoryNotFoundException":
                            response = CreateErrorResponse(404);
                            break;
                        case "UnauthorizedAccessException":
                            response = CreateErrorResponse(403);
                            break;
                        default:
                            response = CreateErrorResponse(500, ex.Message);
                            break;
                    }
                }

                if (response != null)
                {
                    if (response.Headers != null)
                    {
                        if (doCompress && response.Body.Length > settings.compressMinSize)
                        {
                            using MemoryStream compressStream = new MemoryStream();
                            using (GZipStream zipStream = new GZipStream(compressStream, CompressionMode.Compress))
                            {
                                zipStream.Write(response.Body, 0, response.Body.Length);
                            }

                            response.Body = compressStream.ToArray();
                            response.Headers.TryAdd("Content-Encoding", "gzip");
                        }
                        response.Headers.TryAdd("Content-Length", response.Body.Length.ToString()); //这是 keep-alive 模式所必需的
                        response.Headers.TryAdd("Server", "Nativa WebServer");
                        response.Headers.TryAdd("Date", DateTime.Now.ToString());
                        if (!keepAlive)
                        {
                            response.Headers.TryAdd("Connection", "Close");
                        }
                    }
                    stream.Write(response.ToByteArray());
                    stream.Flush();
                }

                GC.Collect();
                if (!keepAlive || requestCount >= settings.keepAliveMaxRequestCount)
                {
                    break;
                }
            }

            sw.Stop();
            client.Close();
        }

        private static HttpHelper.Response CreateErrorResponse(int statusCode, string furtherInformation = "")
        {
            return new HttpHelper.Response
            {
                StatusCode = statusCode,
                Headers = new Dictionary<string, string>
                            {
                                { "Content-Type", "text/html" }
                            },
                Body = Encoding.UTF8.GetBytes(
                               CreateErrorPage(
                                   string.Format(
                                       "{0} {1}",
                                       statusCode,
                                       HttpHelper.Response.GetStatusCodeName(statusCode)),
                                   furtherInformation))
            };
        }

        private static string CreateErrorPage(string errorType, string furtherInformation)
        {
            return string.Format(
                "<html>" +
                    "<head><title>{0}</title></head>" +
                    "<body>" +
                        "<center>" +
                            "<h1>{0}</h1>" +
                            "<hr/>" +
                            "<p>Nativa WebServer</p>" +
                            "<pre>{1}</pre>" +
                        "</center>" +
                    "</body>" +
                "</html>",
                errorType,
                furtherInformation);
        }

        public Server(ServerSettings settings, Logger logger, IPageHandler handler)
        {
            this.settings = settings;
            this.handler = handler;
            this.logger = logger;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            logger.Log(string.Format("Nativa WebServer 创建了一个新实例，使用端口 {0}", settings.port));
        }

        private void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            listener.Stop();
            logger.Log("进程被终结");
            logger.ForceSave();
        }
    }
}
