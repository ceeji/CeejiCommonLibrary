using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading;

namespace Ceeji.Network {
    /// <summary>
    /// 代表一个 Tcp 连接会话。
    /// </summary>
    public class TcpServerUserSession : IDisposable {
        internal TcpServerUserSession(TcpServer server, Socket acceptSocket, System.Net.EndPoint remoteEndPoint) {
            Server = server;

            receiveArgs = new SocketAsyncEventArgs();
            receiveArgs.SetBuffer(new byte[server.BufferSizePerConnection], 0, server.BufferSizePerConnection);
            receiveArgs.UserToken = this;

            sendArgs = new SocketAsyncEventArgs();
            sendArgs.SetBuffer(new byte[server.BufferSizePerConnection], 0, server.BufferSizePerConnection);
            sendArgs.UserToken = this;

            this.acceptSocket = acceptSocket;
            this.RemoteEndPoint = remoteEndPoint;
        }

        /// <summary>
        /// 获取或设置会话 ID
        /// </summary>
        public Guid ID { get; internal set; } = Guid.NewGuid();
        /// <summary>
        /// 获取或设置与当前会话关联的对象。
        /// </summary>
        public object SessionInfo { get; set; }

        public override int GetHashCode() {
            return ID.GetHashCode();
        }

        public override bool Equals(object obj) {
            return (obj is TcpServerUserSession) && ((obj as TcpServerUserSession).ID == this.ID);
        }

        /// <summary>
        /// 关闭连接并释放所有的资源。此方法是线程安全的，且不会抛出异常。
        /// </summary>
        public void Dispose() {
            if (IsDisposed) return;

            lock (locker) {
                try {
                    if (IsDisposed) return;

                    IsDisposed = true;

                    try {
                        acceptSocket.Close();
                    }
                    catch { }
                }
                catch { }
                finally {
                    try {
                        Server.changeClientCount(-1);
                        Server.maxConnectionLimiter.Release();
                        lock (Server.sessions) {
                            Server.sessions.Remove(this);
                        }
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 获取远程终结点。
        /// </summary>
        public System.Net.EndPoint RemoteEndPoint { get; internal set; }
        /// <summary>
        /// 获取或设置会话所关联的服务器对象。
        /// </summary>
        public TcpServer Server { get; internal set; }

        public bool IsDisposed { get; internal set; } = false;

        internal Socket acceptSocket;
        internal SocketAsyncEventArgs receiveArgs, sendArgs;

        internal int lengthPrefix = -1;

        internal short currentMessageID = 0;

        internal object[] waitHandles = new object[short.MaxValue];
        internal ArraySegment<byte>[] replyArrays = new ArraySegment<byte>[short.MaxValue];

        internal byte[] receivedData = null;

        internal byte[] headerBuffer = new byte[8];

        internal object lockerSend = new object();

        /// <summary>
        /// 向远端异步发送数据。不在意数据是否成功投递或得到回应。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="replyToMessage"></param>
        public void SendAndForget(byte[] data, MessageArriveArgs replyToMessage = null) {
            Send(data, 0, data.Length, replyToMessage);
        }

        /// <summary>
        /// 向远端发送数据。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public ArraySegment<byte>? Send(byte[] data, int offset, int count, MessageArriveArgs replyToMessage = null, Action<ArraySegment<byte>> replyCallback = null, bool block = false) {
            if (replyToMessage != null && replyToMessage.HasReplied) throw new Exception("不能重复回复消息");
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count <= 0 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (replyCallback != null && replyToMessage != null) throw new ArgumentException("回复消息时不能再接受回复");
            if (block && replyToMessage !=null) throw new ArgumentException("回复消息时不能再接受回复");
            if (block && replyCallback != null) throw new ArgumentException("使用阻塞模式时不能设置 callback");

            lock (lockerSend) {
                try {
                    MessageFlags flags = MessageFlags.None;

                    short mid;

                    if (replyToMessage != null) {
                        flags |= MessageFlags.Response;
                        mid = replyToMessage.MessageID;
                        replyToMessage.HasReplied = true;
                    }
                    else {
                        flags |= MessageFlags.Request;
                        mid = currentMessageID;
                    }

                    // 获取正文
                    ArraySegment<byte> contentToSend;
                    if (IsEncrypted == false) {
                        contentToSend = new ArraySegment<byte>(data, offset, count);
                    }
                    else {
                        // 加密要发送的数据
                        initEncryption();
                        contentToSend = new ArraySegment<byte>(aes.CreateEncryptor().TransformFinalBlock(data, offset, count));
                    }

                    BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(contentToSend.Count + 4)).CopyTo(headerBuffer, 0);

                    BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder((short)flags)).CopyTo(headerBuffer, 4);

                    BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(mid)).CopyTo(headerBuffer, 6);

                    // 如果带有 callback，则进行处理
                    if (replyCallback != null || block) {
                        if (this.waitHandles[mid] == null) {
                            this.waitHandles[mid] = new object();
                        }

                        if (replyCallback != null) {
                            // 此处使用 Monitor.Wait 实现轻量级的线程同步
                            // waitHandles[mid] 存储一些用于同步的锁对象
                            // Monitor.Wait 等待特定时间以获得回复
                            
                            ThreadPool.QueueUserWorkItem(o => {
                                var id = (short)o;

                                lock (waitHandles[id]) {
                                    var signaled = Monitor.Wait(waitHandles[mid], Server.ReceiveReplyTimeout);

                                    if (signaled) {
                                        replyCallback(this.replyArrays[id]);
                                    }
                                    replyArrays[id] = new ArraySegment<byte>(); // 清理内存
                                }
                            }, mid);
                        }
                    }

                    try {
                        acceptSocket.Send(new ArraySegment<byte>[] { new ArraySegment<byte>(headerBuffer), contentToSend });
                    }
                    catch {
                        Dispose();

                        throw new Exception("连接已经断开");
                    }

                    // 如果是阻塞模式，则等待
                    if (block) {
                        // 此处使用 Monitor.Wait 实现轻量级的线程同步
                        // waitHandles[mid] 存储一些用于同步的锁对象
                        // Monitor.Wait 等待特定时间以获得回复

                        lock (waitHandles[mid]) {
                            var signaled = Monitor.Wait(waitHandles[mid], Server.ReceiveReplyTimeout);

                            try {
                                if (signaled) {
                                    var ret = replyArrays[mid];
                                    return ret;
                                }
                            }
                            finally {
                                replyArrays[mid] = new ArraySegment<byte>(); // 清理内存
                            }
                        }
                        throw new TimeoutException("等待远端回复超时。");
                    }

                    return null;
                }
                finally {
                    if (replyToMessage == null) {
                        if (currentMessageID == short.MaxValue - 1)
                            currentMessageID = 0;
                        else
                            currentMessageID++;
                    }
                }
            }
        }

        /// <summary>
        /// 初始化加解密相关参数。
        /// </summary>
        internal void initEncryption() {
            if (aes == null && IsEncrypted) {
                lock (lockerEncrption) {
                    if (aes != null)
                        return;

                    aes = System.Security.Cryptography.Aes.Create();
                    aes.KeySize = 128;
                    aes.IV = AesIV;
                    aes.Key = AesKey;
                }
            }
        }

        /// <summary>
        /// 指示当前连接是否已经处于加密连接状态。
        /// </summary>
        public bool IsEncrypted {
            get; set;
        } = false;

        /// <summary>
        /// 指示启用 AES 加密时，所使用的 IV。不允许中途更换。
        /// </summary>
        public byte[] AesIV { get; set; }
        /// <summary>
        /// 指示启用 AES 加密时，所使用的 Key。不允许中途更换。
        /// </summary>
        public byte[] AesKey { get; set; }

        internal System.Security.Cryptography.Aes aes;

        /// <summary>
        /// 获取连接建立的时间
        /// </summary>
        public DateTime ConnectingTime {
            get; internal set;
        } = DateTime.UtcNow;

        internal object locker = new object(), lockerEncrption = new object();
    }
}
