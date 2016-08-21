using Ceeji.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace Ceeji.Network {
    /// <summary>
    /// 代表一个可以和 Server 通信的客户端。
    /// </summary>
    public class TcpClient {
        /// <summary>
        /// 创建 <see cref="TcpClient"/> 的新实例。
        /// </summary>
        public TcpClient(string hostname, int port) {
            this.hostname = hostname;
            this.port = port;
        }

        private void closeSocket() {
            if (socket == null) return;

            lock (socket) {
                if (socket != null) {
                    try {
                        socket.Close();
                        socket = null;
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// 连接到服务器，如果连接失败，则会抛出异常。
        /// </summary>
        public void Connect() {
            closeSocket();

            if (socket == null) {
                socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            }

            socket.Connect(hostname, port);

            // 开始接收数据
            (new Thread(startReceive)).Start();

            if (AutoRequestEncryption)
                RequestEncryption();
        }

        private object lockerSend = new object();
        internal byte[] headerBuffer = new byte[8];

        /// <summary>
        /// 向远端发送数据。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        internal  ArraySegment<byte>? Send(byte[] data, int offset, int count, MessageArriveArgs replyToMessage = null, Action<ArraySegment<byte>> replyCallback = null, bool block = false, MessageFlags extraFlags = MessageFlags.None) {
            if (socket == null) throw new Exception("连接已经断开或尚未打开");

            if (replyToMessage != null && replyToMessage.HasReplied) throw new Exception("不能重复回复消息");
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset >= data.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count <= 0 || offset + count > data.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (replyCallback != null && replyToMessage != null) throw new ArgumentException("回复消息时不能再接受回复");
            if (block && replyToMessage != null) throw new ArgumentException("回复消息时不能再接受回复");
            if (block && replyCallback != null) throw new ArgumentException("使用阻塞模式时不能设置 callback");

            lock (lockerSend) {
                try {
                    MessageFlags flags = MessageFlags.None | extraFlags;

                    short mid;

                    if (replyToMessage != null) {
                        flags |= MessageFlags.Response;
                        mid = replyToMessage.MessageID;
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
                        contentToSend = new ArraySegment<byte>(aesEncoder.TransformFinalBlock(data, offset, count));
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
                            ThreadPool.QueueUserWorkItem(o => {
                                var id = (short)o;

                                lock (waitHandles[id]) {
                                    if (Monitor.Wait(waitHandles[id], ReceiveReplyTimeout)) {
                                        replyCallback(this.replyArrays[id]);
                                        replyArrays[id] = new ArraySegment<byte>(); // 清理内存
                                    }
                                }
                            }, mid);
                        }
                    }

                    try {
                        socket.Send(new ArraySegment<byte>[] { new ArraySegment<byte>(headerBuffer), contentToSend });
                    }
                    catch {
                        closeSocket();

                        throw new Exception("连接已经断开");
                    }

                    // 如果是阻塞模式，则等待
                    if (block) {
                        lock (waitHandles[mid]) {
                            if (Monitor.Wait(waitHandles[mid], ReceiveReplyTimeout)) {
                                var ret = replyArrays[mid];
                                replyArrays[mid] = new ArraySegment<byte>(); // 清理内存

                                return ret;
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
        /// 向远端发送数据。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public ArraySegment<byte>? Send(byte[] data, int offset, int count, MessageArriveArgs replyToMessage = null, Action<ArraySegment<byte>> replyCallback = null, bool block = false) {
            return Send(data, offset, count, replyToMessage, replyCallback, block, MessageFlags.None);
        }

        /// <summary>
        /// 请求服务器进行加密。如果加密失败，连接将被断开，且抛出异常。
        /// </summary>
        public void RequestEncryption() {
            try {
                // 请求服务器 RSA 公钥
                var ret = Send(new byte[] { (byte)InternalCalls.RequestRSAPublicKey }, 0, 1, block: true, extraFlags: MessageFlags.InternalCalls);
                var rsa = new System.Security.Cryptography.RSACryptoServiceProvider();
                rsa.PersistKeyInCsp = false;

                var keyxml = Encoding.UTF8.GetString(ret.Value.Array, ret.Value.Offset, ret.Value.Count);
                rsa.FromXmlString(keyxml);

                // 随机产生一个 AES 密钥和 IV，并发送给服务器
                var aesKeyAndIV = RandomHelper.NextStrongRandomByteArray(32, false);

                // 将 AES 密钥信息通过 RSA 加密
                var keyEncrypted = new byte[] { (byte)InternalCalls.SendAESKeysViaRSA }.Concat(rsa.Encrypt(aesKeyAndIV, true)).ToArray();

                ret = Send(keyEncrypted, 0, keyEncrypted.Length, block: true, extraFlags: MessageFlags.InternalCalls);

                // 判断返回值是否成功
                if (ret.HasValue && ret.Value.Count == 1 && ret.Value.Array[ret.Value.Offset] == 0) {
                    AesKey = aesKeyAndIV.Take(16).ToArray();
                    AesIV = aesKeyAndIV.Skip(16).ToArray();
                    IsEncrypted = true;
                    return;
                }

                closeSocket();
                throw new Exception("请求加密失败");
            }
            catch (Exception ex) {
                throw new Exception("请求加密失败", ex);
            }
        }


        private void startReceive() {
            try {
                var receivedData = new byte[0];
                var buffer = new byte[BufferSize];
                while (true) {
                    var count = socket.Receive(buffer);
                    if (count == 0) {
                        // 接收错误时，关闭连接
                        closeSocket();

                        return;
                    }

                    // 获取数据，将数据拼接到原有数据中
                    receivedData = receivedData.Concat(buffer.Take(count)).ToArray();

                    // 处理数据
                    while (true) {
                        if (receivedData.Length < sizeof(int)) break;

                        // 获取数据长度
                        var length = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(receivedData, 0));

                        if (receivedData.Length < sizeof(int) + length)
                            break;

                        // 处理数据
                        try {
                            processData(receivedData, sizeof(int), length);
                        }
                        catch (Exception ex) {
                            closeSocket();

                            return;
                        }

                        // 删除数据
                        receivedData = receivedData.Skip(sizeof(int) + length).ToArray();
                    }
                }
            }
            catch {
                // 接收错误时，关闭连接
                closeSocket();
            }
        }

        /// <summary>
        /// 获取或设置从远端获取回复的最大超时时间。
        /// </summary>
        public TimeSpan ReceiveReplyTimeout { get; set; } = TimeSpan.FromSeconds(60);
        /// <summary>
        /// 返回或设置客户端是否自动通过 RSA 请求随机的 AES-128 加密。
        /// </summary>
        public bool AutoRequestEncryption { get; set; } = false;


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

        /// <summary>
        /// 初始化加解密相关参数。
        /// </summary>
        internal void initEncryption() {
            if (aes == null && IsEncrypted) {
                aes = System.Security.Cryptography.Aes.Create();
                aes.KeySize = 128;
                aes.IV = AesIV;
                aes.Key = AesKey;

                aesEncoder = aes.CreateEncryptor();
                aesDecoder = aes.CreateDecryptor();
            }
        }
    

        internal System.Security.Cryptography.Aes aes;
        internal System.Security.Cryptography.ICryptoTransform aesEncoder;
        internal System.Security.Cryptography.ICryptoTransform aesDecoder;

        private void processData(byte[] receivedData, int offset, int length) {
            // 获取消息的基本属性
            var flags = (MessageFlags)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(receivedData, offset));
            var mid = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(receivedData, offset + sizeof(short)));

            // 将正文消息封装给客户端处理，并允许客户端回复
            var buffer = new byte[length - 4];
            Array.Copy(receivedData, offset + 4, buffer, 0, length - 4);

            if (IsEncrypted) {
                // 如果此消息被加密，则解密
                initEncryption();
                buffer = aes.CreateDecryptor().TransformFinalBlock(buffer, 0, buffer.Length);
            }

            if ((flags & MessageFlags.Request) == MessageFlags.Request) {
                // 对于请求，调用用户的相关功能
                var arg = new MessageArriveArgs(null, mid, buffer, 0, buffer.Length);
                if (MessageArrive != null) {
                    ThreadPool.QueueUserWorkItem(o => {
                        waitHandle.WaitOne();

                        try {
                            MessageArrive(this, arg);
                        }
                        catch {

                        }
                        finally {
                            waitHandle.Set();
                        }
                    });

                }
            }
            else {
                // 对于回复消息，触发相关的事件
                if (waitHandles[mid] != null) {
                    replyArrays[mid] = new ArraySegment<byte>(buffer);
                    lock (waitHandles[mid])
                        Monitor.Pulse(waitHandles[mid]);
                }
            }
        }

        private AutoResetEvent waitHandle = new AutoResetEvent(true);

        public event EventHandler<MessageArriveArgs> MessageArrive;

        internal short currentMessageID = 0;

        internal object[] waitHandles = new object[short.MaxValue];
        internal ArraySegment<byte>[] replyArrays = new ArraySegment<byte>[short.MaxValue];
       
        /// <summary>
        /// 获取或设置缓冲区大小。
        /// </summary>
        public int BufferSize { get; set; } = 40 * 1024;
        private System.Net.Sockets.Socket socket = null;
        private string hostname;
        private int port;
    }
}
