using Ceeji.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ceeji.Network {
    /// <summary>
    /// 提供一个轻量的、高性能的 Tcp 服务器。该服务器采用消息号/长度前缀作为底层协议。而上层数据则完全交给应用层去处理。通过消息号/长度协议，应用层可以实现真正的全双工通信。
    /// </summary>
    public class TcpServer {
        /// <summary>
        /// 创建 <see cref="TcpServer"/> 的新实例。
        /// </summary>
        public TcpServer() {
        }

        /// <summary>
        /// 启动服务器，开始监听客户端请求。
        /// </summary>
        public void Start() {
            if (IsStarted) return;

            if (RSAOpenAESMode != RSAOpenAESMode.Disallow)
                rsa = new System.Security.Cryptography.RSACryptoServiceProvider(2048);

            // 创建一个信号量，用来防止超过最大连接数
            maxConnectionLimiter = new Semaphore(MaxCountOfConnection, MaxCountOfConnection);
            maxCountOfConcurrentDataProccesserLimiter = new Semaphore(MaxCountOfConcurrentDataProccesser, MaxCountOfConcurrentDataProccesser);

            // create the socket which listens for incoming connections
            listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // bind it to the endPoint
            listenSocket.Bind(ListenEndPoint);

            listenSocket.Listen(Backlog);

            StartAccept();
        }

        /// <summary>
        /// 获取在线用户的数量。
        /// </summary>
        public int SessionCount {
            get {
                return this.mConnectionCount;
            }
        }

        /// <summary>
        /// 获取当前所有用户列表的会话快照。
        /// </summary>
        public ICollection<TcpServerUserSession> Sessions {
            get {
                lock (sessions) {
                    return new HashSet<TcpServerUserSession>(sessions);
                }
            }
        }

        private void SocketAccept_Completed(object sender, SocketAsyncEventArgs e) {
            ProcessAccept(e);
        }

        internal void StartAccept() {
            var acceptEventArg = new SocketAsyncEventArgs();
            acceptEventArg.Completed += SocketAccept_Completed;

            this.maxConnectionLimiter.WaitOne();
            listenForConnectionAsync(acceptEventArg);
        }

        private void listenForConnectionAsync(SocketAsyncEventArgs acceptEventArg) {
            bool willRaiseEvent = listenSocket.AcceptAsync(acceptEventArg);

            if (!willRaiseEvent) {
                ProcessAccept(acceptEventArg);
            }
        }

        /// <summary>
        /// 负责关闭连接。
        /// </summary>
        private void CloseConnection(SocketAsyncEventArgs args) {
            // 减少信号量
            maxConnectionLimiter.Release();

            // 尝试关闭连接
            if (args.AcceptSocket != null) {
                try {
                    args.AcceptSocket.Disconnect(true);
                    args.DisconnectReuseSocket = true;
                }
                catch { }
            }
        }

        private int mConnectionCount = 0;

        internal HashSet<TcpServerUserSession> sessions = new HashSet<TcpServerUserSession>();

        private void ProcessAccept(SocketAsyncEventArgs acceptEventArgs) {
            if (acceptEventArgs.SocketError != SocketError.Success) {
                CloseConnection(acceptEventArgs);

                listenForConnectionAsync(acceptEventArgs);
                return;
            }

            // 继续监听下一个连接请求
            var accessSocket = acceptEventArgs.AcceptSocket;
            acceptEventArgs.AcceptSocket = null;
            listenForConnectionAsync(acceptEventArgs);

            // 增加客户端数量
            changeClientCount(1);

            // 为客户端创建一个 Session
            var session = new TcpServerUserSession(this, accessSocket, accessSocket.RemoteEndPoint);
            session.receiveArgs.Completed += ReceiveComplete_Callback;

            // 将 Session 放入客户端列表
            lock (sessions)
                sessions.Add(session);

            // 开始接收客户端数据
            BeginReceive(session.receiveArgs);
        }

        internal void changeClientCount(int count) {
            Interlocked.Add(ref mConnectionCount, count);
        }

        private void BeginReceive(SocketAsyncEventArgs receiveArgs) {
            var session = receiveArgs.UserToken as TcpServerUserSession;
            var socket = session.acceptSocket;

            if (!socket.ReceiveAsync(receiveArgs)) {
                ProcessReceive(receiveArgs);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs receiveSendEventArgs) {
            var session = receiveSendEventArgs.UserToken as TcpServerUserSession;

            try {
                if (receiveSendEventArgs.SocketError != SocketError.Success || receiveSendEventArgs.BytesTransferred == 0) {
                    // 当出现错误的时候，我们要关闭客户端连接
                    session.Dispose();

                    return;
                }

                // 将接收到的数据缓存起来
                if (session.receivedData != null && session.receivedData.Length > 0) {
                    var oldData = session.receivedData;
                    session.receivedData = new byte[session.receivedData.Length + receiveSendEventArgs.BytesTransferred];
                    if (oldData != null) oldData.CopyTo(session.receivedData, 0);
                    Array.Copy(receiveSendEventArgs.Buffer, 0, session.receivedData, session.receivedData.Length, receiveSendEventArgs.BytesTransferred);
                }
                else {
                    session.receivedData = new byte[receiveSendEventArgs.BytesTransferred];
                    Array.Copy(receiveSendEventArgs.Buffer, 0, session.receivedData, 0, receiveSendEventArgs.BytesTransferred);
                }

                // 针对数据进行处理
                while (true) {
                    if (session.receivedData == null || session.receivedData.Length < sizeof(int)) { // 还没有收到完整的长度前缀，无需任何处理
                        break;
                    }
                    else {
                        // 已经收到长度前缀
                        if (session.lengthPrefix == -1) {
                            // 长度前缀尚未解析，先进行解析
                            session.lengthPrefix = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt32(session.receivedData, 0));

                            if (session.lengthPrefix <= 0) { // 无效的长度前缀
                                session.Dispose();
                                return;
                            }
                        }

                        // 判断是否有一个完整的消息，若有，则处理该消息
                        if (session.receivedData.Length >= sizeof(int) + session.lengthPrefix) {
                            try {
                                if (!ProcessData(session, session.receivedData, sizeof(int), session.lengthPrefix)) {
                                    // 如果用户处理数据出错，为了确保安全，强制客户退出
                                    session.Dispose();
                                    return;
                                }
                            }
                            catch (Exception ex) {
                                // 如果用户处理数据出错，为了确保安全，强制客户退出
                                session.Dispose();
                                return;
                            }

                            // 处理完成后，删除这批数据，进行下一次的处理
                            if (session.receivedData.Length > sizeof(int) + session.lengthPrefix) {
                                // 还有多余数据可以处理
                                var remainedData = new byte[session.receivedData.Length - sizeof(int) - session.lengthPrefix];
                                Array.Copy(session.receivedData, sizeof(int) + session.lengthPrefix, remainedData, 0, remainedData.Length);
                                session.receivedData = remainedData;
                            }
                            else {
                                session.receivedData = null;
                            }
                            session.lengthPrefix = -1;
                        }
                    }
                }

                BeginReceive(receiveSendEventArgs);
            }
            catch {
                session.Dispose();
            }
        }

        private bool ProcessData(TcpServerUserSession session, byte[] receivedData, int offset, int length) {
            try {
                // 首先读取头部信息，以确定所传输的数据特征
                var flags = (MessageFlags)(System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(receivedData, offset)));

                var isReply = (flags & MessageFlags.Response) == MessageFlags.Response;

                // 获取消息号
                var messageID = System.Net.IPAddress.NetworkToHostOrder(BitConverter.ToInt16(receivedData, offset + sizeof(short)));

                // 进行处理，将数据拷贝以防止缓冲区覆盖
                var buffer = new byte[length - sizeof(short) * 2];
                Array.Copy(receivedData, offset + sizeof(short) * 2, buffer, 0, buffer.Length);

                if (session.IsEncrypted) {
                    // 对于已经加密的内容，要进行解密
                    session.initEncryption();
                    
                    buffer = session.aes.CreateDecryptor().TransformFinalBlock(buffer, 0, buffer.Length);
                }

                if (!isReply) {
                    var args = new MessageArriveArgs(session, messageID, buffer, 0, buffer.Length);

                    if ((flags & MessageFlags.InternalCalls) == MessageFlags.InternalCalls) {
                        // 如果是内部指令，可以直接在内部处理
                        if (buffer[0] == (byte)InternalCalls.RequestRSAPublicKey) {
                            // 发送 RSA 公钥
                            session.SendAndForget(Encoding.UTF8.GetBytes(rsa.ToXmlString(false)), args);
                        }
                        else if (buffer[0] == (byte)InternalCalls.SendAESKeysViaRSA) {
                            if (rsa == null) {
                                session.SendAndForget(new byte[] { 1 }, args);
                            }
                            else {
                                // 解析 AES Key 和 IV，然后开始加密对话
                                var aesKeyAndIV = rsa.Decrypt(buffer.Skip(1).ToArray(), true);
                                session.AesKey = aesKeyAndIV.Take(16).ToArray();
                                session.AesIV = aesKeyAndIV.Skip(16).Take(16).ToArray();

                                session.SendAndForget(new byte[] { 0 }, args);
                                session.IsEncrypted = true;
                            }
                        }
                    }
                    else {
                        ThreadPool.QueueUserWorkItem(o => {
                            maxCountOfConcurrentDataProccesserLimiter.WaitOne();

                            try {
                                if (this.MessageArrive != null) {
                                    MessageArrive(this, args);
                                }
                            }
                            catch {
                                session.Dispose();
                            }
                            finally {
                                maxCountOfConcurrentDataProccesserLimiter.Release();
                            }
                        });
                    }

                    return true;
                }
                else {
                    // 对于回复，首先判断是否有等待的句柄
                    if (session.waitHandles[messageID] == null)
                        return true;

                    // 设置参数
                    session.replyArrays[messageID] = new ArraySegment<byte>(buffer, 0, buffer.Length);

                    // 如果有等待句柄，则通知事件
                    lock (session.waitHandles[messageID])
                        Monitor.Pulse(session.waitHandles[messageID]);

                    return true;
                }

            }
            catch {
                return false;
            }
        }

        /// <summary>
        /// 获取或设置从远端获取回复的最大超时时间。
        /// </summary>
        public TimeSpan ReceiveReplyTimeout { get; set; } = TimeSpan.FromSeconds(60);

        private void ReceiveComplete_Callback(object sender, SocketAsyncEventArgs e) {
            ProcessReceive(e);
        }

        /// <summary>
        /// 获取或设置服务器要监听的网络终结点。此属性必须在调用 <see cref="Start"/> 方法之前设置。
        /// </summary>
        public System.Net.EndPoint ListenEndPoint { get; set; }
        /// <summary>
        /// 获取或设置服务器的最大连接数。
        /// </summary>
        public int MaxCountOfConnection { get; set; } = 100;
        /// <summary>
        /// 获取或设置每连接的接收/发送 Buffer 大小。此属性必须在调用 <see cref="Start"/> 方法之前设置，且不能中途修改。
        /// </summary>
        public int BufferSizePerConnection { get; set; } = 30 * 1024; // 30 KB
        /// <summary>
        /// 获取或设置能够同时进行处理的消息的最大数量。
        /// </summary>
        public int MaxCountOfConcurrentDataProccesser { get; set; } = 10;
        /// <summary>
        /// 获取或设置连接的 Backlog，即排队等待连接的客户端的最大数目。
        /// </summary>
        public int Backlog { get; set; } = 128;
        /// <summary>
        /// 当消息到达服务器时触发的事件。
        /// </summary>
        public event EventHandler<MessageArriveArgs> MessageArrive;
        /// <summary>
        /// 是否允许客户端请求服务器打开 RSA 加密功能，并获取公钥，然后使用 RSA 方式和服务器协商 AES 密钥并对后续请求进行加密。
        /// </summary>
        public RSAOpenAESMode RSAOpenAESMode { get; set; } = RSAOpenAESMode.Allow;

        internal System.Security.Cryptography.RSACryptoServiceProvider rsa;

        /// <summary>
        /// 指示服务器是否处于启动状态。
        /// </summary>
        public bool IsStarted { get; internal set; }

        private Socket listenSocket;
        // private ObjectPool<SocketAsyncEventArgs> poolOfAcceptEventArgs;
        internal System.Threading.Semaphore maxConnectionLimiter, maxCountOfConcurrentDataProccesserLimiter;
    }

    public enum RSAOpenAESMode {
        Disallow,
        Allow,
        Always
    }

    /// <summary>
    /// 用于提供消息处理参数。
    /// </summary>
    public class MessageArriveArgs : EventArgs {
        internal MessageArriveArgs(TcpServerUserSession session, short id, byte[] arr, int offset, int length) {
            Session = session;
            MessageID = id;
            Content = new ArraySegment<byte>(arr, offset, length);
        }

        /// <summary>
        /// 获取与此事件相关的 <see cref="TcpServerUserSession"/>
        /// </summary>
        public TcpServerUserSession Session {
            get; private set; }
        /// <summary>
        /// 获取与此事件相关的消息 ID
        /// </summary>
        internal short MessageID { get; private set; }
        /// <summary>
        /// 获取与此事件相关的正文
        /// </summary>
        public ArraySegment<byte> Content { get; private set; }
        /// <summary>
        /// 获取该请求是否已经被回复
        /// </summary>
        public bool HasReplied { get; internal set; }
    }

    /// <summary>
    /// 代表传输中的消息标志。
    /// </summary>
    [Flags]
    internal enum MessageFlags : short {
        /// <summary>
        /// 这是一个请求
        /// </summary>
        Request = 1,
        /// <summary>
        /// 这是一个回复
        /// </summary>
        Response = 2,
        /// <summary>
        /// 说明这个请求可以直接被处理，不需要发送给客户端处理
        /// </summary>
        InternalCalls = 4,
        None = 0,
    }

    /// <summary>
    /// 定义由库直接解析和执行的命令列表。
    /// </summary>
    internal enum InternalCalls : byte {
        /// <summary>
        /// 客户端向服务器请求 RSA 公钥
        /// </summary>
        RequestRSAPublicKey,
        /// <summary>
        /// 通过 RSA 加密的方式发送 AES key 信息
        /// </summary>
        SendAESKeysViaRSA
    }
}
