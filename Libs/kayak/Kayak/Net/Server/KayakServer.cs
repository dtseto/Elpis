using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Kayak
{
    class DefaultKayakServer : IServer
    {
        IServerDelegate del;

        IScheduler scheduler;
        KayakServerState state;
        Socket listener;

        internal DefaultKayakServer(IServerDelegate del, IScheduler scheduler)
        {
            if (del == null)
                throw new ArgumentNullException("del");

            if (scheduler == null)
                throw new ArgumentNullException("scheduler");

            this.del = del;
            this.scheduler = scheduler;
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            state = new KayakServerState();
        }

        public void Dispose()
        {
            state.SetDisposed();

            if (listener != null)
            {
                listener.Dispose();
            }
        }

        public IDisposable Listen(IPEndPoint ep)
        {
            if (ep == null) throw new ArgumentNullException(nameof(ep));
            state.SetListening();

            Debug.WriteLine($"KayakServer will bind to {ep}");

            // Make sure the socket was created as TCP:
            // listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // ---- Set options BEFORE Bind ----
            // EITHER prevent other processes from binding the same port…
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);

            // …OR allow quick restarts on the same port during TIME_WAIT (choose ONE of these patterns)
            // listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // Optional: fast close sends RST; use with care
            listener.LingerState = new LingerOption(true, 0);

            // ---- Bind & listen ----
            listener.Bind(ep);
            listener.Listen(100); // real backlog; don’t call Listen twice

            Debug.WriteLine($"KayakServer bound to {ep}");

            // DO NOT set Receive/SendTimeout on the listening socket; apply to accepted sockets instead.
            // If you need timeouts, do it when you accept:
            //   socket.ReceiveTimeout = 10000; 
            //   socket.SendTimeout = 10000;

            AcceptNext();
            return new Disposable(() => Close());
        }

        void Close()
        {
            var closed = state.SetClosing();
            
            Debug.WriteLine("Closing listening socket.");
            listener.Close();

            if (closed)
                RaiseOnClose();
        }

        internal void SocketClosed(DefaultKayakSocket socket)
        {
            //Debug.WriteLine("Connection " + socket.id + ": closed (" + connections + " active connections)");
            if (state.DecrementConnections())
                RaiseOnClose();
        }

        void RaiseOnClose()
        {
            del.OnClose(this);
        }

        void AcceptNext()
        {
            try
            {
                Debug.WriteLine("KayakServer: accepting connection");
                listener.BeginAccept(iasr =>
                {
                    Debug.WriteLine("KayakServer: accepted connection callback");
                    Socket socket = null;
                    Exception error = null;
                    try
                    {
                        socket = listener.EndAccept(iasr);
                        AcceptNext();
                    }
                    catch (Exception e)
                    {
                        error = e;
                    }

                    if (error is ObjectDisposedException)
                        return;

                    scheduler.Post(() =>
                    {
                        Debug.WriteLine("KayakServer: accepted connection");
                        if (error != null)
                            HandleAcceptError(error);

                        var s = new DefaultKayakSocket(new SocketWrapper(socket), this.scheduler);
                        state.IncrementConnections();

                        var socketDelegate = del.OnConnection(this, s);
                        s.del = socketDelegate;
                        s.BeginRead();
                    });

                }, null);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                HandleAcceptError(e);
            }
        }

        void HandleAcceptError(Exception e)
        {
            state.SetError();

            try
            {
                listener.Close();
            }
            catch { }

            Debug.WriteLine("Error attempting to accept connection.");
            Console.Error.WriteStackTrace(e);

            RaiseOnClose();
        }
    }
}
