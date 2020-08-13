# Nativa Webserver
A toy web server that only supports basic functionality.

Written in C# and runs on .NET Core 3.1.

To run on Windows, please install .NET Core SDK 3.1, and click the exe file;

To run on Linux and Mac, please install .NET Core SDK 3.1, and run "dotnet WebServer.dll" in terminal.


# About config.ini
When the program is first ran, a file named "config.ini" will automatically appear in the directory where the executable exists. You can open it with the text editor you like, and you will see several sections.

Section "nws" stands for Nativa WebServer, and it's the settings for the server part:
* `port` sets the port that the server will be listening on.
* `keep_alive_max_delay` sets the longest time it will wait in a keep-alive connection in milliseconds.
* `keep_alive_max_request_count` sets the maximum number of requests it will response to in a keep-alive connection.
* `compress_min_size` sets the minimum size where a file will be compressed before sent.
* `aggressive_chunking` currently serves no functionality. Chunking will be implemented soon.

Section "dph" stands for Default Page Handler. It does not only handles the default page. In fact, it is the default implementation of the interface IPageHandler, which has only one method, GetPage. Other methods will be added in the future. Below is its settings:
* `physical_base_path` sets the actual path it will find files in.
* `default_page` sets the default page name when a directory name is given but not the specific page name.

Section "dph_file_cache" does exactly what it seems to do. Here is the explanation of its settings:
* `cache_clearing_interval` , in milliseconds, doesn't mean the cache will be cleaned after this period of time. In fact, it stands for the interval of the cycle during which the life of a cached file decreases. For example, if you set it to 60000, then each cached file will be decreased 1 point in life every minute. When its life becomes zero, it will be removed from the cache.
* `init_life` sets the life points when a file is initially cached. When accessed, a file will be read into the memory and cached, with the given life.
* `first_gen_life_max` sets the maximum life of the first of the three generations. Same for the other two.
* `first_gen_life_growth` sets the life points a file gains in each generation. When a file's life exceeds the max of its generation, it will get into the next generation and be applied the growth rate of that generation. When a file's life exceeds the max of the third generation, it will no longer grow. Currently you cannot manually remove a file from the cache unless you restart the whole program.

Section "global" determines the global behaviors of the program, specifically, the logging module.
* `show_log_on_screen` can be `true` or `false`, determining whether the log will be shown on screen or just in files. When set to true, log will appear on screen not immediately but refreshed every five seconds in order not to affect the performance. Logs will be saved every time its length reaches a certain level or when the process is being manually terminated.
* `log_save_location` literally sets where the log is saved.

Wish you'd have a good time with the program!
