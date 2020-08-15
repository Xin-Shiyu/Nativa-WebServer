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

        public int GetFileLength(string filename)
        {
            if (cache.ContainsKey(filename)) return cache[filename].Length;

            var info = new FileInfo(filename);
            return (int)info.Length; //不是很安全，以后再改吧
        }

        [Obsolete]
        public ReadOnlyMemory<byte> ReadPartialFile(string filename, int begin, ref int end, out int fullLength)
        {
            if (cache.ContainsKey(filename))
            {
                var memory = cache[filename].AsMemory();
                fullLength = memory.Length;
                return memory[begin..end];
            }
            else
            {
                fullLength = GetFileLength(filename);
                using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                stream.Seek(begin, SeekOrigin.Begin);
                if (end >= fullLength) end = fullLength - 1;
                var memory = new byte[end - begin + 1];
                stream.Read(memory);
                return memory;
            }
        }

        public HttpHelper.ResponseStream.ChunkingProvider
            ReadPartialFileChunked(string filename, int begin, ref int end, out int fullLength)
        {
            fullLength = GetFileLength(filename);
            if (end >= fullLength) end = fullLength - 1;
            var endCopy = end;
            IEnumerable<Memory<byte>> provider()
            {
                using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                stream.Seek(begin, SeekOrigin.Begin);
                var memory = new byte[1024 * 1024];
                for (int i = begin; i < endCopy; i += 1024 * 1024)
                {
                    stream.Read(memory);
                    if (i + 1024 * 1024 - 1 <= endCopy)
                    {
                        yield return memory;
                    }
                    else
                    {
                        yield return memory[..(endCopy - i + 1)];
                    }
                }
                yield break;
            }
            return provider;
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
            isClearing = false;
        }
    }
}
