﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace WebServer
{
    class FileCache
    {
        //简单的缓存机制，读了就存，没有寿命，先这么用一用。
        //暂时不缓存非文本文件。
        private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>();

        public byte[] ReadFile(string filename)
        {
            if (!cache.ContainsKey(filename)) cache.Add(filename, File.ReadAllBytes(filename));
            return cache[filename];
        }
        
        public byte[] ReadTextFile(string filename) //暂且重新编码为 UTF-8，权宜之计，以后再改。
        {
            if (!cache.ContainsKey(filename)) cache.Add(filename, Encoding.UTF8.GetBytes(File.ReadAllText(filename)));
            return cache[filename];
        }

    }
}