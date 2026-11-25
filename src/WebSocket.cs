using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace LocalBotServer
{
    public static class WebSocket
    {
        public static string AcceptKey(string key)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(key.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                return Convert.ToBase64String(hash);
            }
        }

        public static byte[] Frame(byte[] payload, byte opcode)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                stream.WriteByte((byte)(0x80 | (opcode & 0x0F)));
                if (payload.Length <= 125)
                {
                    stream.WriteByte((byte)payload.Length);
                }
                else if (payload.Length <= 65535)
                {
                    stream.WriteByte(126);
                    stream.WriteByte((byte)(payload.Length >> 8));
                    stream.WriteByte((byte)payload.Length);
                }
                else
                {
                    stream.WriteByte(127);
                    for (int i = 7; i >= 0; i--) stream.WriteByte((byte)((long)payload.Length >> (8 * i)));
                }
                stream.Write(payload, 0, payload.Length);
                return stream.ToArray();
            }
        }

        public static byte[] ReadExact(Stream stream, int count)
        {
            byte[] buffer = new byte[count];
            int read = 0;
            while (read < count)
            {
                int received = stream.Read(buffer, read, count - read);
                if (received <= 0) return null;
                read += received;
            }
            return buffer;
        }

        public static string ReadHeaderLine(Stream stream)
        {
            StringBuilder builder = new StringBuilder();
            int value;
            while ((value = stream.ReadByte()) != -1)
            {
                if (value == '\n') break;
                if (value != '\r') builder.Append((char)value);
                if (builder.Length > 2048) break;
            }
            return builder.ToString();
        }
    }

    public class Session
    {
        public readonly TcpClient Tcp;
        public readonly NetworkStream Stream;
        public readonly string Endpoint;
        public string Name = "";
        public string Room = "";
        public int Packets;
        public long PacketsWindow;
        public int MessageId;

        // Ping: so a resposta que devolve o seq mede. O DibraBot tambem manda pings proprios (sem seq)
        // neste servidor, e conta-los estragaria a medida; um vBot nunca devolve seq, e nele vale a
        // primeira resposta depois de cada ping.
        private readonly object pingGate = new object();
        private int pingSeq;
        private int pingSentAt;
        private bool pingWaiting;
        private bool echoesSeq;
        private int ping = -1;

        private readonly object sendGate = new object();
        private volatile bool alive = true;

        public Session(TcpClient tcp)
        {
            Tcp = tcp;
            Tcp.SendTimeout = 5000;
            Tcp.NoDelay = true;
            Stream = tcp.GetStream();
            Endpoint = tcp.Client.RemoteEndPoint.ToString();
        }

        public bool Alive { get { return alive; } }
        public int Ping { get { lock (pingGate) return ping; } }

        // Um ping sem resposta continua valendo ate ser respondido: senao uma latencia acima do
        // intervalo nunca seria medida.
        public int BeginPing()
        {
            lock (pingGate)
            {
                if (!pingWaiting)
                {
                    pingWaiting = true;
                    pingSeq++;
                    pingSentAt = Environment.TickCount;
                }
                return pingSeq;
            }
        }

        public void EndPing(object seq)
        {
            lock (pingGate)
            {
                if (seq != null) echoesSeq = true;
                else if (echoesSeq) return;
                if (!pingWaiting || (seq != null && !(seq is int && (int)seq == pingSeq))) return;
                pingWaiting = false;
                ping = Math.Max(0, unchecked(Environment.TickCount - pingSentAt));
            }
        }

        public void Send(string text)
        {
            SendFrame(Encoding.UTF8.GetBytes(text), 0x1);
        }

        public void SendFrame(byte[] payload, byte opcode)
        {
            if (!alive) return;
            byte[] frame = WebSocket.Frame(payload, opcode);
            lock (sendGate)
            {
                try { Stream.Write(frame, 0, frame.Length); }
                catch (Exception ex)
                {
                    if (!(ex is IOException || ex is ObjectDisposedException)) throw;
                    Close();
                }
            }
        }

        public void Close()
        {
            alive = false;
            Stream.Close();
            Tcp.Close();
        }
    }
}
