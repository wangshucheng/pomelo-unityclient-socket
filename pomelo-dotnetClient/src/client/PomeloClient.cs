using SimpleJson;
using System;
using System.ComponentModel;
using System.Net;
using WebSocket4Net;
using System.Threading;

namespace Pomelo.DotNetClient
{
    /// <summary>
    /// network state enum
    /// </summary>
    public enum NetWorkState
    {
        [Description("initial state")]
        CLOSED,

        [Description("connecting server")]
        CONNECTING,

        [Description("server connected")]
        CONNECTED,

        [Description("disconnected with server")]
        DISCONNECTED,

        [Description("connect timeout")]
        TIMEOUT,

        [Description("netwrok error")]
        ERROR
    }

    public class PomeloClient : IDisposable
    {
        /// <summary>
        /// netwrok changed event
        /// </summary>
        public event Action<NetWorkState> NetWorkStateChangedEvent;


        private NetWorkState netWorkState = NetWorkState.CLOSED;   //current network state

        private EventManager eventManager;
        private WebSocket socket;
        private Protocol protocol;
        private bool disposed = false;
        private uint reqId = 1;
        private const string ARRAY_FLAG = "[";
        private const string URL_HEADER = "ws://";

        private ManualResetEvent timeoutEvent = new ManualResetEvent(false);
        private int timeoutMSec = 8000;    //connect timeout count in millisecond

        Action callback;

        public PomeloClient()
        {
        }

        /// <summary>
        /// initialize pomelo client
        /// </summary>
        /// <param name="host">server name or server ip (www.xxx.com/127.0.0.1/::1/localhost etc.)</param>
        /// <param name="port">server port</param>
        /// <param name="callback">socket successfully connected callback(in network thread)</param>
        public void initClient(string host, int port, Action callback = null)
        {
            this.callback = callback;

            timeoutEvent.Reset();
            eventManager = new EventManager();
            NetWorkChanged(NetWorkState.CONNECTING);

            this.socket = new WebSocket(URL_HEADER+host + ":" + port);

            this.socket.Opened += this.SocketOpened;
            //this.socket.MessageReceived += this.SocketMessage;
            this.socket.DataReceived += this.DataMessage;
            this.socket.Closed += this.SocketConnectionClosed;
            this.socket.Error += this.SocketError;

            this.socket.Open();

            if (timeoutEvent.WaitOne(timeoutMSec, false))
            {
                if (netWorkState != NetWorkState.CONNECTED && netWorkState != NetWorkState.ERROR)
                {
                    NetWorkChanged(NetWorkState.TIMEOUT);
                    Dispose();
                }
            }
        }

        /// <summary>
        /// 网络状态变化
        /// </summary>
        /// <param name="state"></param>
        private void NetWorkChanged(NetWorkState state)
        {
            netWorkState = state;

            if (NetWorkStateChangedEvent != null)
            {
                NetWorkStateChangedEvent(state);
            }
        }

        public void connect()
        {
            connect(null, null);
        }

        public void connect(JsonObject user)
        {
            connect(user, null);
        }

        public void connect(Action<JsonObject> handshakeCallback)
        {
            connect(null, handshakeCallback);
        }

        public bool connect(JsonObject user, Action<JsonObject> handshakeCallback)
        {
            try
            {
                protocol.start(user, handshakeCallback);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return false;
            }
        }

        private JsonObject emptyMsg = new JsonObject();
        public void request(string route, Action<JsonObject> action)
        {
            this.request(route, emptyMsg, action);
        }

        public void request(string route, JsonObject msg, Action<JsonObject> action)
        {
            this.eventManager.AddCallBack(reqId, action);
            protocol.send(route, reqId, msg);

            reqId++;
        }

        public void notify(string route, JsonObject msg)
        {
            protocol.send(route, msg);
        }

        public void on(string eventName, Action<JsonObject> action)
        {
            eventManager.AddOnEvent(eventName, action);
        }

        internal void processMessage(Message msg)
        {
            if (msg.type == MessageType.MSG_RESPONSE)
            {
                //msg.data["__route"] = msg.route;
                //msg.data["__type"] = "resp";
                eventManager.InvokeCallBack(msg.id, msg.data);
            }
            else if (msg.type == MessageType.MSG_PUSH)
            {
                //msg.data["__route"] = msg.route;
                //msg.data["__type"] = "push";
                eventManager.InvokeOnEvent(msg.route, msg.data);
            }
        }

        public void disconnect()
        {
            Dispose();
            NetWorkChanged(NetWorkState.DISCONNECTED);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // The bulk of the clean-up code
        protected virtual void Dispose(bool disposing)
        {
            if (this.disposed)
                return;

            if (disposing)
            {
                // free managed resources
                if (this.protocol != null)
                {
                    this.protocol.close();
                }

                if (this.eventManager != null)
                {
                    this.eventManager.Dispose();
                }

                try
                {
                    if (this.socket != null)
                    {
                        this.socket.Opened -= this.SocketOpened;
                        //this.socket.MessageReceived -= this.SocketMessage;
                        this.socket.DataReceived -= this.DataMessage;
                        this.socket.Closed -= this.SocketConnectionClosed;
                        this.socket.Error -= this.SocketError;
                        this.socket.Close();

                        this.socket = null;
                    }
                }
                catch (Exception)
                {
                    //todo : 有待确定这里是否会出现异常，这里是参考之前官方github上pull request。emptyMsg
                }

                this.disposed = true;
            }
        }

        private void SocketOpened(object sender, EventArgs e)
        {
            this.protocol = new Protocol(this, this.socket);
            NetWorkChanged(NetWorkState.CONNECTED);

            this.callback?.Invoke();

            timeoutEvent.Set();
        }

        private void DataMessage(object sender, DataReceivedEventArgs e)
        {
            if (e != null)
            {
                if (netWorkState != NetWorkState.CONNECTED)
                    return;

                if (e.Data.Length > 0)
                {
                    this.protocol.processBytes(e.Data, 0, e.Data.Length);
                }
                else
                {
                    disconnect();
                }
            }
        }

        //Connetction close event.
        private void SocketConnectionClosed(object sender, EventArgs e)
        {
            Console.WriteLine("WebSocketConnection was terminated!");
            Console.WriteLine(e.ToString());

            if (netWorkState != NetWorkState.TIMEOUT)
            {
                NetWorkChanged(NetWorkState.CLOSED);
            }
            Dispose();
        }

        //Connection error event.
        private void SocketError(object sender, EventArgs e)
        {
            Console.WriteLine("socket client error:");
            Console.WriteLine(e.ToString());

            if (netWorkState != NetWorkState.TIMEOUT)
            {
                NetWorkChanged(NetWorkState.ERROR);
            }
            Dispose();
        }
    }
}