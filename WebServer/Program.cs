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
        private static void Main()
        {
            var iniFile = new IniFile(Path.Combine(AppContext.BaseDirectory, "config.ini"));
            iniFile.SetDefault(
                "nws",
                new Dictionary<string, string>
                {
                    { "port", "80" },
                    { "keep_alive_max_delay", "4000" },
                    { "keep_alive_max_request_count", "100" },
                    { "compress_min_size", "1048576" },
                    { "log_save_location", "log" },
                    { "aggressive_chunking", "false" }
                });
            iniFile.SetDefault(
                "dph",
                new Dictionary<string, string>
                {
                    { "physical_base_path", iniFile.Sections["nws"].ContainsKey("root") ? iniFile.Sections["nws"]["root"] : "webroot"},
                    //对于旧的配置的兼容性措施
                    { "default_page", "index.html" }
                });
            if (iniFile.Sections["nws"].ContainsKey("root")) iniFile.Sections["nws"].Remove("root"); //兼容完了还是要升级的，不能留着误会
            iniFile.SetDefault(
                "dph_file_cache",
                new Dictionary<string, string>
                {
                    { "cache_clearing_interval", "60000" },
                    { "init_life", "2" },
                    { "first_gen_life_max", "60" },
                    { "second_gen_life_max", "120" },
                    { "third_gen_life_max", "240" },
                    { "first_gen_life_growth", "2" },
                    { "second_gen_life_growth", "4" },
                    { "third_gen_life_growth", "8" }
                });
            iniFile.SetDefault(
                "global",
                new Dictionary<string, string>
                {
                    { "show_log_on_screen", "true" }
                });
            iniFile.Save();
            GC.Collect(); //轻装上阵

            var logger = new Logger(
                Path.Combine(AppContext.BaseDirectory, iniFile.Sections["nws"]["log_save_location"]),
                bool.Parse(iniFile.Sections["global"]["show_log_on_screen"]));
            Server server = new Server(
                new ServerSettings
                {
                    port = int.Parse(iniFile.Sections["nws"]["port"]),
                    compressMinSize = int.Parse(iniFile.Sections["nws"]["compress_min_size"]),
                    keepAliveMaxDelay = int.Parse(iniFile.Sections["nws"]["keep_alive_max_delay"]),
                    keepAliveMaxRequestCount = int.Parse(iniFile.Sections["nws"]["keep_alive_max_request_count"]),
                    aggressiveChunking = bool.Parse(iniFile.Sections["nws"]["aggressive_chunking"])
                },
                logger,
                handler: new DefaultPageHandler(
                    new DefaultPageHandlerSettings
                    {
                        PhysicalBasePath = Path.Combine(AppContext.BaseDirectory, iniFile.Sections["dph"]["physical_base_path"]),
                        DefaultPage = iniFile.Sections["dph"]["default_page"]
                    }, 
                    logger,
                    new FileCacheSettings
                    {
                        cacheClearingInterval = int.Parse(iniFile.Sections["dph_file_cache"]["cache_clearing_interval"]),
                        initLife = int.Parse(iniFile.Sections["dph_file_cache"]["init_life"]),
                        firstGenLifeMax = int.Parse(iniFile.Sections["dph_file_cache"]["first_gen_life_max"]),
                        secondGenLifeMax = int.Parse(iniFile.Sections["dph_file_cache"]["second_gen_life_max"]),
                        thirdGenLifeMax = int.Parse(iniFile.Sections["dph_file_cache"]["third_gen_life_max"]),
                        firstGenLifeGrowth = int.Parse(iniFile.Sections["dph_file_cache"]["first_gen_life_growth"]),
                        secondGenLifeGrowth = int.Parse(iniFile.Sections["dph_file_cache"]["second_gen_life_growth"]),
                        thirdGenLifeGrowth = int.Parse(iniFile.Sections["dph_file_cache"]["third_gen_life_growth"])
                    })
                );
            server.Run();
        }
    }
}
