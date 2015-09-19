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

namespace libTest {
    class Program {
        static void Main(string[] args) {
            Task.Run(async () =>
            {
                await test();
                // Do any async anything you need here without worry
            }).Wait();
        }

        private async static Task test() {
            var client = await RedisBoost.RedisClient.ConnectAsync("127.0.0.1", 6379);

            for (var i = 0; i < 60; ++i) {
                var ret = await client.LIndexAsync("fastlog", 0);
                var s = ret.As<string>();
                Console.WriteLine(s);

                Thread.Sleep(1000);
            }
        }
    }
}
