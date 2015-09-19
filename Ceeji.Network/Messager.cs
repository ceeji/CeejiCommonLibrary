using Ceeji.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ceeji.Network {
    /// <summary>
    /// 代表可封装底层可靠连接的信使。它具有如下特征：全双工的双向通信；具有连接状态。
    /// </summary>
    /// <typeparam name="TData">数据的类型，对于每一个连接（会话），这个类型只有一个实例，可用来存储交互过程中所需要的数据。并且，为提高性能，系统会重用已关闭连接的此类型的实例。</typeparam>
    public abstract class Messager<TData> {
        /// <summary>
        /// 在会话成功建立之后触发
        /// </summary>
        public event MessagerEventHandler<TData, Session<TData>> IncomingSessionStarted;
        /// <summary>
        /// 在会话结束时触发
        /// </summary>
        public event MessagerEventHandler<TData, Session<TData>> SessionEnded;
        /// <summary>
        /// 在接收到数据时触发
        /// </summary>
        public event MessagerDataEventHandler<TData, Session<TData>> DataReceived;
        /// <summary>
        /// 在发送数据出错时触发
        /// </summary>
        public event MessagerEventHandler<TData, Session<TData>> SendError;
        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">要发送的数组片段。</param>
        /// <param name="conn">要发送数据的连接。</param>
        public void Send(Session<TData> conn, ArraySegment<byte> data) {
            this.OnSend(conn, data.Array, data.Offset, data.Count);
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">要发送的数组。</param>
        /// <param name="conn">要发送数据。</param>
        /// <param name="count">数据的长度。</param>
        /// <param name="offset">数据的偏移量。</param>
        public void Send(Session<TData> conn, byte[] data, int offset, int count) {
            this.OnSend(conn, data, offset, count);
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">要发送的数组片段。</param>
        /// <param name="conn">要发送数据的连接。</param>
        public void Send(Session<TData> conn, byte[] data) {
            this.OnSend(conn, data, 0, data.Length);
        }

        /// <summary>
        /// 异步发送数据
        /// </summary>
        /// <param name="data">要发送的数组片段。</param>
        /// <param name="conn">要发送数据的连接。</param>
        /// <param name="encoding">数据所采用的编码，默认为 UTF-8。</param>
        public void Send(Session<TData> conn, string data, Encoding encoding = null) {
            Send(conn, (encoding ?? Encoding.UTF8).GetBytes(data));
        }

        /// <summary>
        /// 异步建立连接，此函数不会阻塞。调用后，根据调用结果，在 callback 委托中进行下一步操作
        /// </summary>
        /// <param name="data"></param>
        public void Connect(EndPoint endPoint, MessagerConnectionEventHandler<TData, Exception> eventHandler) {
            this.OnConnect(endPoint, eventHandler);
        }
        /// <summary>
        /// 异步关闭会话
        /// </summary>
        /// <param name="data"></param>
        public void CloseSession(Session<TData> session) {
            this.OnCloseSession(session);
        }
        /// <summary>
        /// 在派生类中，发送数据
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="data"></param>
        /// <param name="pos"></param>
        /// <param name="count"></param>
        protected abstract void OnSend(Session<TData> conn, byte[] data, int pos, int count);
        /// <summary>
        /// 在派生类中，连接到远端（异步）
        /// </summary>
        /// <param name="endPoint"></param>
        protected abstract void OnConnect(EndPoint endPoint, MessagerConnectionEventHandler<TData, Exception> eventHandler);

        /// <summary>
        /// 在派生类中，连接到远端（异步）
        /// </summary>
        /// <param name="endPoint"></param>
        protected abstract void OnCloseSession(Session<TData> session);


        /// <summary>
        /// 返回信使的当前状态。
        /// </summary>
        public virtual MessagerStatus Status { get; protected set; }

        protected void OnSendError(Session<TData> conn) {
            if (this.SendError != null)
                this.SendError(this, conn);
        }

        protected void OnIncomingConnectionEstablished(Session<TData> conn) {
            if (this.IncomingSessionStarted != null) {
                this.IncomingSessionStarted(this, conn);
            }
        }

        protected void OnDataReceived(Session<TData> conn, byte[] data, int pos, int count) {
            if (this.DataReceived != null) {
                this.DataReceived(this, conn, new ArraySegment<byte>(data, pos, count));
            }
        }

        protected void OnConnectionClosed(Session<TData> conn) {
            if (this.SessionEnded != null)
                this.SessionEnded(this, conn);
        }


    }

    /// <summary>
    /// 代表处理信使消息的通用委托。
    /// </summary>
    /// <typeparam name="TMessager"></typeparam>
    /// <typeparam name="TParam"></typeparam>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    public delegate void MessagerEventHandler<TMessager, TParam>(Messager<TMessager> sender, TParam e);

    /// <summary>
    /// 代表处理信使消息的通用委托。请注意，此委托参数中 dataSegment 引用的有效期仅限于委托本身调用结束之前。
    /// </summary>
    /// <typeparam name="TMessager"></typeparam>
    /// <typeparam name="TParam"></typeparam>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <param name="dataSegment">指示将被回收利用的接收到的数据块</param>
    public delegate void MessagerDataEventHandler<TMessager, TParam>(Messager<TMessager> sender, TParam e, ArraySegment<byte> dataSegment);

    /// <summary>
    /// 代表处理信使消息的通用委托。请注意，此委托参数中 dataSegment 引用的有效期仅限于委托本身调用结束之前。
    /// </summary>
    /// <typeparam name="TMessager"></typeparam>
    /// <typeparam name="TParam"></typeparam>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    /// <param name="dataSegment">指示将被回收利用的接收到的数据块</param>
    public delegate void MessagerConnectionEventHandler<TMessager, TParam>(Messager<TMessager> sender, Session<TMessager> conn, TParam e);
    
    /// <summary>
    /// 代表信使的状态。
    /// </summary>
    public enum MessagerStatus {
        /// <summary>
        /// 代表信使已经连接到远端。
        /// </summary>
        Connected,
        /// <summary>
        /// 代表信使已经关闭。
        /// </summary>
        Closed
    }

    /// <summary>
    /// 代表一次特定的连接会话。此类可以被重用。
    /// </summary>
    public class Session<TData> {
        public Session() {
            this.Status = ConnectionStatus.Invalid;
        }

        /// <summary>
        /// 发送数据到连接的另一端。
        /// </summary>
        /// <param name="data"></param>
        public void Send(ArraySegment<byte> data) {
            if (invalidSend()) return; // 如果连接已经不存在，直接抛弃。
            this.Messager.Send(this, data);
        }

        private bool invalidSend() {
            return this.Messager == null || this.Status != ConnectionStatus.Running;
        }

        /// <summary>
        /// 发送数据到连接的另一端。
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data) {
            if (invalidSend()) return; // 如果连接已经不存在，直接抛弃。
            Send(data, 0, data.Length);
        }

        /// <summary>
        /// 发送数据到连接的另一端。
        /// </summary>
        /// <param name="data"></param>
        /// <param name="encoding">数据所用的编码，默认为 UTF-8 编码</param>
        public void Send(string data, Encoding encoding = null) {
            if (invalidSend()) return; // 如果连接已经不存在，直接抛弃。
            Send((encoding ?? Encoding.UTF8).GetBytes(data));
        }

        /// <summary>
        /// 发送数据到连接的另一端。
        /// </summary>
        /// <param name="data"></param>
        public void Send(byte[] data, int offset, int count) {
            if (invalidSend()) return; // 如果连接已经不存在，直接抛弃。
            this.Messager.Send(this, new ArraySegment<byte>(data, offset, count));
        }

        /// <summary>
        /// 关闭和远端的连接。
        /// </summary>
        public void Close() {
            try {
                if (this.Messager != null) {
                    this.Messager.CloseSession(this);
                }
            }
            catch { }
        }

        /// <summary>
        /// 此连接绑定到的对方终结点。
        /// </summary>
        public EndPoint EndPoint { get; internal set; }

        /// <summary>
        /// 此连接绑定到的信使。
        /// </summary>
        public Messager<TData> Messager { get; internal set; }

        /// <summary>
        /// 此连接绑定到的会话数据。
        /// </summary>
        public TData Data { get; internal set; }

        /// <summary>
        /// 此连接建立的时间。
        /// </summary>
        public DateTime BeginTime { get; internal set; }

        /// <summary>
        /// 此连接的状态。
        /// </summary>
        public ConnectionStatus Status {
            get {
                return (ConnectionStatus)mStatus;
            }
            internal set {
                Interlocked.Exchange(ref mStatus, (int)value);
            }
        }

        private volatile int mStatus = (int)ConnectionStatus.Invalid;
    }

    public enum ConnectionStatus {
        /// <summary>
        /// 标示连接已经关闭
        /// </summary>
        Closed,
        /// <summary>
        /// 标示连接正在运行
        /// </summary>
        Running,
        /// <summary>
        /// 标示此对象在池中待用，没有实际数据
        /// </summary>
        Invalid
    }

    /// <summary>
    /// 代表网络终结点。
    /// </summary>
    public abstract class EndPoint {
    };
}
