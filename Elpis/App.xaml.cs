using Microsoft.Shell;
using NDesk.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using Util;


namespace Elpis
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        [STAThread]
        public static void Main()
        {
            // Unique app key (stable across builds)
            const string appKey = "ElpisInstance";

            if (SingleInstance<App>.InitializeAsFirstInstance(appKey))
            {
                var application = new App();
                application.InitializeComponent();   // loads App.xaml (StartupUri respected)
                application.Run();

                // cleanup when the first instance exits
                SingleInstance<App>.Cleanup();
            }
            // else: SingleInstance will signal the running instance via SignalExternalCommandLineArgs
        }

        public void Init()
        {
            this.InitializeComponent();
        }

        protected override void OnStartup(StartupEventArgs e)
        {

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
    Debug.WriteLine("UnhandledException: " + ex.ExceptionObject);
            DispatcherUnhandledException += (s, ex) =>
            {
                Debug.WriteLine("DispatcherUnhandledException: " + ex.Exception);
                ex.Handled = true; // don’t crash the UI thread
            };


            base.OnStartup(e);

            // Process the first instance's args
            //HandleCommandLine(Environment.GetCommandLineArgs());

            //if (Elpis.MainWindow._clo.ShowHelp)
            //   Current.Shutdown();

            var ok = HandleCommandLine(Environment.GetCommandLineArgs());
                if (!ok)
                    {
                Current.Shutdown();   // we already showed help
                        return;
                    }
        }


        protected override void OnExit(ExitEventArgs e)
        {
            //_instanceMutex?.ReleaseMutex();
           // _instanceMutex = null;
            base.OnExit(e);
        }

        private void ShowHelp(OptionSet optionSet, string msg = null)
        {
            using (var sw = new StringWriter())
            {
                optionSet.WriteOptionDescriptions(sw);
                var output = sw.ToString();
                if (msg != null)
                    output += "\r\n\r\n" + msg;

                MessageBox.Show(output, "Elpis Options");
            }
        }
        // ---- CLI handling ----
        public bool HandleCommandLine(IList<string> args)
        {
            var clo = new CommandLineOptions();
            var p = new OptionSet()
               .Add("c|config=", "a {CONFIG} file to load", v => clo.ConfigPath = v)
               .Add("h|?|help", "show this message and exit", v => clo.ShowHelp = v != null)
               .Add("playpause", "toggles playback", v => clo.TogglePlayPause = v != null)
               .Add("next", "skips current track", v => clo.SkipTrack = v != null)
               .Add("thumbsup", "like (thumbs up) current song", v => clo.DoThumbsUp = v != null)
               .Add("thumbsdown", "dislike (thumbs down) current", v => clo.DoThumbsDown = v != null)
               .Add("s|station=", "starts station \"{STATIONNAME}\"", v => clo.StationToLoad = v)
               .Add("exit|quit", "exits Elpis", v => clo.Exit = v != null);

            try { p.Parse(args); }
            catch (OptionException ex)
            {
                clo.ShowHelp = true;
                //Elpis.MainWindow.SetCommandLine(clo);
                ShowHelp(p, ex.Message);
            }

            Elpis.MainWindow.SetCommandLine(clo);

            if (clo.ShowHelp)
            {
                ShowHelp(p);
                //}
                //else if (Current?.MainWindow is Elpis.MainWindow mw)
                //{
                //    mw.DoCommandLine();
                return false;
            }

            return true;
        }


        #region ISingleInstanceApp Members
        // Called in the first instance when a 2nd instance launches.
        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // If no args, bring to front; otherwise assume “control only”
            if (args.Count <= 1 && Current?.MainWindow is Elpis.MainWindow mw)
            {
                mw.ShowInTaskbar = true;
                mw.Show();
                if (mw.WindowState == WindowState.Minimized) mw.WindowState = WindowState.Normal;
                mw.Activate();
                mw.Topmost = true; mw.Topmost = false; // z-order nudge
                mw.Focus();
            }

            return HandleCommandLine(args);
        }

        #endregion
    }
}