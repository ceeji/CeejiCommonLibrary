using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Ceeji.Network
{
    public interface IEndPointListener
    {
        /// <summary>
        /// 开始监听指定的终结点。
        /// </summary>
        void Start();
        /// <summary>
        /// 停止监听指定的终结点。
        /// </summary>
        void Stop();
        /// <summary>
        /// 返回目前的监听状态。
        /// </summary>
        EndPointListenStatus Status { get; }
    }

    /// <summary>
    /// 枚举目前的监听状态
    /// </summary>
    public enum EndPointListenStatus {
        /// <summary>
        /// 正在监听
        /// </summary>
        Listening,
        /// <summary>
        /// 已停止
        /// </summary>
        Stop
    }

    public class TcpEndPointListener : IEndPointListener {
        /// <summary>
        /// 绑定终结点到指定的 IP 和端口号上。
        /// </summary>
        /// <param name="ip">要绑定的 IP 地址，或 null 绑定所有 IP 地址。</param>
        /// <param name="port">要绑定的端口号。</param>
        public TcpEndPointListener(IPAddress ip, int port) {
            Status = EndPointListenStatus.Stop;

            if (ip == null) {
                this.IPAddress = System.Net.IPAddress.Any;
            }
            else {
                this.IPAddress = ip;
            }
            this.Port = port;
            mListener = new TcpListener(this.IPAddress, this.Port);
        }

        /// <summary>
        /// 开始监听指定的终结点。
        /// </summary>
        public void Start() {
            if (this.ConnectionBegin == null) {
                throw new ArgumentNullException("必须绑定 ConnectionBegin 事件");
            }

            mListener.Start();
            mListenThread = new Thread(listenLoop);
            mListenThread.Start();
            Status = EndPointListenStatus.Listening;
        }

        /// <summary>
        /// 停止监听指定的终结点。
        /// </summary>
        public void Stop() {
            mListenThread.Abort();
            mListenThread.Join();
            mListener.Stop();
        }

        /// <summary>
        /// 获取对象绑定到的 IP 地址。
        /// </summary>
        public IPAddress IPAddress { get; private set;  }

        /// <summary>
        /// 获取对象绑定到的端口号。
        /// </summary>
        public int Port { get; private set; }

        private void listenLoop() {
            do {
                var socket = mListener.AcceptSocket();
                ConnectionBegin(this, new TcpConnectionBeginEventArgs(socket));
            }
            while (true);
        }

        public event EventHandler<TcpConnectionBeginEventArgs> ConnectionBegin;

        private TcpListener mListener;
        private Thread mListenThread;

        /// <summary>
        /// 返回目前的监听状态。
        /// </summary>
        public EndPointListenStatus Status { get; private set; }
    }
}
