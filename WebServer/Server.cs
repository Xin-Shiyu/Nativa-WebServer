﻿using Nativa;
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
                    //Task.Run(() => HandleClient(client));
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
#if DEBUG
                logger.Dbg(string.Format("{0} 等待开始", sw.ElapsedTicks.ToString()));
#endif
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
#if DEBUG
                logger.Dbg(string.Format("{0} 等待结束", sw.ElapsedTicks.ToString()));
#endif
                if (noMoreRequest)
                {
                    break; //若超时则结束连接
                }

#if DEBUG
                logger.Dbg(string.Format("{0} 接受请求", sw.ElapsedTicks.ToString()));
#endif

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

#if DEBUG
                logger.Dbg(string.Format("{0} 接受结束", sw.ElapsedTicks.ToString()));
#endif

                bool doCompress = false;
                bool sendBody = true;
                HttpHelper.Response response;
                try
                {
#if DEBUG
                    logger.Dbg(string.Format("{0} 解析请求", sw.ElapsedTicks.ToString()));
#endif
                    HttpHelper.Request request = HttpHelper.ParseRequest(in requestString);

#if DEBUG
                    logger.Dbg(string.Format("{0} 解析结束", sw.ElapsedTicks.ToString()));
#endif
                    if (request.Headers.ContainsKey(HeaderStrings.Connection) &&
                        MemoryExtensions.Equals(
                            request.Headers[HeaderStrings.Connection].Span,
                            HeaderStrings.KeepAlive,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        keepAlive = true;
                    }
                    logger.Log(string.Format("{0} {1}",
                            client.Client.RemoteEndPoint,
                            requestString));
#if DEBUG
                    logger.Dbg(string.Format("{0} 创建响应", sw.ElapsedTicks.ToString()));
#endif
                    switch (request.Type)
                    {
                        case HttpHelper.RequestType.HEAD:
                            sendBody = false;
                            response = handler.GetHead(HttpHelper.DecodeURL(request.URL.ToString()));
                            break;
                        case HttpHelper.RequestType.GET:
                            if (request.Headers.TryGetValue(HeaderStrings.Range, out var range) &&
                                HttpHelper.TryParseRange(range, out var begin, out var end))
                            {
                                response = handler.GetPage(HttpHelper.DecodeURL(request.URL.ToString()), begin, end);
                            }
                            else
                            {
                                response = handler.GetPage(HttpHelper.DecodeURL(request.URL.ToString()));
                            }
                            break;
                        default:
                            throw new NotImplementedException();
                    }
#if DEBUG
                    logger.Dbg(string.Format("{0} 创建结束", sw.ElapsedTicks.ToString()));
#endif
                    if (request.Headers.ContainsKey(HeaderStrings.AcceptEncoding) &&
                        request.Headers[HeaderStrings.AcceptEncoding].Span.IndexOf(HeaderStrings.Gzip) != -1 && //就问你别扭不别扭
                        response.Body.Length > settings.compressMinSize)
                    {
                        doCompress = true;
                    }
                }
                catch (WebException ex)
                {
                    logger.Err(ex.ToString());
                    response = errorPageHandler.GetPage(ex);
                }
                catch (Exception ex)
                {
                    logger.Err(ex.ToString());
                    response = errorPageHandler.GetPage(new WebException(500, ex));
                }

                if (response != null)
                {
                    if (response.Headers != null)
                    {
                        if (doCompress && sendBody)
                        {
#if DEBUG
                            logger.Dbg(string.Format("{0} 压缩开始", sw.ElapsedTicks.ToString()));
#endif
                            using MemoryStream compressStream = new MemoryStream();
                            using (GZipStream zipStream = new GZipStream(compressStream, CompressionMode.Compress))
                            {
                                zipStream.Write(response.Body.Span);
                            }

                            response.Body = compressStream.ToArray();
                            response.Headers.TryAdd(HeaderStrings.ContentEncoding, HeaderStrings.Gzip);
#if DEBUG
                            logger.Dbg(string.Format("{0} 压缩结束", sw.ElapsedTicks.ToString()));
#endif
                        }
                        if (!response.Body.IsEmpty)
                            response.Headers.TryAdd(HeaderStrings.ContentLength, response.Body.Length.ToString()); //这是 keep-alive 模式所必需的
                        response.Headers.TryAdd(HeaderStrings.Server, "Nativa WebServer");
                        response.Headers.TryAdd(HeaderStrings.Date, DateTime.Now.ToString());
                        if (!keepAlive)
                        {
                            response.Headers.TryAdd(HeaderStrings.Connection, HeaderStrings.Close);
                        }
                    }
#if DEBUG
                    logger.Dbg(string.Format("{0} 生成并发送响应", sw.ElapsedTicks.ToString()));
#endif
                    try
                    {
                        response.WriteToStream(ref stream);
                        if (sendBody) stream.Write(response.Body.Span); //分开发送，免去拷贝
                    }
                    catch (IOException ex)
                    {
                        logger.Err(ex.ToString());
                    }
#if DEBUG
                    logger.Dbg(string.Format("{0} 响应完毕", sw.ElapsedTicks.ToString()));
#endif
                    stream.Flush();
                }

                if (!keepAlive || requestCount >= settings.keepAliveMaxRequestCount || response.Body.IsEmpty)
                {
                    break;
                }
            }
            
            sw.Stop();
            client.Close();
            client.Dispose(); //我猜这里应该 Dispose 掉
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
