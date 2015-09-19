// ====================================================================
// ==        Copyright(C) Ceeji Cheng All rights reserved.           ==
// ====================================================================
// ==  Ceeji Cheng <i@ceeji.net>, Created 2015/06/27                 ==
// ==  Allowed Use After License Or Use By LearningByDoing Co.Ltd.   ==
// ====================================================================

using Ceeji.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ceeji.Network {
    /// <summary>
    /// 代表 Tcp 信使，用来传递 Tcp 协议上的消息。此类使用对象池重用几乎所有的数据成员、用户给定的 TData 类型成员，并使用缓冲池减少 byte[] 缓冲区的分配，所有的方法都是异步执行的。
    /// </summary>
    /// <typeparam name="TData">数据的类型，对于每一个连接（会话），这个类型只有一个实例，可用来存储交互过程中所需要的数据。并且，为提高性能，系统会重用已关闭连接的此类型的实例。</typeparam>
    public class TcpMessager<TData> : Messager<TData> where TData : new() {

        #region Constructors

        /// <summary>
        /// 创建一个具有默认最大并发连接数（100）的新的 TcpMessager。用来传递 Tcp 协议上的消息。此类使用对象池重用几乎所有的数据成员、用户给定的 TData 类型成员，并使用缓冲池减少 byte[] 缓冲区的分配，所有的方法都是异步执行的。
        /// </summary>
        public TcpMessager() : this(100) {
        }

        /// <summary>
        /// 创建一个具有默认最大并发连接数（100）的新的 TcpMessager。用来传递 Tcp 协议上的消息。此类使用对象池重用几乎所有的数据成员、用户给定的 TData 类型成员，并使用缓冲池减少 byte[] 缓冲区的分配，所有的方法都是异步执行的。
        /// </summary>
        /// <param name="listeners">用来监听的 listener，必须手工调用其 Start 方法开始监听。</param>
        public TcpMessager(IEnumerable<TcpEndPointListener> listeners)
            : this(100, null, listeners) {

        }

        /// <summary>
        /// 创建一个具有默认最大并发连接数（100）的新的 TcpMessager。用来传递 Tcp 协议上的消息。此类使用对象池重用几乎所有的数据成员、用户给定的 TData 类型成员，并使用缓冲池减少 byte[] 缓冲区的分配，所有的方法都是异步执行的。
        /// </summary>
        /// <param name="listener">用来监听的 listener，必须手工调用其 Start 方法开始监听。</param>
        public TcpMessager(TcpEndPointListener listener)
            : this(100, null, listener) {

        }

        /// <summary>
        /// 创建一个具有默认最大并发连接数（100）的新的 TcpMessager。用来传递 Tcp 协议上的消息。此类使用对象池重用几乎所有的数据成员、用户给定的 TData 类型成员，并使用缓冲池减少 byte[] 缓冲区的分配，所有的方法都是异步执行的。
        /// </summary>
        /// <param name="dataResetFactory">用来进行数据重设的委托，此委托应保证 TData 类型的会话相关数据可以在每个连接建立时重用。</param>
        public TcpMessager(Action<TData> dataResetFactory)
            : this(100, dataResetFactory) {
        }

        /// <summary>
        /// 创建一个新的 TcpMessager。用来传递 Tcp 协议上的消息。此类使用对象池重用几乎所有的数据成员、用户给定的 TData 类型成员，并使用缓冲池减少 byte[] 缓冲区的分配，所有的方法都是异步执行的。
        /// </summary>
        /// <param name="maxConcurrentConnectionCount">指定服务器支持的最大并发连接数。系统将根据并发连接数自动调整相应的参数。</param>
        /// <param name="dataResetFactory">用来进行数据重设的委托，此委托应保证 TData 类型的会话相关数据可以在每个连接建立时重用。可以为 null。</param>
        public TcpMessager(int maxConcurrentConnectionCount, Action<TData> dataResetFactory = null) {
            this.Status = MessagerStatus.Closed;
            this.MaxConcurrentConnectionCount = maxConcurrentConnectionCount;

            var f = dataResetFactory;

            // 创建连接对象池
            this.mConnectionPool = new ObjectPool<TcpSession<TData>>(() => {
                var ret = new TcpSession<TData>();
                ret.EndPoint = new IPEndPoint();
                ret.Data = new TData();

                return ret;
            }, (data) => {
                data.Status = ConnectionStatus.Invalid;
                data.Messager = null;
                data.BeginTime = DateTime.MinValue;
                data.PooledConnection = null;
                data.PooledReceiveSocketAsyncEventArgs = null;
                data.Socket = null;
                try {
                    data.mSemaphoreSend.Release();
                }
                catch { }

                if (f != null)
                    f(data.Data);
            }, Math.Max(Math.Min((int)(MaxConcurrentConnectionCount * 0.3), 10), MaxConcurrentConnectionCount), (int)(MaxConcurrentConnectionCount * 1.1));

            // 创建缓冲区池
            this.mBufferPool = new ObjectPool<byte[]>(() => new byte[bufferSize], Math.Max(Math.Min((int)(MaxConcurrentConnectionCount * 0.1), 20), MaxConcurrentConnectionCount), (int)(MaxConcurrentConnectionCount * 1.2));

            // 限制连接数
            this.mSemaphoreConnection = new Semaphore(this.MaxConcurrentConnectionCount, this.MaxConcurrentConnectionCount);

            // 创建异步事件池
            mSocketAsyncArgsPool = new ObjectPool<SocketAsyncEventArgs>(() => {
                var ret = new SocketAsyncEventArgs();
                ret.UserToken = new SocketAsyncData<TData>();

                return ret;
            }, args => {
                args.Completed -= Send_Complete;
                args.Completed -= Receive_Completed;
                args.Completed -= Connect_Complete;
            },
            Math.Max(Math.Min((int)(MaxConcurrentConnectionCount * 0.6), 20), MaxConcurrentConnectionCount), (int)(MaxConcurrentConnectionCount * 2.2));
        }

        /// <summary>
        /// 创建一个新的 TcpMessager，并绑定指定的 TcpListener。
        /// </summary>
        /// <param name="maxConcurrentConnectionCount">指定服务器支持的最大并发连接数。系统将根据并发连接数自动调整相应的参数。</param>
        /// <param name="dataResetFactory">用来进行数据重设的委托，此委托应保证 TData 类型的会话相关数据可以在每个连接建立时重用。</param>
        /// <param name="listener">用来监听的 listener，必须手工调用其 Start 方法开始监听。</param>
        public TcpMessager(int maxConcurrentConnectionCount, Action<TData> dataResetFactory, TcpEndPointListener listener)
            : this(maxConcurrentConnectionCount, dataResetFactory, new TcpEndPointListener[] { listener }) {
        }

        /// <summary>
        /// 创建一个新的 TcpMessager，并绑定指定的 TcpListener。
        /// </summary>
        /// <param name="maxConcurrentConnectionCount">指定服务器支持的最大并发连接数。系统将根据并发连接数自动调整相应的参数。</param>
        /// <param name="dataResetFactory">用来进行数据重设的委托，此委托应保证 TData 类型的会话相关数据可以在每个连接建立时重用。</param>
        /// <param name="listeners">用来监听的 listener，必须手工调用其 Start 方法开始监听。</param>
        public TcpMessager(int maxConcurrentConnectionCount, Action<TData> dataResetFactory, IEnumerable<TcpEndPointListener> listeners)
            : this(maxConcurrentConnectionCount, dataResetFactory) {
                this.Listeners = listeners;
        }

        #endregion

        #region Properties

        /// <summary>
        /// 获取或设置监听所使用的 Listeners。
        /// </summary>
        public IEnumerable<TcpEndPointListener> Listeners {
            get {
                lock (mLockListeners)
                    return this.mListeners;
            }
            set {
                lock (mLockListeners) {
                    if (this.mListeners != null) {
                        this.mListeners.Iterate(x => x.ConnectionBegin -= mListener_ConnectionBegin);
                    }

                    this.mListeners = value;
                    this.mListeners.Iterate(x => x.ConnectionBegin += mListener_ConnectionBegin);
                }
            }
        }

        /// <summary>
        /// 获取该 Messager{T} 容许的最大连接数量。
        /// </summary>
        public int MaxConcurrentConnectionCount { get; private set; }

        /// <summary>
        /// 获取或设置此 Messager 的传输块的大小。默认为 20 KB。
        /// </summary>
        public int TransferBlockSize {
            get {
                return this.bufferSize;
            }
            set {
                if (this.Status != MessagerStatus.Closed)
                    throw new InvalidOperationException("不能在使用（连接）之后修改缓冲区大小");

                this.bufferSize = value;
            }
        }

        /// <summary>
        /// 返回在线会话的实时数量。
        /// </summary>
        public int CountOfSessions {
            get {
                return this.mCountSessions;
            }
        }

        /// <summary>
        /// 获取所有在线的 Tcp 会话。所获得的是一个快照，不会随之后的操作而变化。在高性能服务器中，此操作建议不要经常进行，避免影响性能。
        /// </summary>
        /// <returns></returns>
        public IList<TcpSession<TData>> GetSessions() {
            IList<TcpSession<TData>> ret;

            lock (mLockConnections)
                ret = this.mDicConnections.ToArray();

            return ret;
        }

        #endregion

        /// <summary>
        /// 当超过最大连接数时发生
        /// </summary>
        public event EventHandler MaxConnectionCountExceeded;

        private void mListener_ConnectionBegin(object sender, TcpConnectionBeginEventArgs e) {
            // 创建一个新的 Connection 对象
            var conn = createConnectionAndAdd(e.ConnectionObject);

            if (conn == null) {
                // 如果未能创建连接，则触发报警事件
                if (MaxConnectionCountExceeded != null)
                    MaxConnectionCountExceeded(this, EventArgs.Empty);

                return;
            }

            // 引发事件
            this.OnIncomingConnectionEstablished(conn);

            // 开始监听所有接收到的数据，必须位于引发事件之后，以确保用户完成了相关的初始化后才收到数据
            beginReceive(conn);
        }

        private TcpSession<TData> createConnectionAndAdd(Socket s) {
            if (!mSemaphoreConnection.WaitOne(timeoutWaitingConnectionAvailable)) {
                // 连接达到最大值，且无法等待到可用连接，则不允许继续连接
                return null;
            }

            // 增大连接数
            Interlocked.Increment(ref mCountSessions);

            // 从池中取出一个可用的连接对象
            var connFromPool = this.mConnectionPool.Get();
            var conn = connFromPool.Value;
            conn.PooledConnection = connFromPool;

            // == 设置连接信息 ==
            // 连接建立时间
            conn.BeginTime = DateTime.UtcNow;

            // 绑定的终结点（可重用，只修改，不分配新对象）
            var ep = ((System.Net.IPEndPoint)s.RemoteEndPoint);
            var ipep = conn.EndPoint as IPEndPoint;
            ipep.IPAddress = ep.Address;
            ipep.Port = ep.Port;

            // 关联连接和信使
            conn.Messager = this;
            conn.Socket = s;

            conn.PooledReceiveSocketAsyncEventArgs = this.mSocketAsyncArgsPool.Get(); // 每个 TcpSession 只分配一次用于 Receive 的 SocketAsyncEventArgs
            conn.PooledReceiveSocketAsyncEventArgs.Value.Completed += this.Receive_Completed;
            (conn.PooledReceiveSocketAsyncEventArgs.Value.UserToken as SocketAsyncData<TData>).Connection = conn;
            conn.PooledReceiveBuffer = this.mBufferPool.Get();

            // 初始化接收所用的参数
            conn.PooledReceiveSocketAsyncEventArgs.Value.SetBuffer(conn.PooledReceiveBuffer.Value, 0, bufferSize);

            // 启用连接
            conn.Status = ConnectionStatus.Running;

            // 将 Connection 加入词典
            lock (mDicConnections)
                mDicConnections.Add(conn);

            Console.WriteLine("New Incoming Connection, Online = " + this.CountOfSessions);

            // 返回连接对象
            return conn;
        }

        private void beginReceive(TcpSession<TData> conn) {
            // 使用每个 TcpSession 只分配一次的 SocketAsyncEventArgs
            // 不停地进行数据的读取。
            // 同时也起到检测连接断开的作用
            Console.WriteLine("beginReceive");
            try {
                var p = conn.PooledReceiveSocketAsyncEventArgs;
                var asyncArg = p.Value;
                asyncArg.SetBuffer(0, bufferSize);
                var userToken = asyncArg.UserToken as SocketAsyncData<TData>;
                userToken.DebugInfo = "beginReceive";


                if (!conn.Socket.ReceiveAsync(asyncArg)) {
                    try {
                        if (asyncArg.SocketError != SocketError.Success) {
                            this.OnSendError(conn);
                        }
                    }
                    finally {
                        userToken.DebugInfo = "beginReceiveDirectly";
                    }
                }
            }
            catch {
                Console.WriteLine("beginReceive Error");
                sessionClosed(conn);
            }
        }

        private void Receive_Completed(object sender, SocketAsyncEventArgs e) {
            Console.WriteLine("Receive_Completed, BytesTransferred = {0}, SocketError = {1}", e.BytesTransferred, e.SocketError);
            var userToken = e.UserToken as SocketAsyncData<TData>;
            var tcpSession = userToken.Connection as TcpSession<TData>;

            try {
                if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success) {
                    // 如果接收数据成功，则引发数据接收事件
                    this.OnDataReceived(tcpSession, e.Buffer, e.Offset, e.BytesTransferred);

                    beginReceive(tcpSession);
                }
                else {
                    sessionClosed(tcpSession);
                    return;
                }
            }
            finally {
                // 引发数据接收事件之后，清除相关的池变量
                userToken.DebugInfo = "beginReceiveComplete";
            }
        }

        protected override void OnSend(Session<TData> conn, byte[] data, int pos, int count) {
            if (count == 0 || conn == null || conn.Status != ConnectionStatus.Running)
                return;

            Console.WriteLine("OnSend");
            var p = mSocketAsyncArgsPool.Get(); // 从池中取出一个对象，以用来发送数据
            System.Diagnostics.Debug.Assert(p.Value != null);
            var tcpConn = conn as TcpSession<TData>;

            // 限制在同一个连接中只能并发进行一个发送操作，【一定要确保回收该资源】

            var asyncArgs = p.Value;
            var userToken = asyncArgs.UserToken as SocketAsyncData<TData>;

            // 复制缓冲区
            byte[] buffer;
            if (count <= bufferSize) {
                var bufferFromPool = this.mBufferPool.Get();
                buffer = bufferFromPool.Value;
                userToken.PooledBuffer = bufferFromPool;
            }
            else {
                buffer = new byte[count];
            }

            Array.Copy(data, pos, buffer, 0, count);

            // 初始化发送所用的参数
            asyncArgs.SetBuffer(buffer, 0, count); // 发送时必须复制 data，否则用户可能在随后修改数组的内容
            asyncArgs.Completed += Send_Complete;
            userToken.Connection = conn;
            userToken.PooledSocketAsyncEventArgs = p;
            userToken.DebugInfo = "OnSend";

            bool sendRet = false;
            try {
                sendRet = tcpConn.Socket.SendAsync(asyncArgs);
            }
            catch (Exception ex) {
                Console.WriteLine("OnSend Error: " + ex.ToString());
            }

            if (!sendRet) {
                if (userToken.PooledBuffer != null) {
                    userToken.PooledBuffer.Dispose();
                    userToken.PooledBuffer = null;
                }
                p.Dispose();
                sessionClosed(tcpConn);
                //tcpConn.mSemaphoreSend.Release();
                this.OnSendError(conn);
            }
        }

        void Send_Complete(object sender, SocketAsyncEventArgs e) {
            Console.WriteLine("Send_Complete");

            var userToken = e.UserToken as SocketAsyncData<TData>;
            e.Completed -= Send_Complete;
            userToken.PooledSocketAsyncEventArgs.Dispose();
            if (userToken.PooledBuffer != null) {
                userToken.PooledBuffer.Dispose();
                userToken.PooledBuffer = null;
            }
            try {
                if (e.SocketError != SocketError.Success && e.SocketError != SocketError.OperationAborted) {
                    this.OnSendError(userToken.Connection);
                    sessionClosed(userToken.Connection as TcpSession<TData>);
                }
            }
            finally {
                try {
                    // 释放资源
                    userToken.DebugInfo = "beginSendComplete";
                    // (userToken.Connection as TcpSession<TData>).mSemaphoreSend.Release();
                    
                }
                catch {
                }
            }
        }

        protected override void OnCloseSession(Session<TData> session) {
            sessionClosed(session as TcpSession<TData>);
        }

        protected override void OnConnect(EndPoint endPoint, MessagerConnectionEventHandler<TData, Exception> eventHandler) {
            // 获取终结点
            checkEndPoint(endPoint);
            var ep = endPoint as IPEndPoint;

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var p = mSocketAsyncArgsPool.Get(); // 从池中取出一个对象，以用来进行连接。

            try {
                var userToken = p.Value.UserToken as SocketAsyncData<TData>;

                // 初始化所用的参数
                p.Value.RemoteEndPoint = new System.Net.IPEndPoint(ep.IPAddress, ep.Port);
                p.Value.Completed += Connect_Complete;
                p.Value.UserToken = userToken;
                userToken.TempSocket = socket;
                userToken.EventHandler = eventHandler;
                userToken.PooledSocketAsyncEventArgs = p;
                userToken.DebugInfo = "OnConnect";

                if (!socket.ConnectAsync(p.Value)) {
                    try {
                        if (p.Value.SocketError != SocketError.Success) { eventHandler(this, null, new Exception("连接失败，SocketError = " + p.Value.SocketError.ToString())); }
                    }
                    finally {
                        p.Dispose();
                    }
                }
            }
            catch (Exception ex) {
                eventHandler(this, null, ex);
                p.Dispose();
            }
        }

        private void sessionClosed(TcpSession<TData> session) {
            if (session == null || session.Socket == null)
                return;

            try {
                Console.WriteLine("sessionClosed, Online = " + this.CountOfSessions);

                var removed = false;
                lock (mDicConnections) {
                    // 移除连接
                    removed = mDicConnections.Remove(session);
                    session.Status = ConnectionStatus.Closed;
                }
                if (removed) {
                    try {
                        this.OnConnectionClosed(session); // 调用事件
                        session.Socket.Close();
                    }
                    finally {
                        session.PooledReceiveSocketAsyncEventArgs.Value.Completed -= Receive_Completed; // 防止重复调用接收完成事件
                        session.PooledReceiveSocketAsyncEventArgs.Dispose(); // 归还用来接收数据的 SocketAsyncEventArgs
                        session.PooledReceiveBuffer.Dispose();
                        mSemaphoreConnection.Release(); // 归还标示连接数的 Semephore
                        session.PooledConnection.Dispose();

                        // 减少连接数
                        Interlocked.Decrement(ref mCountSessions);
                    }
                }
            }
            catch { }
        }

        private void Connect_Complete(object sender, SocketAsyncEventArgs e) {
            var userToken = e.UserToken as SocketAsyncData<TData>;

            try {
                if (e.SocketError != SocketError.Success) {
                    userToken.EventHandler(this, null, new Exception("连接失败，SocketError = " + e.SocketError.ToString()));
                }
                else {
                    // 创建新的连接对象
                    Status = MessagerStatus.Connected;
                    var conn = createConnectionAndAdd(userToken.TempSocket);
                    userToken.EventHandler(this, conn, null);

                    // 开始接收数据
                    beginReceive(conn);
                }
            }
            finally {
                // 释放资源
                userToken.DebugInfo = "beginConnectComplete";
                userToken.PooledSocketAsyncEventArgs.Dispose();
            }
        }

        /// <summary>
        /// 检查终结点参数
        /// </summary>
        /// <param name="endPoint"></param>
        private void checkEndPoint(EndPoint endPoint) {
            if (endPoint is IPEndPoint)
                return;
            throw new ArgumentException("TcpMessager 只承认 IPEndPoint 作为 EndPoint 的参数类型");
        }

        private static void resetSocketAsyncData(SocketAsyncData<TData> obj) {
            obj.Connection = null;
            obj.PooledBuffer = null;
            obj.PooledSocketAsyncEventArgs = null;
            obj.EventHandler = null;
            obj.TempSocket = null;
        }

        private ObjectPool<SocketAsyncEventArgs> mSocketAsyncArgsPool;
        private ObjectPool<TcpSession<TData>> mConnectionPool;
        private ObjectPool<byte[]> mBufferPool;
        private object mLockConnections = new object();
        private HashSet<TcpSession<TData>> mDicConnections = new HashSet<TcpSession<TData>>();
        private IEnumerable<TcpEndPointListener> mListeners;
        private Semaphore mSemaphoreConnection;
        private const int timeoutWaitingConnectionAvailable = 2000;
        private int bufferSize = 100 * 1024; // 20 K 缓冲区
        private object mLockListeners = new object();
        private volatile int mCountSessions = 0;
    }

    /// <summary>
    /// 代表一次特定的连接。此类可以被重用。
    /// </summary>
    public class TcpSession<TData> : Session<TData> {
        internal Socket Socket;
        internal PooledObject<TcpSession<TData>> PooledConnection;
        internal PooledObject<SocketAsyncEventArgs> PooledReceiveSocketAsyncEventArgs;
        internal PooledObject<byte[]> PooledReceiveBuffer;
        internal Semaphore mSemaphoreSend = new Semaphore(1, 1);
    }

    /// <summary>
    /// 代表 IP 终结点。
    /// </summary>
    public class IPEndPoint : EndPoint {
        /// <summary>
        /// 创建 IP 终结点的新实例。
        /// </summary>
        public IPEndPoint(IPAddress address, int port) {
            this.IPAddress = address;
            this.Port = port;
        }

        public IPEndPoint() {
        }

        public IPAddress IPAddress { get; set; }
        public int Port { get; set; }

        public override string ToString() {
            return string.Format("{0}:{1}", IPAddress, Port);
        }
    }

    /// <summary>
    /// 存储所有和异步 Socket 有关的数据。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class SocketAsyncData<T> {
        public Session<T> Connection;
        public PooledObject<SocketAsyncEventArgs> PooledSocketAsyncEventArgs;
        public PooledObject<byte[]> PooledBuffer;
        public MessagerConnectionEventHandler<T, Exception> EventHandler;
        public Socket TempSocket;
        public string DebugInfo;
    }
}
