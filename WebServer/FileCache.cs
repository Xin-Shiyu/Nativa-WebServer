using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.IO;

namespace WebServer
{
    class FileCache
    {
        //简单的缓存机制，读了就存，没有寿命，先这么用一用。
        
        private readonly ConcurrentDictionary<string, byte[]> cache = new ConcurrentDictionary<string, byte[]>();

        public byte[] ReadFile(string filename)
        {
            if (!cache.ContainsKey(filename)) cache.TryAdd(filename, File.ReadAllBytes(filename));
            return cache[filename];
        }
        
        public byte[] ReadTextFile(string filename) //暂且重新编码为 UTF-8，权宜之计，以后再改。
        {
            if (!cache.ContainsKey(filename)) cache.TryAdd(filename, Encoding.UTF8.GetBytes(File.ReadAllText(filename)));
            return cache[filename];
        }

    }
}
