using System;
using System.IO;

namespace LocalBotServer
{
    // config.env ao lado do .exe. Chave ausente usa o padrao documentado; valor invalido e erro.
    public class Config
    {
        public int Port = 8000;
        public bool Lan = false;
        public int MaxPayload = 65536;
        public int MaxPackets = 100;
        public int MaxTopicLength = 30;
        public int PingIntervalMs = 1000;

        public static Config Load(string path)
        {
            Config config = new Config();
            if (!File.Exists(path)) return config;

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;

                int separator = line.IndexOf('=');
                if (separator < 0) throw new FormatException("config.env: invalid line: " + line);

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim().Trim('"', '\'');

                switch (key)
                {
                    case "BOTSERVER_WS_PORT": config.Port = ParseInt(key, value, 1, 65535); break;
                    case "BOTSERVER_LAN": config.Lan = ParseInt(key, value, 0, 1) == 1; break;
                    case "BOTSERVER_WS_MAX_PAYLOAD": config.MaxPayload = ParseInt(key, value, 1, 1048576); break;
                    case "BOTSERVER_WS_MAX_PACKETS": config.MaxPackets = ParseInt(key, value, 1, 100000); break;
                    case "BOTSERVER_WS_MAX_TOPIC_LENGTH": config.MaxTopicLength = ParseInt(key, value, 1, 1000); break;
                    case "BOTSERVER_PING_INTERVAL_MS": config.PingIntervalMs = ParseInt(key, value, 100, 60000); break;
                    default: throw new FormatException("config.env: unknown key: " + key);
                }
            }
            return config;
        }

        private static int ParseInt(string key, string value, int min, int max)
        {
            int parsed;
            if (!int.TryParse(value, out parsed) || parsed < min || parsed > max)
                throw new FormatException(string.Format("config.env: {0} must be a number from {1} to {2}", key, min, max));
            return parsed;
        }
    }
}
