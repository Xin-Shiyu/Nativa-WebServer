# Nativa Webserver
A toy web server that only supports basic functionality.

Written in C# and runs on .NET Core 3.1.

To run on Windows, please install .NET Core SDK 3.1, and click the exe file;

To run on Linux and Mac, please install .NET Core SDK 3.1, and run `dotnet WebServer.dll` in terminal.


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
* `first_gen_life_growth` sets the life points a file gains (when it's accessed) in each generation. When a file's life exceeds the max of its generation, it will get into the next generation and be applied the growth rate of that generation. When a file's life exceeds the max of the third generation, it will no longer grow. Currently you cannot manually remove a file from the cache unless you restart the whole program.

Section "global" determines the global behaviors of the program, specifically, the logging module.
* `show_log_on_screen` can be `true` or `false`, determining whether the log will be shown on screen or just in files. When set to true, log will appear on screen not immediately but refreshed every five seconds in order not to affect the performance. (They are put into a queue and made into string every five second on a different thread) Logs will be saved every time its length reaches a certain level or when the process is being manually terminated, the filename representing the precise time when the log was saved.
* `log_save_location` literally sets where the log is saved.

Wish you'd have a good time with the program!

# 中文介绍
一个仅支持基本功能的玩具性质网页服务器。

使用 C# 编写，运行于 .NET Core 3.1。

要在 Windows 上运行，请先安装 .NET Core SDK 3.1，并双击打开 exe 文件。

要在 Linux 或者 Mac 上运行，请先安装 .NET Core SDK 3.1，并在终端运行 `dotnet WebServer.dll`。


# 关于 config.ini
首次运行时，本程序会在其所在目录创建 config.ini。用任意文本编辑器打开，可以看到几个部分。

“nws” 代表 Nativa 网页服务器，该部分是服务器模块本身的设置。
* `port` 决定服务器要侦听哪个端口。
* `keep_alive_max_delay` 决定服务器在每个 keep-alive 长连接中最多等待多少毫秒。
* `keep_alive_max_request_count` 决定服务器在每个 keep-alive 长连接中最多接受几个请求
* `compress_min_size` 决定文件开始被压缩的最小大小。
* `aggressive_chunking` 目前没有任何功能。将来会支持分块。

“dph” 代表默认页面处理模块。默认页面处理模块实际上是接口 IPageHandler 的默认实现，并不代表它只处理首页。目前该接口只要求实现获取页面的方法，简而言之就是不支持 POST 请求，将来会加入。下面是它的设置：
* `physical_base_path` 代表网站的实际物理路径。
* `default_page` 代表没有给定具体页面名称而只给了目录名的时候假定的页面名称。

“dph_file_cache” 顾名思义，是默认页面处理模块的缓存设置。下面解释各项设置的含义。
* `cache_clearing_interval` 以毫秒计数。它不代表过这段时间缓存就会被全部清空，而是文件缓存每次减少寿命的间隔周期。比如，如果它被设为 60000，那么每隔一分钟在缓存中的文件都会减少 1 的寿命。寿命为 0 的文件会被移出缓存。
* `init_life` 代表文件被缓存下来的时候所具有的初始寿命。每次访问文件，文件会被读入内存并缓存下来，并赋予给定的初始寿命。
* `first_gen_life_max` 代表第一代文件的最高寿命。另外两代（second，third）同理。
* `first_gen_life_growth` 代表每一代文件在被访问时增长的寿命。当一个文件的寿命超出了其所在代数的最大寿命，则它会进入下一代，按照下一代的寿命增长速率增长。寿命超过第三代最高寿命以后便不再增长。目前尚不能手动将文件从缓存中移出，除非重新启动整个程序。

"global" 代表程序的全局行为，此处特指日志模块。
* `show_log_on_screen` 可为 `true` 或 `false`，前者代表日志也会显示在屏幕上，后者代表仅储存在文件中。设为 `true` 时，日志并不会立刻在屏幕上出现，而是每隔五秒刷新，这样不至于影响性能。（每条日志都会进入队列，每隔五秒钟一个独立的线程会将其追加为字符串）日志总长达到一定长度或者进程被人为结束时日志会被存盘，文件名代表了存盘时的确切时刻。
* `log_save_location` 代表日志保存的位置。

祝玩得开心！
