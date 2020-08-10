using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace Nativa
{
    public class IniDeserializationException : Exception
    {
        public override string Message
        {
            get
            {
                return message;
            }
        }

        private string message = "INI 文件格式错误";
    }

    public class IniFile
    {
        public Dictionary<string, Dictionary<string, string>> Sections;
        private readonly string myPath;

        public IniFile(string path)
        {
            Sections = new Dictionary<string, Dictionary<string, string>>();
            if (File.Exists(path))
            {
                Dictionary<string, string> currentSectionDictionary = null;
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (line.Length == 0) continue; //跳过空行

                    if (line[0] == '[' &&
                        line[^1] == ']')
                    {
                        currentSectionDictionary = new Dictionary<string, string>();
                        Sections.Add(line[1..^1], currentSectionDictionary);
                    }
                    else
                    {
                        var equalFirstAppearIndex = line.IndexOf("=");
                        if (currentSectionDictionary != null)
                        {
                            currentSectionDictionary.Add(
                                line[..equalFirstAppearIndex],
                                line[(equalFirstAppearIndex + 1)..]
                                );
                        }
                        else
                        {
                            throw new IniDeserializationException();
                        }
                    }
                }
            }
            else
            {
                File.Create(path).Close(); //要释放新创建文件的句柄
            }
            myPath = path;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var section in Sections)
            {
                sb.Append("[");
                sb.Append(section.Key);
                sb.Append("]");
                sb.Append(Environment.NewLine);
                foreach (var pair in section.Value)
                {
                    sb.Append(pair.Key);
                    sb.Append("=");
                    sb.Append(pair.Value);
                    sb.Append(Environment.NewLine);
                }
                sb.Append(Environment.NewLine);
            }
            File.WriteAllText(myPath, sb.ToString());
        }
    }
}
