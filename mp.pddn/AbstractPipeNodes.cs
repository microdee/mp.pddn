using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using md.stdl.Interfaces;
using NamedPipeWrapper;
using VVVV.PluginInterfaces.V2;

namespace mp.pddn
{
    public delegate void ProxyEventHandler(EventArgs args);

    public interface IPipeClientWrapper<TR, in TW, in TUnwrap, out TWrap> : IMainlooping
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    {
        LinkedList<TR> AccumulatedData { get; }
        TR LastData { get; set; }
        string LastError { get; set; }
        bool MessageReceivedBang { get; }
        bool IsConnected { get; }
        int Id { get; }
        string Name { get; }
        Func<TW, TWrap> WrapperMethod { get; }
        Func<TUnwrap, TR> UnwrapperMethod { get; }

        void Send(TW data);
    }

    public class ServerSidePipeWrapper<TR, TW, TUnwrap, TWrap> : IPipeClientWrapper<TR, TW, TUnwrap, TWrap>
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    {
        public NamedPipeConnection<TUnwrap, TWrap> Connection
        {
            get => _connection;
            set
            {
                if(_connection != null)
                {
                    _connection.ReceiveMessage -= OnMessage;
                    _connection.Error -= OnError;
                }
                _connection = value;
                Subscribe();
            }
        }

        public LinkedList<TR> AccumulatedData { get; } = new LinkedList<TR>();
        public TR LastData { get; set; }

        public string LastError { get; set; }
        
        public bool MessageReceivedBang => FlatMessageReceived.Bang;

        public bool IsConnected => _connection.IsConnected;
        public int Id => _connection.Id;
        public string Name => _connection.Name;
        public Func<TW, TWrap> WrapperMethod { get; }
        public Func<TUnwrap, TR> UnwrapperMethod { get; }

        public EventFlattener<ProxyEventHandler, EventArgs> FlatMessageReceived;
        public EventFlattener<ProxyEventHandler, EventArgs> FlatError;
        private Queue<TR> _accumulatedData { get; } = new Queue<TR>();
        private NamedPipeConnection<TUnwrap, TWrap> _connection;

        public void Send(TW data)
        {
            _connection.PushMessage(WrapperMethod(data));
        }

        public ServerSidePipeWrapper(NamedPipeConnection<TUnwrap, TWrap> conn, Func<TW, TWrap> wrapper, Func<TUnwrap, TR> unwrapper)
        {
            WrapperMethod = wrapper;
            UnwrapperMethod = unwrapper;
            _connection = conn;
            Subscribe();
        }

        private void Subscribe()
        {
            _connection.ReceiveMessage += OnMessage;
            _connection.Error += OnError;
            FlatMessageReceived = new EventFlattener<ProxyEventHandler, EventArgs>(
                handler => OnMessageProxy += handler,
                handler => OnMessageProxy -= handler
            );
            FlatError = new EventFlattener<ProxyEventHandler, EventArgs>(
                handler => OnErrorProxy += handler,
                handler => OnErrorProxy -= handler
            );
        }

        private void OnError(NamedPipeConnection<TUnwrap, TWrap> namedPipeConnection, Exception exception)
        {
            LastError = exception.Message;
            OnErrorProxy?.Invoke(EventArgs.Empty);
        }

        private void OnMessage(NamedPipeConnection<TUnwrap, TWrap> connection, TUnwrap message)
        {
            var unwrapped = UnwrapperMethod(message);
            _accumulatedData.Enqueue(unwrapped);
            LastData = unwrapped;
            OnMessageProxy?.Invoke(EventArgs.Empty);
        }

        public event ProxyEventHandler OnMessageProxy;
        public event ProxyEventHandler OnErrorProxy;

        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);
            FlatMessageReceived.Mainloop(0);
            FlatError.Mainloop(0);
            AccumulatedData.Clear();
            lock (_accumulatedData)
            {
                while (_accumulatedData.Count > 0)
                {
                    AccumulatedData.AddLast(_accumulatedData.Dequeue());
                }
            }
            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler OnMainLoopBegin;
        public event EventHandler OnMainLoopEnd;
    }

    public abstract class PipeServerNode<TR, TW, TUnwrap, TWrap> : IPluginEvaluate
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    {
        [Import]
        public IHDEHost Host;

        private readonly Dictionary<int, ServerSidePipeWrapper<TR, TW, TUnwrap, TWrap>> _clients = new Dictionary<int, ServerSidePipeWrapper<TR, TW, TUnwrap, TWrap>>();
        private Server<TUnwrap, TWrap> _server;
        
        public EventFlattener<ConnectionEventHandler<TUnwrap, TWrap>, NamedPipeConnection<TUnwrap, TWrap>> ClientConnected;
        public EventFlattener<ConnectionEventHandler<TUnwrap, TWrap>, NamedPipeConnection<TUnwrap, TWrap>> ClientDisconnected;
        public EventFlattener<PipeExceptionEventHandler, Exception> Error;

        protected abstract TWrap Wrap(TW data);
        protected abstract TR Unwrap(TUnwrap message);

        [Input("Pipe Name")]
        public IDiffSpread<string> FPipeName;
        [Input("Data")]
        public ISpread<TW> FData;
        [Input("Welcome Data")]
        public ISpread<TW> FDataWelcome;
        [Input("Broadcast", IsBang = true)]
        public ISpread<bool> FBroadcast;

        [Output("Clients")]
        public ISpread<ServerSidePipeWrapper<TR, TW, TUnwrap, TWrap>> FClients;
        [Output("Client Connected", IsBang = true)]
        public ISpread<bool> FConnected;
        [Output("Client Disconnected", IsBang = true)]
        public ISpread<bool> FDisconnected;
        [Output("Error", IsBang = true)]
        public ISpread<bool> FError;
        [Output("Last Error")]
        public ISpread<string> FErrorMessage;
        [Output("Last Disconnected")]
        public ISpread<ServerSidePipeWrapper<TR, TW, TUnwrap, TWrap>> FLastClient;

        public void ObserveEvents()
        {
            ClientConnected = new EventFlattener<ConnectionEventHandler<TUnwrap, TWrap>, NamedPipeConnection<TUnwrap, TWrap>>(
                    handler => _server.ClientConnected += handler,
                    handler => _server.ClientConnected -= handler
                );

            ClientDisconnected = new EventFlattener<ConnectionEventHandler<TUnwrap, TWrap>, NamedPipeConnection<TUnwrap, TWrap>>(
                    handler => _server.ClientDisconnected += handler,
                    handler => _server.ClientDisconnected -= handler
                );

            Error = new EventFlattener<PipeExceptionEventHandler, Exception>(
                    handler => _server.Error += handler,
                    handler => _server.Error -= handler
                );
        }

        public void Evaluate(int SpreadMax)
        {
            if (FPipeName.IsChanged)
            {
                _server?.Stop();
                if (FPipeName.SliceCount > 0 && !string.IsNullOrWhiteSpace(FPipeName[0]))
                {
                    _server = new Server<TUnwrap, TWrap>(FPipeName[0], null);
                    _server.ClientConnected += connection =>
                    {
                        lock (_clients)
                        {
                            if (_clients.ContainsKey(connection.Id))
                            {
                                var clwrap = _clients[connection.Id];
                                clwrap.Connection = connection;
                            }
                            else
                            {
                                var clwrap = new ServerSidePipeWrapper<TR, TW, TUnwrap, TWrap>(connection, Wrap, Unwrap);
                                _clients.Add(connection.Id, clwrap);
                            }
                        }

                        foreach (var data in FDataWelcome)
                        {
                            if (data != null) connection.PushMessage(Wrap(data));
                        }
                    };
                    _server.ClientDisconnected += connection =>
                    {
                        lock (_clients)
                        {
                            if (_clients.ContainsKey(connection.Id))
                            {
                                FLastClient[0] = _clients[connection.Id];
                                _clients.Remove(connection.Id);
                            }
                        }
                    };
                    _server.Error += exception =>
                    {
                        FErrorMessage[0] = exception.Message;
                    };
                    ObserveEvents();
                    _server.Start();
                }
            }

            for (int i = 0; i < SpreadUtils.SpreadMax(FData, FBroadcast); i++)
            {
                if (FBroadcast[i])
                {
                    if(FData[i] != null) _server.PushMessage(Wrap(FData[i]));
                }
            }

            lock (_clients)
            {
                FClients.SliceCount = _clients.Count;
                int ii = 0;
                foreach (var client in _clients.Values)
                {
                    client.Mainloop(0);
                    FClients[ii] = client;
                    ii++;
                }
            }

            ClientConnected?.Mainloop(0);
            FConnected[0] = ClientConnected?.Bang ?? false;
            ClientDisconnected?.Mainloop(0);
            FDisconnected[0] = ClientDisconnected?.Bang ?? false;
            Error?.Mainloop(0);
            FError[0] = Error?.Bang ?? false;
        }
    }

    public class ClientSidePipeWrapper<TR, TW, TUnwrap, TWrap> : IPipeClientWrapper<TR, TW, TUnwrap, TWrap>
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    {
        public NamedPipeClient<TUnwrap, TWrap> Client { get; }

        public NamedPipeConnection<TUnwrap, TWrap> Connection { get; private set; }

        public ClientSidePipeWrapper(NamedPipeClient<TUnwrap, TWrap> client, string name, Func<TW, TWrap> wrapper, Func<TUnwrap, TR> unwrapper)
        {
            WrapperMethod = wrapper;
            UnwrapperMethod = unwrapper;
            Client = client;
            Name = name;
            SubscribeClient();
        }

        public LinkedList<TR> AccumulatedData { get; } = new LinkedList<TR>();
        public TR LastData { get; set; }

        public string LastError { get; set; }

        public bool MessageReceivedBang => FlatMessageReceived.Bang;

        public bool IsConnected => Connection?.IsConnected ?? true;

        public int Id => Connection?.Id ?? -1;

        public string Name { get; }
        public Func<TW, TWrap> WrapperMethod { get; }
        public Func<TUnwrap, TR> UnwrapperMethod { get; }

        public EventFlattener<ProxyEventHandler, EventArgs> FlatMessageReceived;
        public EventFlattener<ProxyEventHandler, EventArgs> FlatError;
        private Queue<TR> _accumulatedData { get; } = new Queue<TR>();

        public void Send(TW data)
        {
            Client.PushMessage(WrapperMethod(data));
        }

        private void SubscribeClient()
        {
            Client.ServerMessage += OnMessage;
            FlatMessageReceived = new EventFlattener<ProxyEventHandler, EventArgs>(
                handler => OnMessageProxy += handler,
                handler => OnMessageProxy -= handler
            );
        }

        private void SubscribeConn()
        {
            Connection.Error += OnError;
            FlatError = new EventFlattener<ProxyEventHandler, EventArgs>(
                handler => OnErrorProxy += handler,
                handler => OnErrorProxy -= handler
            );
        }

        private void OnError(NamedPipeConnection<TUnwrap, TWrap> namedPipeConnection, Exception exception)
        {
            LastError = exception.Message;
            OnErrorProxy?.Invoke(EventArgs.Empty);
        }

        private void OnMessage(NamedPipeConnection<TUnwrap, TWrap> connection, TUnwrap message)
        {
            var unwrapped = UnwrapperMethod(message);
            if (Connection == null)
            {
                Connection = connection;
                SubscribeConn();
            }
            _accumulatedData.Enqueue(unwrapped);
            LastData = unwrapped;
            OnMessageProxy?.Invoke(EventArgs.Empty);
        }

        public event ProxyEventHandler OnMessageProxy;
        public event ProxyEventHandler OnErrorProxy;

        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);
            FlatMessageReceived?.Mainloop(0);
            FlatError?.Mainloop(0);
            AccumulatedData.Clear();
            lock (_accumulatedData)
            {
                while (_accumulatedData.Count > 0)
                {
                    AccumulatedData.AddLast(_accumulatedData.Dequeue());
                }
            }
            OnMainLoopEnd?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler OnMainLoopBegin;
        public event EventHandler OnMainLoopEnd;
    }

    public abstract class PipeClientNode<TR, TW, TUnwrap, TWrap> : IPluginEvaluate
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    {
        [Import]
        public IHDEHost Host;

        private readonly Dictionary<string, ClientSidePipeWrapper<TR, TW, TUnwrap, TWrap>> _clients = new Dictionary<string, ClientSidePipeWrapper<TR, TW, TUnwrap, TWrap>>();

        [Input("Pipe Name")]
        public IDiffSpread<string> FPipeName;
        [Input("Connection Timeout Seconds")]
        public ISpread<double> FTimeOutSec;
        [Input("Data")]
        public ISpread<ISpread<TW>> FData;
        [Input("Send", IsBang = true)]
        public ISpread<bool> FBroadcast;

        [Output("Clients")]
        public ISpread<ClientSidePipeWrapper<TR, TW, TUnwrap, TWrap>> FClients;

        protected abstract TWrap Wrap(TW data);
        protected abstract TR Unwrap(TUnwrap message);

        public void Evaluate(int SpreadMax)
        {
            FClients.SliceCount = FPipeName.SliceCount;
            if (!FPipeName.IsChanged || FPipeName.SliceCount <= 0 || string.IsNullOrWhiteSpace(FPipeName[0])) return;
            for (int i = 0; i < FPipeName.SliceCount; i++)
            {
                if (_clients.ContainsKey(FPipeName[i]))
                {
                    var client = _clients[FPipeName[i]];
                    client.Mainloop(0);
                    FClients[i] = client;
                }
                else
                {
                    var pclient = new NamedPipeClient<TUnwrap, TWrap>(FPipeName[i]);
                    var client = new ClientSidePipeWrapper<TR, TW, TUnwrap, TWrap>(pclient, FPipeName[i], Wrap, Unwrap);
                    _clients.Add(client.Name, client);
                    pclient.Start(TimeSpan.FromSeconds(FTimeOutSec[i]));
                    client.Mainloop(0);
                    FClients[i] = client;
                }

                if (!FBroadcast[i]) continue;
                foreach (var data in FData[i])
                {
                    FClients[i].Send(data);
                }
            }
        }
    }

    public abstract class PipeClientSendNode<TR, TW, TUnwrap, TWrap> : IPluginEvaluate
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    {
        [Import]
        public IHDEHost Host;

        [Input("Clients")]
        public Pin<ClientSidePipeWrapper<TR, TW, TUnwrap, TWrap>> FClients;
        [Input("Data")]
        public ISpread<ISpread<TW>> FData;
        [Input("Send", IsBang = true)]
        public ISpread<bool> FBroadcast;


        public void Evaluate(int SpreadMax)
        {
            if (!FClients.IsConnected || FClients.SliceCount <= 0) return;
            for (int i = 0; i < FClients.SliceCount; i++)
            {
                if (!FBroadcast[i] || FClients[i] == null) continue;
                foreach (var data in FData[i])
                {
                    FClients[i].Send(data);
                }
            }
        }
    }

    public abstract class PipeClientSplitNode<TR, TW, TUnwrap, TWrap> : ObjectSplitNode<IPipeClientWrapper<TR, TW, TUnwrap, TWrap>>
        where TR : class
        where TW : class
        where TUnwrap : class
        where TWrap : class
    { }
}
