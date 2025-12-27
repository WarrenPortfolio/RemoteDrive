using System;
using System.Collections.Concurrent;
using System.IO.Pipes;

namespace RemoteDrive
{
    internal class NamedPipeClient
    {
        public string PipeName { get; private set; }
        public PipeStream PipeStream { get; private set; } = null;

        private NamedPipeClientStream NamedPipeClientStream { get; set; } = null;

        public NamedPipeClient(string pipeName)
        {
            PipeName = pipeName;
        }

        public bool Connect()
        {
            NamedPipeClientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut);
            try
            {
                NamedPipeClientStream.Connect((int)TimeSpan.FromSeconds(0.1).TotalMilliseconds);
                if (NamedPipeClientStream.IsConnected)
                {
                    PipeStream = NamedPipeClientStream;
                    return true;
                }
            }
            catch { }

            return false;
        }
    }

    internal class NamedPipeServer
    {
        public Action<PipeStream> ClientConnectedCallback { get; set; }
        public string PipeName { get; private set; }

        private ConcurrentDictionary<Guid, NamedPipeServerStream> NamedPipeServerStreams { get; } = new ConcurrentDictionary<Guid, NamedPipeServerStream>();

        public NamedPipeServer(string pipeName)
        {
            PipeName = pipeName;
        }

        public void Start()
        {
            int initialStreamCount = 3;
            for (int i = 0; i < initialStreamCount; i++)
            {
                StartNamedPipeServer();
            }
        }

        public void Stop()
        {
            foreach (var stream in NamedPipeServerStreams.Values)
            {
                try
                {
                    if (stream.IsConnected)
                    {
                        stream.Disconnect();
                    }
                }
                finally
                {
                    stream.Dispose();
                }
            }
            NamedPipeServerStreams.Clear();
        }

        private void StartNamedPipeServer()
        {
            NamedPipeServerStream stream = new NamedPipeServerStream(PipeName, PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            Guid key = Guid.NewGuid();
            if (NamedPipeServerStreams.TryAdd(key, stream))
            {
                stream.BeginWaitForConnection(OnNamedPipeConnected, key);
            }
        }

        private void OnNamedPipeConnected(IAsyncResult result)
        {
            StartNamedPipeServer();

            Guid key = (Guid)result.AsyncState;
            if (NamedPipeServerStreams.TryRemove(key, out var stream))
            {
                stream.EndWaitForConnection(result);

                try
                {
                    ClientConnectedCallback(stream);
                }
                finally
                {
                    stream.Dispose();
                }
            }
        }
    }
}
