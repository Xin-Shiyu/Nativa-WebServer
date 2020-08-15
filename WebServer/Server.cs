using Nativa;
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WebServer
{
    internal class Server
    {
        private readonly IPageHandler handler;
        private readonly IErrorPageHandler errorPageHandler = new DefaultErrorPageHandler();
        private TcpListener listener;
        private readonly Logger logger;
        private readonly ServerSettings settings;

        public void Run()
        {
            IPAddress localAddr = IPAddress.Parse("0.0.0.0"); //必须在所有地址上侦听不然只有自己可以连
            listener = new TcpListener(localAddr, settings.port);
            listener.Start();
            logger.Log(string.Format("开始在端口 {0} 上侦听", settings.port));
            try
            {
                for (; ; )
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(new WaitCallback(HandleClientWrapper), client); //线程池会更快一点
                }
            }
            catch (SocketException)
            { } //退出的时候 listener.Stop() 会中断 AcceptTcpClient 的阻断过程，抛出异常，这里将其捕获
        }

        private void HandleClientWrapper(object client)
        {
            HandleClient((TcpClient)client);
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
                bool noMoreRequest = false;
                while (!stream.DataAvailable)
                {
                    Thread.Sleep(TimeSpan.Zero);
                    if (sw.ElapsedMilliseconds >= settings.keepAliveMaxDelay)
                    {
                        noMoreRequest = true;
                        break;
                    }
                }

                if (noMoreRequest)
                {
                    break; //若超时则结束连接
                }

                string requestString;
                {
                    StringBuilder sb = new StringBuilder();
                    Span<byte> buffer = stackalloc byte[1024];
                    int numberOfBytesRead;
                    do
                    {
                        numberOfBytesRead = stream.Read(buffer);
                        sb.Append(Encoding.ASCII.GetString(buffer.Slice(0, numberOfBytesRead)));
                    } while (stream.DataAvailable);
                    requestString = sb.ToString();
                }
                ++requestCount;

                try
                {
                    HttpHelper.Request request = HttpHelper.ParseRequest(in requestString);

                    if (request.Headers.ContainsKey(HeaderStrings.Connection) &&
                        MemoryExtensions.Equals(
                            request.Headers[HeaderStrings.Connection].Span,
                            HeaderStrings.KeepAlive,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        keepAlive = true;
                    }

                    logger.Log(string.Format("{0}\n{1}",
                            client.Client.RemoteEndPoint,
                            requestString));

                    var response = new HttpHelper.ResponseStream(stream,
                        request.Headers.ContainsKey(HeaderStrings.AcceptEncoding) &&
                        request.Headers[HeaderStrings.AcceptEncoding].Span.IndexOf(HeaderStrings.Gzip) != -1);

                    switch (request.Type)
                    {
                        case HttpHelper.RequestType.HEAD:
                            handler.WriteHead(request.URL.ToString(), response);
                            break;
                        case HttpHelper.RequestType.GET:

                            if (request.Headers.TryGetValue(HeaderStrings.Range, out var range))
                            {
                                if (HttpHelper.TryParseRange(range, out var begin, out var end))
                                {
                                    handler.WritePage(request.URL.ToString(), begin, end, response);
                                }
                                else
                                {
                                    handler.WritePage(request.URL.ToString(), begin, int.MaxValue, response);
                                }
                            }
                            else
                            {
                                handler.WritePage(request.URL.ToString(), response);
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    stream.Flush();
                }
                catch (WebException ex)
                {
                    logger.Err(ex.ToString());
                    try
                    {
                        errorPageHandler.WritePage(ex, new HttpHelper.ResponseStream(stream, false));
                    }
                    catch (Exception ex1)
                    {
                        logger.Err(ex1.ToString());
                        break;
                    }
                }
                catch (Exception ex)
                {
                    logger.Err(ex.ToString());
                    try
                    {
                        errorPageHandler.WritePage(new WebException(500, ex), new HttpHelper.ResponseStream(stream, false));
                    }
                    catch (Exception ex1)
                    {
                        logger.Err(ex1.ToString());
                        break;
                    }
                }

                if (!keepAlive || requestCount >= settings.keepAliveMaxRequestCount)
                {
                    break;
                }
            }
            
            sw.Stop();
            client.Close();
            client.Dispose();
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
