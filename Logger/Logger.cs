using System;
using System.IO;
using System.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace Nativa
{
    public class Logger
    {
        private enum LogType
        {
            INFO,
            ERR,
            DEBUG,
        }

        private struct LogPiece
        {
            public DateTime Time;
            public LogType Type;
            public string Message;
            public override string ToString()
            {
                return string.Format(
                        "{0}\t{1}\t{2}",
                        Time,
                        Type switch
                        {
                            LogType.INFO => "消息",
                            LogType.ERR => "错误",
                            LogType.DEBUG => "调试",
                            _ => "未知消息类型"
                        },
                        Message
                        );
            }
        }

        private readonly string saveLocation;
        private readonly bool showOnScreen = true;

        private readonly ConcurrentQueue<LogPiece> queue = new ConcurrentQueue<LogPiece>();
        private bool terminate = false;
        private bool finishedSaving = false;

        public void Log(string logContent)
        {
            LogWithType(logContent, LogType.INFO);
        }

        public void Err(string logContent)
        {
            LogWithType(logContent, LogType.ERR);
        }

        public void Dbg(string logContent)
        {
            LogWithType(logContent, LogType.DEBUG);
        }

        private void LogWithType(string logContent, LogType logType)
        {
            queue.Enqueue(new LogPiece
            {
                Message = logContent,
                Type = logType,
                Time = DateTime.Now
            });
        }

        private void WorkingLoop()
        {
            var logBuffer = new StringBuilder();
            for (; ; )
            {
                while (queue.TryDequeue(out var logPiece))
                {
                    var line = logPiece.ToString();
                    if (showOnScreen) Console.WriteLine(line);
                    logBuffer.AppendLine(line);
                }
                if (logBuffer.Length >= 1048576 || terminate)
                {
                    using var log = File.Create(Path.Combine(saveLocation, string.Format("{0}.log", DateTime.Now.ToString("yyyyMMddHHmmssffff"))));
                    log.Write(Encoding.UTF8.GetBytes(logBuffer.ToString()));
                    logBuffer.Clear();
                    if (terminate)
                    {
                        finishedSaving = true;
                        return;
                    }
                }
                Thread.Sleep(5000);
            }
        }

        public void ForceSave()
        {
            terminate = true;
            Console.WriteLine("请等待日志存盘……");
            while (!finishedSaving) { Thread.Sleep(1); } //如果不 Sleep 就一直读不到 finishedSaving 的实际值，不知道为什么
        }


        public Logger(string saveLocation, bool showOnScreen)
        {
            this.saveLocation = saveLocation;
            if (!Directory.Exists(saveLocation))
            {
                Directory.CreateDirectory(saveLocation);
            }    
            this.showOnScreen = showOnScreen;
            Task.Run(() => WorkingLoop());
        }
    }
}
