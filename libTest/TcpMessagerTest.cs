using Ceeji.Network;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace libTest {
    class TcpMessagerTest {
        public static void TcpMessagerTester() {
            var listener = new TcpEndPointListener(null, 8080);
            var messager = new TcpMessager<SessionData>(100, data => {
                data.Messager = null;
                data.Buffer = null;
                data.Connection = null;
            }, listener);
            listener.Start();

            messager.IncomingSessionStarted += messager_IncomingConnectionEstablished;
            messager.DataReceived += messager_DataReceived;
        }

        static void messager_DataReceived(Messager<SessionData> sender, Session<SessionData> e, ArraySegment<byte> dataSegment) {
            var sr = new StreamReader(new MemoryStream(dataSegment.Array, dataSegment.Offset, dataSegment.Count));
            var data = sr.ReadToEnd();
            var binary = Encoding.UTF8.GetBytes(data.Replace("Host: 127.0.0.1:8080", "Host: ceeji.net"));
            dataSegment = new ArraySegment<byte>(binary, 0, binary.Length);

            lock (e.Data) {
                if (e.Data.Buffer == null) {
                    e.Data.Buffer = new byte[dataSegment.Count];
                    Array.Copy(dataSegment.Array, dataSegment.Offset, e.Data.Buffer, 0, dataSegment.Count);
                }
                else {
                    var newBuf = new byte[e.Data.Buffer.Length + dataSegment.Count];
                    e.Data.Buffer.CopyTo(newBuf, 0);
                    Array.Copy(dataSegment.Array, dataSegment.Offset, newBuf, e.Data.Buffer.Length, dataSegment.Count);
                    e.Data.Buffer = newBuf;
                }
            }

            /*var ret = @"HTTP/1.1 200 OK
Server: nginx/1.8.0
Date: Fri, 26 Jun 2015 07:34:02 GMT
Content-Type: text/html; charset=utf-8
Content-Length: 40
Connection: keep-alive
Cache-Control: private

<!DOCTYPE html><html>hello, world</html>";

            e.Send(Encoding.Default.GetBytes(ret));

            return;*/
            Action sendAction = () => {
                e.Data.Messager.Send(e.Data.Connection, new ArraySegment<byte>(e.Data.Buffer, 0, e.Data.Buffer.Length));
                e.Data.Buffer = null;
            };

            if (e.Data.Messager == null || e.Data.Messager.Status != MessagerStatus.Connected) {
                // 还没有初始化完成，先缓冲
                
                Console.WriteLine("!!");
            }
            else {
                sendAction();
            }
        }

        static void messager_IncomingConnectionEstablished(Messager<SessionData> sender, Session<SessionData> e) {
            Console.WriteLine("New Connection, Time = {0}, EndPoint = {1}", e.BeginTime, e.EndPoint);
            e.Data.Messager = new TcpMessager<SessionData2>();
            e.Data.Messager.DataReceived += (m, conn, data) => {
                sender.Send(e, data);
            };
            e.Data.Messager.SendError += Messager_SendError;
            e.Data.Messager.SessionEnded += (m, conn) => {
                Console.WriteLine("Connection Closed");
                e.Data.Messager = null;
                e.Close();
            };
            e.Data.Messager.Connect(new IPEndPoint(System.Net.IPAddress.Parse("121.201.7.41"), 80), (m, conn, ex) => {
                if (conn != null) {
                    e.Data.Connection = conn;

                    lock (e.Data) {
                        if (e.Data.Buffer != null) {
                            conn.Send(new ArraySegment<byte>(e.Data.Buffer, 0, e.Data.Buffer.Length));
                        }
                    }
                }
            });
        }

        static void Messager_SendError(Messager<SessionData2> sender, Session<SessionData2> e) {
            Console.WriteLine("Sending Error!");
        }

    }

    class SessionData2 {
    }

    class SessionData {
        public TcpMessager<SessionData2> Messager;
        public Session<SessionData2> Connection;
        public byte[] Buffer;
    }
}
