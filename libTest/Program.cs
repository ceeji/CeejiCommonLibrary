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
            Console.WriteLine(1);
            var client = new Ceeji.Caching.RedisBoostCachingProvider("localhost", 6379);

            Console.WriteLine(2);
            await client.SetAddAsync("settest", "1", "2", "3");
            var count = await client.SetLengthAsync("settest");
            Console.WriteLine(3);
            var members = await client.SetMembersAsync("settest");
            Console.WriteLine(4);
            Console.WriteLine($"{count}, {string.Join(",", members)}");
        }
    }
}
