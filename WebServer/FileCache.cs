using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.IO;
using System.Linq;
using System.Timers;
using System.ComponentModel;

namespace WebServer
{
    class FileCache
    {
        //简单的缓存机制
        //一个不是很好的寿命机制

        private readonly ConcurrentDictionary<string, byte[]> cache = new ConcurrentDictionary<string, byte[]>();
        private readonly ConcurrentDictionary<string, int> lifeDict = new ConcurrentDictionary<string, int>();
        private readonly Timer timer;

        private void ExtendLife(string filename)
        {
            if (!lifeDict.ContainsKey(filename)) lifeDict.TryAdd(filename, 2);
            else if (lifeDict[filename] < 60) lifeDict[filename] += 5;
            else if (lifeDict[filename] < 240) lifeDict[filename] += 15;
        }

        public byte[] ReadFile(string filename)
        {
            ExtendLife(filename);
            if (!cache.ContainsKey(filename)) cache.TryAdd(filename, File.ReadAllBytes(filename));
            return cache[filename];
        }

        public byte[] ReadTextFile(string filename) //暂且重新编码为 UTF-8，权宜之计，以后再改。
        {
            ExtendLife(filename);
            if (!cache.ContainsKey(filename)) cache.TryAdd(filename, Encoding.UTF8.GetBytes(File.ReadAllText(filename)));
            return cache[filename];
        }

        public FileCache()
        {
            timer = new Timer(60000); //每一分钟触发一次清理缓存
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var killList = new List<string>();
            foreach (var pair in lifeDict)
            {
                if (pair.Value == 1)
                {
                    killList.Add(pair.Key);
                }
                else
                {
                    --lifeDict[pair.Key];
                }
            }
            killList.ForEach(
                file =>
                {
                    cache.Remove(file, out _);
                    lifeDict.Remove(file, out _);
                });
        }
    }
}
