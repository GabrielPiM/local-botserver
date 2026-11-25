using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace LocalBotServer
{
    public enum LogKind { Info, Ok, Room, Join, Leave, Warn, Error }

    public class LogLine
    {
        public string Time { get; set; }
        public string Tag { get; set; }
        public string Text { get; set; }
        public string Color { get; set; }
    }

    // Escreve em logs/output.log e guarda as linhas novas ate a janela busca-las (Drain).
    public class Log
    {
        public const int MaxLines = 500;

        private readonly object gate = new object();
        private readonly Queue<LogLine> pending = new Queue<LogLine>();
        private readonly string filePath;
        private bool fileFailed;

        public Log(string baseDir)
        {
            filePath = Path.Combine(Path.Combine(baseDir, "logs"), "output.log");
        }

        public void Write(LogKind kind, string message)
        {
            string text = Clean(message);
            lock (gate)
            {
                DateTime now = DateTime.Now;
                Enqueue(now, kind, text);
                if (fileFailed) return;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    File.AppendAllText(filePath, string.Format("[{0:dd.MM HH:mm:ss}] {1} {2}\r\n", now, TagOf(kind), text));
                }
                catch (Exception ex)
                {
                    fileFailed = true;
                    Enqueue(now, LogKind.Error, "Log file disabled: " + ex.GetType().Name);
                }
            }
        }

        public List<LogLine> Drain()
        {
            lock (gate)
            {
                List<LogLine> lines = new List<LogLine>(pending);
                pending.Clear();
                return lines;
            }
        }

        private void Enqueue(DateTime now, LogKind kind, string text)
        {
            if (pending.Count >= MaxLines) pending.Dequeue();
            pending.Enqueue(new LogLine { Time = now.ToString("HH:mm:ss"), Tag = TagOf(kind), Color = ColorOf(kind), Text = text });
        }

        // Nomes e salas vem da rede: sem quebra de linha nem controle, ninguem forja linhas do log.
        private static string Clean(string message)
        {
            StringBuilder builder = new StringBuilder(message.Length);
            foreach (char c in message) builder.Append(char.IsControl(c) ? ' ' : c);
            return builder.ToString();
        }

        private static string TagOf(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Ok: return "OK";
                case LogKind.Room: return "ROOM";
                case LogKind.Join: return "JOIN";
                case LogKind.Leave: return "LEAVE";
                case LogKind.Warn: return "WARN";
                case LogKind.Error: return "ERROR";
                default: return "INFO";
            }
        }

        private static string ColorOf(LogKind kind)
        {
            switch (kind)
            {
                case LogKind.Ok:
                case LogKind.Join: return "#4ade80";
                case LogKind.Room: return "#67e8f9";
                case LogKind.Warn: return "#facc15";
                case LogKind.Leave:
                case LogKind.Error: return "#f87171";
                default: return "#ffffff";
            }
        }
    }
}
