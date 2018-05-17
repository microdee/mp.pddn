using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO.Pipes;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using md.stdl.Coding;
using md.stdl.Interfaces;
using NamedPipeWrapper;
using VVVV.PluginInterfaces.V2;

namespace mp.pddn
{
    public interface IPipeClientWrapper<TR, in TW> : IMainlooping
        where TR : class
        where TW : class
    {
        LinkedList<TR> AccumulatedData { get; }
        TR LastData { get; set; }
        string LastError { get; set; }
        bool MessageReceivedBang { get; }
        bool IsConnected { get; }
        int Id { get; }
        string Name { get; }

        void Send(TW data);
    }

    public class ServerSidePipeWrapper<TR, TW> : IPipeClientWrapper<TR, TW>
        where TR : class
        where TW : class
    {
        public NamedPipeConnection<TR, TW> Connection
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
        
        public bool MessageReceivedBang => _flatMessageReceived.Bang;

        public bool IsConnected => _connection.IsConnected;
        public int Id => _connection.Id;
        public string Name => _connection.Name;

        private EventFlattener<ConnectionMessageEventHandler<TR, TW>, TR> _flatMessageReceived;
        private EventFlattener<ConnectionExceptionEventHandler<TR, TW>, Exception> _flatError;
        private Queue<TR> _accumulatedData { get; } = new Queue<TR>();
        private NamedPipeConnection<TR, TW> _connection;

        public void Send(TW data)
        {
            _connection.PushMessage(data);
        }

        public ServerSidePipeWrapper(NamedPipeConnection<TR, TW> conn)
        {
            _connection = conn;
            Subscribe();
        }

        private void Subscribe()
        {
            _connection.ReceiveMessage += OnMessage;
            _connection.Error += OnError;
            _flatMessageReceived = new EventFlattener<ConnectionMessageEventHandler<TR, TW>, TR>(
                handler => _connection.ReceiveMessage += handler,
                handler => _connection.ReceiveMessage -= handler
            );
            _flatError = new EventFlattener<ConnectionExceptionEventHandler<TR, TW>, Exception>(
                handler => _connection.Error += handler,
                handler => _connection.Error -= handler
            );
        }

        private void OnError(NamedPipeConnection<TR, TW> namedPipeConnection, Exception exception)
        {
            LastError = exception.Message;
        }

        private void OnMessage(NamedPipeConnection<TR, TW> connection, TR message)
        {
            _accumulatedData.Enqueue(message);
            LastData = message;
        }

        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);
            _flatMessageReceived.Mainloop(0);
            _flatError.Mainloop(0);
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

    public abstract class PipeServerNode<TR, TW> : IPluginEvaluate
        where TR : class
        where TW : class
    {
        [Import]
        public IHDEHost Host;

        private readonly Dictionary<int, ServerSidePipeWrapper<TR, TW>> _clients = new Dictionary<int, ServerSidePipeWrapper<TR, TW>>();
        private Server<TR, TW> _server;
        
        private EventFlattener<ConnectionEventHandler<TR, TW>, NamedPipeConnection<TR, TW>> _clientConnected;
        private EventFlattener<ConnectionEventHandler<TR, TW>, NamedPipeConnection<TR, TW>> _clientDisconnected;
        private EventFlattener<PipeExceptionEventHandler, Exception> _error;

        [Input("Pipe Name")]
        public IDiffSpread<string> FPipeName;
        [Input("Data")]
        public ISpread<TW> FData;
        [Input("Welcome Data")]
        public ISpread<TW> FDataWelcome;
        [Input("Broadcast", IsBang = true)]
        public ISpread<bool> FBroadcast;

        [Output("Clients")]
        public ISpread<ServerSidePipeWrapper<TR, TW>> FClients;
        [Output("Client Connected", IsBang = true)]
        public ISpread<bool> FConnected;
        [Output("Client Disconnected", IsBang = true)]
        public ISpread<bool> FDisconnected;
        [Output("Error", IsBang = true)]
        public ISpread<bool> FError;
        [Output("Last Error")]
        public ISpread<string> FErrorMessage;
        [Output("Last Disconnected")]
        public ISpread<ServerSidePipeWrapper<TR, TW>> FLastClient;

        public void ObserveEvents()
        {
            _clientConnected = new EventFlattener<ConnectionEventHandler<TR, TW>, NamedPipeConnection<TR, TW>>(
                    handler => _server.ClientConnected += handler,
                    handler => _server.ClientConnected -= handler
                );

            _clientDisconnected = new EventFlattener<ConnectionEventHandler<TR, TW>, NamedPipeConnection<TR, TW>>(
                    handler => _server.ClientDisconnected += handler,
                    handler => _server.ClientDisconnected -= handler
                );

            _error = new EventFlattener<PipeExceptionEventHandler, Exception>(
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
                    _server = new Server<TR, TW>(FPipeName[0], null);
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
                                var clwrap = new ServerSidePipeWrapper<TR, TW>(connection);
                                _clients.Add(connection.Id, clwrap);
                            }
                        }

                        foreach (var data in FDataWelcome)
                        {
                            if (data != null) connection.PushMessage(data);
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
                    if(FData[i] != null) _server.PushMessage(FData[i]);
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

            _clientConnected.Mainloop(0);
            FConnected[0] = _clientConnected.Bang;
            _clientDisconnected.Mainloop(0);
            FDisconnected[0] = _clientDisconnected.Bang;
            _error.Mainloop(0);
            FError[0] = _error.Bang;
        }
    }

    public class ClientSidePipeWrapper<TR, TW> : IPipeClientWrapper<TR, TW>
        where TR : class
        where TW : class
    {
        public NamedPipeClient<TR, TW> Client { get; }

        public NamedPipeConnection<TR, TW> Connection { get; private set; }

        public ClientSidePipeWrapper(NamedPipeClient<TR, TW> client, string name)
        {
            Client = client;
            Name = name;
            SubscribeClient();
        }

        public LinkedList<TR> AccumulatedData { get; } = new LinkedList<TR>();
        public TR LastData { get; set; }

        public string LastError { get; set; }

        public bool MessageReceivedBang => _flatMessageReceived.Bang;

        public bool IsConnected => Connection?.IsConnected ?? true;

        public int Id => Connection?.Id ?? -1;

        public string Name { get; }

        private EventFlattener<ConnectionMessageEventHandler<TR, TW>, TR> _flatMessageReceived;
        private EventFlattener<ConnectionExceptionEventHandler<TR, TW>, Exception> _flatError;
        private Queue<TR> _accumulatedData { get; } = new Queue<TR>();

        public void Send(TW data)
        {
            Client.PushMessage(data);
        }

        private void SubscribeClient()
        {
            Client.ServerMessage += OnMessage;
            _flatMessageReceived = new EventFlattener<ConnectionMessageEventHandler<TR, TW>, TR>(
                handler => Client.ServerMessage += handler,
                handler => Client.ServerMessage -= handler
            );
        }

        private void SubscribeConn()
        {
            Connection.Error += OnError;
            _flatError = new EventFlattener<ConnectionExceptionEventHandler<TR, TW>, Exception>(
                handler => Connection.Error += handler,
                handler => Connection.Error -= handler
            );
        }

        private void OnError(NamedPipeConnection<TR, TW> namedPipeConnection, Exception exception)
        {
            LastError = exception.Message;
        }

        private void OnMessage(NamedPipeConnection<TR, TW> connection, TR message)
        {
            if (Connection == null)
            {
                Connection = connection;
                SubscribeConn();
            }
            _accumulatedData.Enqueue(message);
            LastData = message;
        }

        public void Mainloop(float deltatime)
        {
            OnMainLoopBegin?.Invoke(this, EventArgs.Empty);
            _flatMessageReceived.Mainloop(0);
            _flatError.Mainloop(0);
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

    public abstract class PipeClientNode<TR, TW> : IPluginEvaluate
        where TR : class
        where TW : class
    {
        [Import]
        public IHDEHost Host;

        private readonly Dictionary<string, ClientSidePipeWrapper<TR, TW>> _clients = new Dictionary<string, ClientSidePipeWrapper<TR, TW>>();

        [Input("Pipe Name")]
        public IDiffSpread<string> FPipeName;
        [Input("Connection Timeout Seconds")]
        public ISpread<double> FTimeOutSec;
        [Input("Data")]
        public ISpread<ISpread<TW>> FData;
        [Input("Send", IsBang = true)]
        public ISpread<bool> FBroadcast;

        [Output("Clients")]
        public ISpread<ClientSidePipeWrapper<TR, TW>> FClients;

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
                    var pclient = new NamedPipeClient<TR, TW>(FPipeName[i]);
                    var client = new ClientSidePipeWrapper<TR, TW>(pclient, FPipeName[i]);
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

    public abstract class PipeClientSendNode<TR, TW> : IPluginEvaluate
        where TR : class
        where TW : class
    {
        [Import]
        public IHDEHost Host;

        [Input("Clients")]
        public Pin<ClientSidePipeWrapper<TR, TW>> FClients;
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

    public abstract class PipeClientSplitNode<TR, TW> : ObjectSplitNode<IPipeClientWrapper<TR, TW>> where TR : class where TW : class { }
}
