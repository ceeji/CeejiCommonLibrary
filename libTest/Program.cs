using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ceeji.Data;
using Ceeji;
using Ceeji.Log;
using Ceeji.Testing.Configuration;
using System.Xml.Serialization;
using Ceeji.Testing;
using System.Threading;
using Ceeji.Data.BinaryPackage;
using System.Diagnostics;
using Ceeji.FastWeb;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using Ceeji.DirectCall;
using System.Security.Cryptography;
using Ceeji.Imaging;

namespace libTest {
    class Program {
        static void Main(string[] args) {
            byte[] f = WebPHelper.Encode(File.ReadAllBytes(@"C:\t.jpg"), 54);

            Console.WriteLine(f.Length);

            return;

            Aes aes = Aes.Create();
            aes.KeySize = 128;
            aes.Key = new byte[128 / 8];
            RandomNumberGenerator.Create().GetNonZeroBytes(aes.Key);
            aes.CreateEncryptor().TransformFinalBlock(Encoding.UTF8.GetBytes("hello"), 0, 5);

            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(1024);
            rsa.PersistKeyInCsp = false;

            //rsa.
            var eee = rsa.Encrypt(Encoding.UTF8.GetBytes("hello"), true);
            var stop = Stopwatch.StartNew();
            for (var i = 0; i < 50; ++i) {
                var ee = rsa.Encrypt(Encoding.UTF8.GetBytes("helloho"), true);
            }

            Console.WriteLine(stop.ElapsedMilliseconds);
            var e = rsa.Encrypt(Encoding.UTF8.GetBytes("hello"), true);
            stop.Restart();
            var s = Encoding.UTF8.GetString(rsa.Decrypt(e, true));
            Console.WriteLine(stop.ElapsedMilliseconds);
            //RandomNumberGenerator.Create().GetBytes(aes.Key);
            // return;

            TcpServerTest.StartServer();

            Thread.Sleep(1000);

            for (var i = 0; i < 10; ++i) {
                TcpClientTest test = new TcpClientTest();
                test.StartClient(aes.IV, aes.Key);

            }

            Console.ReadKey();

            return;
            DirectCaller caller = new DirectCaller();
            caller.Register(new test());
            var ms = new MemoryStream();
            var bw = new BinaryWriter(ms, Encoding.UTF8);
            caller.MakeMethodCall("test", "test1", bw);

            // 压入参数
            caller.WriteValue(typeof(string), null, bw);
            caller.WriteValue(typeof(int), 5, bw);
            caller.WriteValue(typeof(Guid?), null, bw);

            ms.Position = 0;
            var br = new BinaryReader(ms, Encoding.UTF8);
            var bw2 = new BinaryWriter(new MemoryStream(), Encoding.UTF8);

            caller.Call(br, bw2);

            bw2.BaseStream.Position = 0;
            var ret = caller.GetReturnValue<string>(new BinaryReader(bw2.BaseStream));
        }

        private async static Task test() {
            //var log = new Ceeji.Log.EventLogger(new LogWriter[] { new Ceeji.Log.Redis.RedisLogger("localhost", 0) });
            // log.Debug("sd");

        }
    }

    class test {
        public string test1(string arg1, int arg2, Guid? arg3) {
            Console.WriteLine(arg1);


            throw new Exception("haha");

            return "5";
        }
    }
}
