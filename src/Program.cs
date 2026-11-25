using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

[assembly: AssemblyTitle("Local BotServer")]
[assembly: AssemblyProduct("Local BotServer")]
[assembly: AssemblyCompany("DibraBot")]
[assembly: AssemblyVersion("1.0.0.0")]

namespace LocalBotServer
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            try { Run(); }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Run()
        {
            // Tudo em ingles, inclusive as mensagens do proprio .NET.
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Config config = Config.Load(Path.Combine(baseDir, "config.env"));

            // Uma instancia por porta: abrir de novo so traz a janela existente para frente.
            bool created;
            using (EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\LocalBotServer." + config.Port, out created))
            {
                if (!created)
                {
                    show.Set();
                    return;
                }

                Log log = new Log(baseDir);
                Application app = new Application();
                app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                {
                    log.Write(LogKind.Error, e.Exception.ToString());
                    e.Handled = true;
                    Fail(e.Exception);
                    app.Shutdown(1);
                };

                MainWindow main = new MainWindow(new Server(config, log), log, baseDir);
                RegisteredWaitHandle wait = ThreadPool.RegisterWaitForSingleObject(show, delegate
                {
                    app.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new ThreadStart(main.BringToFront));
                }, null, -1, false);
                app.Run(main.Window);
                wait.Unregister(null);
            }
        }

        private static void Fail(Exception ex)
        {
            MessageBox.Show(ex.Message, "Local BotServer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
