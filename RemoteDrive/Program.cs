using CommandLine;
using RemoteDrive.CommandLine;
using RemoteDrive.Framework;
using RemoteDrive.Framework.Providers;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;

namespace RemoteDrive
{
    class Program
    {
        const string AppMutexName = "W.RemoteDrive";
        const string AppPipeName = "W.RemoteDrive.Pipe";

        static ConcurrentDictionary<string, VirtualFileSystem> VirtualFileSystem { get; } = new ConcurrentDictionary<string, VirtualFileSystem>();
        static NamedPipeServer NamedPipeServer { get; set; }

        private static void OnNamedPipeConnected(PipeStream stream)
        {
            BinaryFormatter binaryFormatter = new BinaryFormatter();
            string[] args = binaryFormatter.Deserialize(stream) as string[];

            StreamWriter writer = new StreamWriter(stream)
            {
                AutoFlush = true
            };

            Type[] verbTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetCustomAttribute<VerbAttribute>() != null).ToArray();

            new Parser(
                settings =>
                {
                    settings.CaseSensitive = false;
                    settings.EnableDashDash = true;
                    settings.IgnoreUnknownArguments = false;
                    settings.HelpWriter = writer;
                })
                .ParseArguments(args, verbTypes)
                .WithParsed<MountOptions>(
                options =>
                {
                    string targetPath = Path.GetFullPath(options.VirtualizationRootPath);
                    if (!VirtualFileSystem.ContainsKey(targetPath))
                    {
                        VirtualFileProvider fileProvider = null;
                        if (options is DirectoryMountOptions directoryMountOptions)
                        {
                            fileProvider = new DirectoryProvider(directoryMountOptions.SourcePath, targetPath);
                        }

                        if (fileProvider != null)
                        {
                            VirtualFileSystem fileSystem = new VirtualFileSystem(fileProvider);
                            if (VirtualFileSystem.TryAdd(targetPath, fileSystem))
                            {
                                fileSystem.Start();
                            }
                        }
                    }
                });
        }

        private static void SendCommandLineArgs(string[] args, bool printResultsToConsole = true)
        {
            if (args == null || args.Length == 0)
            {
                return;
            }

            NamedPipeClient namedPipeClient = new NamedPipeClient(AppPipeName);
            if (namedPipeClient.Connect())
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(namedPipeClient.PipeStream, args);

                if (printResultsToConsole)
                {
                    StreamReader streamReader = new StreamReader(namedPipeClient.PipeStream);
                    while (streamReader.EndOfStream == false)
                    {
                        Console.WriteLine(streamReader.ReadLine());
                    }
                }
            }
        }

        private static bool OnConsoleCtrlHandler(CtrlType dwCtrlType)
        {
            switch (dwCtrlType)
            {
                case CtrlType.CTRL_C_EVENT:
                case CtrlType.CTRL_BREAK_EVENT:
                case CtrlType.CTRL_CLOSE_EVENT:
                case CtrlType.CTRL_LOGOFF_EVENT:
                case CtrlType.CTRL_SHUTDOWN_EVENT:
                    Environment.Exit(Environment.ExitCode);
                    break;
            }
            return false;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            if (NamedPipeServer != null)
            {
                NamedPipeServer.Stop();
                NamedPipeServer = null;
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Console.WriteLine(e.ToString());
        }

        static void Main(string[] args)
        {
            using (Mutex appMutex = new Mutex(true, AppMutexName, out bool createdNew))
            {
                if (createdNew)
                {
                    Kernal32.SetConsoleCtrlHandler(OnConsoleCtrlHandler, add: true);

                    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
                    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                    NamedPipeServer = new NamedPipeServer(AppPipeName);
                    NamedPipeServer.ClientConnectedCallback = OnNamedPipeConnected;
                    NamedPipeServer.Start();

                    SendCommandLineArgs(args, printResultsToConsole: false);

                    // wait for console window to close
                    new AutoResetEvent(false).WaitOne();
                }
                else
                {
                    SendCommandLineArgs(args);
                }
            }
        }
    }
}
