using Ceeji.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace libTest {
    public static class TcpServerTest {
        public static void StartServer() {
            server = new TcpServer();
            server.ListenEndPoint = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 2000);
            server.Start();

            server.MessageArrive += Server_MessageArrive;
        }

        private static void Server_MessageArrive(object sender, MessageArriveArgs e) {
            Console.WriteLine("[S] Received: " + Encoding.UTF8.GetString(e.Content.Array, e.Content.Offset, e.Content.Count));

            // 如果是收到的第一个消息，则开始加密
            if (!e.Session.IsEncrypted) {
                var splited = Encoding.UTF8.GetString(e.Content.Array, e.Content.Offset, e.Content.Count).Split('*');
                e.Session.AesIV = Convert.FromBase64String(splited[0]);
                e.Session.AesKey = Convert.FromBase64String(splited[1]);
                e.Session.IsEncrypted = true;

                e.Session.Send(e.Content.Array, 0, e.Content.Count, replyToMessage: e);
            }
            else {
                var p = int.Parse(Encoding.UTF8.GetString(e.Content.Array, e.Content.Offset, e.Content.Count));

                var dataHello = Encoding.UTF8.GetBytes((p * p).ToString());

                e.Session.Send(dataHello, 0, dataHello.Length, replyToMessage: e);
            }
        }

        public static TcpServer server;
    }

    public class TcpClientTest {
        public void StartClient(byte[] iv, byte[] key) {
            client = new TcpClient("127.0.0.1", 2000);
            client.AutoRequestEncryption = true;
            client.Connect();
            var r = new Random();

            var dataKeys = Encoding.UTF8.GetBytes(Convert.ToBase64String(iv) + "*" + Convert.ToBase64String(key));


            //client.Send(dataKeys, 0, dataKeys.Length, block: true);
            //client.IsEncrypted = true;
            //client.AesIV = iv;
            //client.AesKey = key;

            Task.Factory.StartNew(() => {
                while (true) {
                    var data = r.Next(1, 1000);
                    var dataHello = Encoding.UTF8.GetBytes(data.ToString());

                    client.Send(dataHello, 0, dataHello.Length, replyCallback: dataReplied => {
                        Console.WriteLine("[C] Received Reply: " + Encoding.UTF8.GetString(dataReplied.Array, dataReplied.Offset, dataReplied.Count));
                        var reply = int.Parse(Encoding.UTF8.GetString(dataReplied.Array, dataReplied.Offset, dataReplied.Count));
                        if (reply != data * data)
                            throw new Exception("结果不正确");
                    });

                    client.Send(dataHello, 0, dataHello.Length, block: true);

                    //Thread.Sleep(5);
                }
            });

            client.MessageArrive += Client_MessageArrive;
        }

        private void Client_MessageArrive(object sender, MessageArriveArgs e) {
            Console.WriteLine("[C] Received: " + Encoding.UTF8.GetString(e.Content.Array, e.Content.Offset, e.Content.Count));

        }

        TcpClient client;
    }
}
