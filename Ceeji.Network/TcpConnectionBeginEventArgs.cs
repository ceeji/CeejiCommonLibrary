using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Ceeji.Network {
    public class TcpConnectionBeginEventArgs : EventArgs {
        /// <summary>
        /// 获取 ConnectionBeginEventArgs 的新实例。
        /// </summary>
        public TcpConnectionBeginEventArgs(Socket connectionObject) {
            this.ConnectionObject = connectionObject;
        }

        /// <summary>
        /// 获取连接后得到的对象。
        /// </summary>
        public Socket ConnectionObject { get; internal set; }
    }
}
