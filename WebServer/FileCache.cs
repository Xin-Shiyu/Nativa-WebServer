using Nativa;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Timers;

namespace WebServer
{
    internal class FileCache
    {
        private readonly ConcurrentDictionary<string, byte[]> cache = new ConcurrentDictionary<string, byte[]>();
        private readonly ConcurrentDictionary<string, int> lifeDict = new ConcurrentDictionary<string, int>();
        private readonly Timer timer;
        private readonly Logger logger;
        private readonly FileCacheSettings settings;
        private bool isClearing = false;

        private void ExtendLife(string filename)
        {
            if (!lifeDict.ContainsKey(filename))
            {
                lifeDict.TryAdd(filename, settings.initLife);
            }
            else if (lifeDict[filename] < settings.firstGenLifeMax)
            {
                lifeDict[filename] += settings.firstGenLifeGrowth;
            }
            else if (lifeDict[filename] < settings.secondGenLifeMax)
            {
                lifeDict[filename] += settings.secondGenLifeGrowth;
            }
            else if (lifeDict[filename] < settings.thirdGenLifeMax)
            {
                lifeDict[filename] += settings.thirdGenLifeGrowth;
            }
        }

        public byte[] ReadFile(string filename)
        {
            while (isClearing) { } //清理优先，新缓存让路
            ExtendLife(filename);
            if (!cache.ContainsKey(filename))
            {
                cache.TryAdd(filename, File.ReadAllBytes(filename));
            }

            return cache[filename];
        }

        [Obsolete]
        public byte[] ReadTextFile(string filename) //暂且重新编码为 UTF-8，权宜之计，以后再改。
        {
            while (isClearing) { }
            ExtendLife(filename);
            if (!cache.ContainsKey(filename))
            {
                cache.TryAdd(filename, Encoding.UTF8.GetBytes(File.ReadAllText(filename)));
            }

            return cache[filename];
        }

        public FileCache(Logger logger, FileCacheSettings settings)
        {
            timer = new Timer(settings.cacheClearingInterval);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
            this.logger = logger;
            this.settings = settings;
        }

        public int GetFileLife(string filename)
        {
            if (lifeDict.TryGetValue(filename, out int fileLife))
            {
                return fileLife * settings.cacheClearingInterval;
            }
            return 0;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            List<string> killList = new List<string>();
            foreach (KeyValuePair<string, int> pair in lifeDict)
            {
                if (pair.Value <= 0)
                {
                    killList.Add(pair.Key);
                }
                else
                {
                    --lifeDict[pair.Key];
                }
            }
            isClearing = true;
            killList.ForEach(
                file =>
                {
                    cache.Remove(file, out _);
                    lifeDict.Remove(file, out _);
                });
            GC.Collect();
            isClearing = false;
        }
    }
}
