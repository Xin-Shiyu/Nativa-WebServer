using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Nativa
{
    public class Logger
    {
        private enum LogType
        {
            INFO,
            ERR,
        }
        //仿佛不是很线程安全
        private string saveLocation;
        private bool showOnScreen = true;
        private StringBuilder logBuffer = new StringBuilder();

        public void Log(string logContent)
        {
            LogWithType(logContent, LogType.INFO);
        }

        public void Err(string logContent)
        {
            LogWithType(logContent, LogType.ERR);
        }

        private void LogWithType(string logContent, LogType logType)
        {
            var line = string.Format(
                "{0}\t{1}\t{2}",
                DateTime.Now,
                logType switch
                {
                    LogType.INFO => "消息",
                    LogType.ERR => "错误",
                    _ => "未知消息类型"
                },
                logContent
                );
            logBuffer.AppendLine(line);
            if (showOnScreen)
            {
                Console.WriteLine(line);
            }
            if (logBuffer.Length >= 1000)
            {
                ForceSave();
                logBuffer.Clear();
            }
        }

        public void ForceSave()
        {
            using var log = File.Create(Path.Combine(saveLocation, string.Format("{0}.log", DateTime.Now.ToString("yyyyMMddHHmmssffff"))));
            log.Write(Encoding.UTF8.GetBytes(logBuffer.ToString()));
        }

        public Logger(string saveLocation, bool showOnScreen)
        {
            this.saveLocation = saveLocation;
            if (!Directory.Exists(saveLocation))
            {
                Directory.CreateDirectory(saveLocation);
            }    
            this.showOnScreen = showOnScreen;
        }
    }
}
