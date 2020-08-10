using System;

namespace Nativa
{
    public class Logger
    {
        private enum LogType
        {
            INFO,
            ERR,
        }
        //目前还没有记录日志的功能，以后再实现
        public string SaveLocation;
        public bool ShowOnScreen = true;

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
            if (ShowOnScreen)
            {
                Console.WriteLine("[{0}] {1}",
                    logType switch
                    {
                        LogType.INFO => "消息",
                        LogType.ERR => "错误",
                        _ => "未知消息类型"
                    },
                    logContent);
            }
        }
    }
}
