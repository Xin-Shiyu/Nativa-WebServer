//玩具性质，练手用的
using Nativa;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.IO;

namespace WebServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var iniFile = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
            if (!iniFile.Sections.ContainsKey("nws"))
            {
                Console.WriteLine("未能找到配置文件 config.ini 或配置文件为空，创建默认配置。");
                iniFile.Sections.Add(
                    "nws",
                    new Dictionary<string, string>
                    {
                        { "port", "80" },
                        { "root", "WebRoot" },
                        { "keep_alive_max_delay", "800" },
                        { "compress_min_size", "1048576" },
                        { "log_save_location", "log" },
                    });
                iniFile.Save();
            }
            Server server = new Server(
                port: int.Parse(iniFile.Sections["nws"]["port"]),
                compressMinSize: int.Parse(iniFile.Sections["nws"]["compress_min_size"]),
                keepAliveMaxDelay: int.Parse(iniFile.Sections["nws"]["compress_min_size"]),
                handler: new DefaultPageHandler(iniFile.Sections["nws"]["root"]),
                logSaveLocation: Path.Combine(AppContext.BaseDirectory, iniFile.Sections["nws"]["log_save_location"])
                );
            server.Run();
        }
    }
}
