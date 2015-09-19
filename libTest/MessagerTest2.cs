using Ceeji.Data;
using Ceeji.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace libTest {
    class MessagerTest2 {
        public static void Test() {
            var listener = new TcpEndPointListener(null, 8080);
            var messager1 = new TcpMessager<SessionData3>(listener);
            messager1.DataReceived += messager1_DataReceived;
            listener.Start();

            var task = new ThreadStart(() => {
                var messager2 = new TcpMessager<SessionData3>();
                messager2.DataReceived += (sender, session, data) => {
                    var str = Encoding.Default.GetString(data.Array, data.Offset, data.Count);
                    Console.WriteLine("[messager2_DataReceived], " + str);

                    int result = int.Parse(str);
                    if (result != session.Data.a + session.Data.b) {
                        Console.WriteLine("Wrong!");
                    }
                    sendAddOrClose(session);
                };

                messager2.SessionEnded += (sender, session) => {
                    createClient(sender);
                };

                createClient(messager2);
            });

            new Thread(task).Start();
        }

        static void createClient(Messager<SessionData3> messager) {
            Console.WriteLine("createClient");
            messager.Connect(new Ceeji.Network.IPEndPoint(IPAddress.Loopback, 8080), (sender, session, ex) => {
                if (session != null) {
                    sendAddOrClose(session);
                }
            });
        }

        static void sendAddOrClose(Session<SessionData3> session) {
            Console.WriteLine("sendAddOrClose");
            if (RandomHelper.NextRandomInt(0, 6) == 2) {
                session.Close();
            }
            else {
                session.Data.a = RandomHelper.NextRandomInt(0, 1000000);
                session.Data.b = RandomHelper.NextRandomInt(0, 10000000);

                var sent = session.Data.a + "," + session.Data.b;
                session.Send(Encoding.Default.GetBytes(sent));
                Console.WriteLine("[messager2_DataSend], " + sent);
                Console.OpenStandardOutput().Flush();
            }
        }

        static void messager1_DataReceived(Messager<SessionData3> sender, Session<SessionData3> e, ArraySegment<byte> dataSegment) {
            var data = Encoding.Default.GetString(dataSegment.Array, dataSegment.Offset, dataSegment.Count);
            Console.WriteLine("[messager1_DataReceived], " + data);

            var num = data.Split(',').Select(x => int.Parse(x)).ToArray();
            Console.WriteLine("[messager1_DataSend], " + (num[0] + num[1]).ToString());
            e.Send((num[0] + num[1]).ToString());
        }
    }
}
