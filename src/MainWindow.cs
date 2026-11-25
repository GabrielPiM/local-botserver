using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LocalBotServer
{
    public class Row : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private string right;

        public string Name { get; set; }

        public string Right
        {
            get { return right; }
            set
            {
                if (right == value) return;
                right = value;
                if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs("Right"));
            }
        }
    }

    // Liga a janela (MainWindow.xaml embutido) ao servidor. A tela le o servidor a cada REFRESH_MS;
    // as threads do servidor nunca tocam a interface.
    public class MainWindow
    {
        private const int RefreshMs = 300;
        private const int ResizeBorder = 6;
        // Discord e YouTube: os mesmos links do bot. GitHub: o codigo-fonte deste programa.
        private const string DiscordUrl = "https://discord.gg/RkQ9nyPMBH";
        private const string YouTubeUrl = "https://www.youtube.com/@VivoDibra";
        private const string GitHubUrl = "https://github.com/GabrielPiM/local-botserver";

        public readonly Window Window;
        private readonly Server server;
        private readonly Log log;
        private readonly string baseDir;
        private readonly ObservableCollection<Row> rooms = new ObservableCollection<Row>();
        private readonly ObservableCollection<Row> players = new ObservableCollection<Row>();
        private readonly ObservableCollection<LogLine> lines = new ObservableCollection<LogLine>();
        private readonly ListBox roomList;
        private readonly ListBox logList;
        private readonly DispatcherTimer timer = new DispatcherTimer();

        public MainWindow(Server server, Log log, string baseDir)
        {
            this.server = server;
            this.log = log;
            this.baseDir = baseDir;

            using (Stream xaml = Resource("MainWindow.xaml")) Window = (Window)XamlReader.Load(xaml);
            BitmapFrame icon = LoadImage("icon.png");
            Window.Icon = icon;
            Find<Image>("TitleIcon").Source = icon;
            Find<Image>("DiscordIcon").Source = LoadImage("discord.png");
            Find<Image>("YouTubeIcon").Source = LoadImage("youtube.png");
            Find<Image>("GitHubIcon").Source = LoadImage("github.png");

            roomList = Find<ListBox>("RoomList");
            logList = Find<ListBox>("LogList");
            roomList.ItemsSource = rooms;
            Find<ListBox>("PlayerList").ItemsSource = players;
            logList.ItemsSource = lines;
            Find<TextBlock>("UrlText").Text = server.Url;

            Find<Border>("TitleBar").MouseLeftButtonDown += delegate { Window.DragMove(); };
            Find<Button>("MinimizeButton").Click += delegate { Window.WindowState = WindowState.Minimized; };
            Find<Button>("CloseButton").Click += delegate { Window.Close(); };
            Find<Button>("CopyButton").Click += delegate { Copy(); };
            Find<Button>("ClearButton").Click += delegate { lines.Clear(); };
            Find<Button>("FolderButton").Click += delegate { Process.Start("explorer.exe", "\"" + baseDir.TrimEnd('\\') + "\""); };
            Find<Button>("DiscordButton").Click += delegate { OpenLink(DiscordUrl); };
            Find<Button>("YouTubeButton").Click += delegate { OpenLink(YouTubeUrl); };
            Find<Button>("GitHubButton").Click += delegate { OpenLink(GitHubUrl); };
            Find<Button>("ToggleButton").Click += delegate { Toggle(); };
            roomList.SelectionChanged += delegate { Refresh(); };
            Window.SourceInitialized += delegate { HwndSource.FromHwnd(new WindowInteropHelper(Window).Handle).AddHook(WindowProc); };
            Window.Closed += delegate
            {
                timer.Stop();
                server.Stop();
            };

            timer.Interval = TimeSpan.FromMilliseconds(RefreshMs);
            timer.Tick += delegate { Refresh(); };
            timer.Start();

            log.Write(LogKind.Info, "made by: vivodibra - " + DiscordUrl);
            StartServer();
        }

        public void BringToFront()
        {
            if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
            Window.Activate();
            Window.Topmost = true;
            Window.Topmost = false;
        }

        private static Stream Resource(string name)
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            if (stream == null) throw new InvalidOperationException("Missing embedded resource: " + name);
            return stream;
        }

        private static BitmapFrame LoadImage(string name)
        {
            using (Stream stream = Resource(name))
                return BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        }

        private T Find<T>(string name) where T : class
        {
            T element = Window.FindName(name) as T;
            if (element == null) throw new InvalidOperationException("Missing window element: " + name);
            return element;
        }

        private void StartServer()
        {
            try { server.Start(); }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.AddressAlreadyInUse || ex.SocketErrorCode == SocketError.AccessDenied)
                    log.Write(LogKind.Error, "The port is already in use by another program: close it or change BOTSERVER_WS_PORT in config.env");
                else
                    log.Write(LogKind.Error, "Could not start: " + ex.SocketErrorCode);
            }
            Refresh();
        }

        private void Toggle()
        {
            if (server.Running)
            {
                server.Stop();
                Refresh();
            }
            else StartServer();
        }

        private void Copy()
        {
            // Outro programa pode estar segurando a area de transferencia.
            try
            {
                Clipboard.SetText(server.Url);
                log.Write(LogKind.Info, "Address copied");
            }
            catch (ExternalException) { log.Write(LogKind.Error, "Could not copy: the clipboard is in use by another program"); }
        }

        private void OpenLink(string url)
        {
            // Sem navegador padrao, ou com ele bloqueado, o Windows recusa a abertura.
            try { Process.Start(url); }
            catch (Win32Exception ex) { log.Write(LogKind.Error, "Could not open " + url + ": Windows error " + ex.NativeErrorCode); }
        }

        private void Refresh()
        {
            bool running = server.Running;
            Brush color = new SolidColorBrush(running ? Color.FromRgb(0x4a, 0xde, 0x80) : Color.FromRgb(0xf8, 0x71, 0x71));
            Find<Ellipse>("StatusDot").Fill = color;
            TextBlock status = Find<TextBlock>("StatusText");
            status.Foreground = color;
            status.Text = running ? "Online" : "Stopped";
            Find<Button>("ToggleButton").Content = running ? "Stop" : "Start";

            List<RoomInfo> snapshot = server.Snapshot();
            List<Row> roomRows = new List<Row>();
            int total = 0;
            foreach (RoomInfo room in snapshot)
            {
                roomRows.Add(new Row { Name = room.Name, Right = room.Players.Count.ToString() });
                total += room.Players.Count;
            }
            Sync(rooms, roomRows);
            if (roomList.SelectedItem == null && rooms.Count > 0) roomList.SelectedIndex = 0;

            Row selected = roomList.SelectedItem as Row;
            RoomInfo current = selected == null ? null : snapshot.Find(delegate(RoomInfo room) { return room.Name == selected.Name; });
            List<Row> playerRows = new List<Row>();
            if (current != null)
            {
                foreach (PlayerInfo player in current.Players)
                    playerRows.Add(new Row { Name = player.Name, Right = player.Ping < 0 ? "..." : player.Ping + " ms" });
            }
            Sync(players, playerRows);

            Find<TextBlock>("Summary").Text = Plural(snapshot.Count, "room", "rooms") + ", " + Plural(total, "player", "players");
            Find<TextBlock>("RoomCount").Text = snapshot.Count.ToString();
            Find<TextBlock>("RoomsEmpty").Visibility = snapshot.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            Find<TextBlock>("PlayersTitle").Text = current == null ? "Players" : "Players in \"" + current.Name + "\"";
            Find<TextBlock>("PlayerCount").Text = current == null ? "" : current.Players.Count.ToString();

            AppendLog();
        }

        private void AppendLog()
        {
            List<LogLine> added = log.Drain();
            if (added.Count == 0) return;
            foreach (LogLine line in added) lines.Add(line);
            while (lines.Count > Log.MaxLines) lines.RemoveAt(0);
            logList.ScrollIntoView(lines[lines.Count - 1]);
        }

        private static string Plural(int count, string one, string many)
        {
            return count + " " + (count == 1 ? one : many);
        }

        // Atualiza a lista no lugar: recriar os itens perderia a sala escolhida e a rolagem.
        private static void Sync(ObservableCollection<Row> target, List<Row> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                int found = -1;
                for (int j = i; j < target.Count && found < 0; j++)
                {
                    if (target[j].Name == source[i].Name) found = j;
                }
                if (found < 0) target.Insert(i, source[i]);
                else
                {
                    // Ambas as listas sao ordenadas: o que ficou antes do encontrado saiu.
                    for (; found > i; found--) target.RemoveAt(i);
                    target[i].Right = source[i].Right;
                }
            }
            while (target.Count > source.Count) target.RemoveAt(target.Count - 1);
        }

        // Janela sem moldura: devolve ao Windows as bordas de redimensionamento.
        private IntPtr WindowProc(IntPtr handle, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int HitTest = 0x0084;
            if (message != HitTest || Window.WindowState != WindowState.Normal) return IntPtr.Zero;

            int packed = unchecked((int)lParam.ToInt64());
            Point point = Window.PointFromScreen(new Point((short)(packed & 0xFFFF), (short)(packed >> 16)));
            int column = point.X < ResizeBorder ? 0 : point.X >= Window.ActualWidth - ResizeBorder ? 2 : 1;
            int row = point.Y < ResizeBorder ? 0 : point.Y >= Window.ActualHeight - ResizeBorder ? 2 : 1;
            if (column == 1 && row == 1) return IntPtr.Zero;

            handled = true;
            int[] codes = { 13, 12, 14, 10, 1, 11, 16, 15, 17 };
            return new IntPtr(codes[row * 3 + column]);
        }
    }
}
