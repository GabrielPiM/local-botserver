using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace LocalBotServer
{
    public class PlayerInfo
    {
        public string Name;
        public int Ping;
    }

    public class RoomInfo
    {
        public string Name;
        public List<PlayerInfo> Players = new List<PlayerInfo>();
    }

    // Protocolo BotServer padrao (OTCv8/vBot): a sala e o `channel` do init, sem senha nem lobby.
    public class Server
    {
        private readonly Config config;
        private readonly Log log;
        private readonly object gate = new object();
        private readonly List<Session> sessions = new List<Session>();
        private readonly Dictionary<string, List<Session>> rooms = new Dictionary<string, List<Session>>();
        private TcpListener listener;
        private Timer pingTimer;

        public Server(Config config, Log log)
        {
            this.config = config;
            this.log = log;
        }

        public string Url { get { return "ws://127.0.0.1:" + config.Port + "/"; } }
        public bool Running { get { lock (gate) return listener != null; } }

        // Porta ocupada lanca SocketException; o servidor fica parado.
        public void Start()
        {
            TcpListener created = new TcpListener(config.Lan ? IPAddress.Any : IPAddress.Loopback, config.Port);
            created.ExclusiveAddressUse = true;
            created.Start();
            lock (gate)
            {
                listener = created;
                pingTimer = new Timer(PingAll, null, config.PingIntervalMs, config.PingIntervalMs);
            }

            Thread thread = new Thread(delegate() { AcceptLoop(created); });
            thread.IsBackground = true;
            thread.Start();

            log.Write(LogKind.Ok, "Server listening on " + Url);
            if (config.Lan) LogLanUrls();
        }

        public void Stop()
        {
            List<Session> open;
            lock (gate)
            {
                if (listener == null) return;
                pingTimer.Dispose();
                listener.Stop();
                listener = null;
                open = new List<Session>(sessions);
            }
            foreach (Session session in open) session.Close();
            log.Write(LogKind.Info, "Server stopped");
        }

        public List<RoomInfo> Snapshot()
        {
            List<RoomInfo> result = new List<RoomInfo>();
            lock (gate)
            {
                foreach (KeyValuePair<string, List<Session>> room in rooms)
                {
                    RoomInfo info = new RoomInfo { Name = room.Key };
                    foreach (Session session in room.Value)
                        info.Players.Add(new PlayerInfo { Name = session.Name, Ping = session.Ping });
                    info.Players.Sort(delegate(PlayerInfo a, PlayerInfo b) { return CompareNames(a.Name, b.Name); });
                    result.Add(info);
                }
            }
            result.Sort(delegate(RoomInfo a, RoomInfo b) { return CompareNames(a.Name, b.Name); });
            return result;
        }

        private static int CompareNames(string a, string b)
        {
            int order = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            return order != 0 ? order : string.CompareOrdinal(a, b);
        }

        private void LogLanUrls()
        {
            try
            {
                foreach (IPAddress ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        log.Write(LogKind.Info, string.Format("Local network: ws://{0}:{1}/", ip, config.Port));
                }
            }
            catch (SocketException ex)
            {
                log.Write(LogKind.Warn, "Could not list the network addresses: " + ex.SocketErrorCode);
            }
        }

        private void AcceptLoop(TcpListener owner)
        {
            while (true)
            {
                TcpClient tcp;
                try { tcp = owner.AcceptTcpClient(); }
                catch (Exception ex)
                {
                    lock (gate) { if (listener != owner) return; }
                    log.Write(LogKind.Error, "Could not accept a connection: " + ex.GetType().Name);
                    Thread.Sleep(200);
                    continue;
                }

                Thread thread = new Thread(delegate() { Handle(tcp, owner); });
                thread.IsBackground = true;
                thread.Start();
            }
        }

        private void Handle(TcpClient tcp, TcpListener owner)
        {
            Session session;
            try { session = new Session(tcp); }
            catch (Exception)
            {
                // O cliente caiu entre o accept e a leitura do endereco.
                tcp.Close();
                return;
            }

            lock (gate)
            {
                if (listener != owner) { session.Close(); return; }
                sessions.Add(session);
            }

            try
            {
                if (Handshake(session)) ReadLoop(session);
            }
            catch (Exception ex)
            {
                // Queda de conexao e normal; o resto e defeito e aparece no log.
                if (!(ex is IOException || ex is ObjectDisposedException))
                    log.Write(LogKind.Error, string.Format("Error with {0}: {1}", Label(session), ex.GetType().Name));
            }
            finally
            {
                session.Close();
                Remove(session);
            }
        }

        private static string Label(Session session)
        {
            return session.Name.Length > 0 ? session.Name : session.Endpoint;
        }

        private void ReadLoop(Session session)
        {
            while (session.Alive)
            {
                byte[] header = WebSocket.ReadExact(session.Stream, 2);
                if (header == null) return;

                int opcode = header[0] & 0x0F;
                bool masked = (header[1] & 0x80) != 0;
                ulong length = (ulong)(header[1] & 0x7F);

                int extended = length == 126 ? 2 : length == 127 ? 8 : 0;
                if (extended > 0)
                {
                    byte[] bytes = WebSocket.ReadExact(session.Stream, extended);
                    if (bytes == null) return;
                    length = 0;
                    foreach (byte b in bytes) length = (length << 8) | b;
                }

                if (length > (ulong)config.MaxPayload)
                {
                    log.Write(LogKind.Warn, string.Format("{0} sent a packet above {1} bytes", Label(session), config.MaxPayload));
                    return;
                }

                byte[] mask = null;
                if (masked && (mask = WebSocket.ReadExact(session.Stream, 4)) == null) return;

                byte[] data = WebSocket.ReadExact(session.Stream, (int)length);
                if (data == null) return;
                if (mask != null)
                {
                    for (int i = 0; i < data.Length; i++) data[i] ^= mask[i % 4];
                }

                if (opcode == 0x8) return;
                if (opcode == 0x9) session.SendFrame(data, 0xA);
                else if (opcode == 0x1 && !Process(session, Encoding.UTF8.GetString(data))) return;
            }
        }

        private bool Handshake(Session session)
        {
            string key = null;
            string request = "";
            string line;
            int count = 0;
            while ((line = WebSocket.ReadHeaderLine(session.Stream)).Length > 0)
            {
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = line.Substring(18).Trim();
                if (count == 0) request = line;
                if (++count > 100) return false;
            }
            if (count == 0) return false;

            if (string.IsNullOrEmpty(key))
            {
                // /status: o DibraBot pergunta se este servidor esta aberto antes de conectar.
                bool status = request.StartsWith("GET /status ", StringComparison.Ordinal);
                byte[] body = Encoding.UTF8.GetBytes(status ? StatusJson() : TestPage.Html(config.Port));
                WriteRaw(session, "HTTP/1.1 200 OK\r\nContent-Type: " + (status ? "application/json" : "text/html; charset=utf-8")
                    + "\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                session.Stream.Write(body, 0, body.Length);
                return false;
            }

            WriteRaw(session, "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + WebSocket.AcceptKey(key) + "\r\n\r\n");
            return true;
        }

        private string StatusJson()
        {
            int online = 0;
            lock (gate)
            {
                foreach (List<Session> members in rooms.Values) online += members.Count;
                return string.Format("{{\"server\":\"dibrabot-local\",\"online\":{0},\"rooms\":{1}}}", online, rooms.Count);
            }
        }

        private static void WriteRaw(Session session, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            session.Stream.Write(bytes, 0, bytes.Length);
        }

        // false encerra a conexao.
        private bool Process(Session session, string text)
        {
            JavaScriptSerializer json = new JavaScriptSerializer();
            Dictionary<string, object> message;
            try { message = json.Deserialize<Dictionary<string, object>>(text); }
            catch (Exception ex)
            {
                if (!(ex is ArgumentException || ex is InvalidOperationException)) throw;
                message = null;
            }
            if (message == null)
            {
                log.Write(LogKind.Warn, Label(session) + " sent invalid JSON");
                return false;
            }

            string type = Text(message, "type");

            if (session.Room.Length == 0)
            {
                string name = Text(message, "name");
                string room = Text(message, "channel");
                if (type != "init" || name.Length == 0 || room.Length == 0 || name.Length > 100 || room.Length > 100)
                {
                    log.Write(LogKind.Warn, Label(session) + " sent an invalid init");
                    return false;
                }
                return Join(session, name, room);
            }

            long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            if (session.PacketsWindow < now)
            {
                session.PacketsWindow = now + 1;
                session.Packets = 0;
            }
            if (++session.Packets > config.MaxPackets)
            {
                log.Write(LogKind.Warn, session.Name + " exceeded the packet limit");
                return false;
            }

            if (type == "ping")
            {
                object seq;
                message.TryGetValue("seq", out seq);
                session.EndPing(seq);
                return true;
            }

            string topic = Text(message, "topic");
            if (type != "message" || topic.Length == 0 || topic.Length > config.MaxTopicLength)
            {
                log.Write(LogKind.Warn, session.Name + " sent an invalid message");
                return false;
            }

            Dictionary<string, object> response = new Dictionary<string, object>();
            response["type"] = "message";
            response["id"] = ++session.MessageId;
            response["name"] = session.Name;
            response["topic"] = topic;

            if (topic == "list")
            {
                List<string> names = new List<string>();
                foreach (Session member in Members(session.Room)) names.Add(member.Name);
                response["message"] = names;
                session.Send(json.Serialize(response));
                return true;
            }

            object payload;
            message.TryGetValue("message", out payload);
            response["message"] = payload;
            string serialized = json.Serialize(response);
            foreach (Session member in Members(session.Room)) member.Send(serialized);
            return true;
        }

        private static string Text(Dictionary<string, object> message, string key)
        {
            object value;
            message.TryGetValue(key, out value);
            return value as string ?? "";
        }

        private List<Session> Members(string room)
        {
            lock (gate)
            {
                List<Session> members;
                return rooms.TryGetValue(room, out members) ? new List<Session>(members) : new List<Session>();
            }
        }

        private bool Join(Session session, string name, string room)
        {
            lock (gate)
            {
                // Depois do Stop a sessao ja foi fechada e nao pode entrar em sala nenhuma.
                if (!session.Alive) return false;
                session.Name = name;
                session.Room = room;
                List<Session> members;
                if (!rooms.TryGetValue(room, out members))
                {
                    rooms[room] = members = new List<Session>();
                    log.Write(LogKind.Room, string.Format("Room \"{0}\" created by {1}", room, name));
                }
                members.Add(session);
                log.Write(LogKind.Join, string.Format("{0} joined room \"{1}\"", name, room));
            }
            return true;
        }

        private void Remove(Session session)
        {
            lock (gate)
            {
                sessions.Remove(session);
                if (session.Room.Length == 0) return;
                List<Session> members = rooms[session.Room];
                members.Remove(session);
                log.Write(LogKind.Leave, string.Format("{0} left room \"{1}\"", session.Name, session.Room));
                if (members.Count > 0) return;
                rooms.Remove(session.Room);
                log.Write(LogKind.Room, string.Format("Room \"{0}\" closed", session.Room));
            }
        }

        private void PingAll(object state)
        {
            List<Session> joined = new List<Session>();
            lock (gate)
            {
                foreach (List<Session> members in rooms.Values) joined.AddRange(members);
            }
            foreach (Session session in joined)
            {
                int ping = Math.Max(0, session.Ping);
                session.Send(string.Format("{{\"type\":\"ping\",\"ping\":{0},\"seq\":{1}}}", ping, session.BeginPing()));
            }
        }
    }
}
