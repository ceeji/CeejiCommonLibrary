using Ceeji.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace libTest {
    class ObjectPoolTest {
        public static void TestPool() {
            var time = 2000000;
            ObjectPool<byte[]> pool = new ObjectPool<byte[]>(() => new byte[1024], 10, 80);
            pool.MaxCountExceed += pool_MaxCountExceed;

            TimedMethod.RunMethod("Loop Fill With Pool", () => {
                Parallel.For(0, time, i => {
                    using (var buffer = pool.Get()) {
                        using (var buffer1 = pool.Get()) {
                            using (var buffer2 = pool.Get()) {
                                using (var buffer3 = pool.Get()) {
                                    using (var buffer4 = pool.Get()) {
                                        using (var buffer5 = pool.Get()) {
                                            using (var buffer6 = pool.Get()) {
                                                using (var buffer7 = pool.Get()) {
                                                    using (var buffer8 = pool.Get()) {
                                                        using (var buffer9 = pool.Get()) {
                                                            using (var buffer10 = pool.Get()) {
                                                                using (var buffer11 = pool.Get()) {
                                                                    using (var buffer12 = pool.Get()) {
                                                                        using (var buffer13 = pool.Get()) {
                                                                            buffer.Value.Fill<byte>(1);
                                                                            buffer.Value.Fill<byte>(1);
                                                                            buffer.Value.Fill<byte>(1);
                                                                            buffer.Value.Fill<byte>(1);
                                                                            buffer.Value.Fill<byte>(1);
                                                                            buffer.Value.Fill<byte>(1);
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            });

            TimedMethod.RunMethod("Loop Fill Without Pool", () => {
                Parallel.For(0, time, i => {
                    var buffer = new byte[1024];
                    buffer.Fill<byte>(1);
                });
            });
        }

        static void pool_MaxCountExceed(object sender, EventArgs e) {
            Console.WriteLine("Pool Full!");
        }
    }

    class TimedMethod {
        public static void RunMethod(string action, Action method) {
            var watch = Stopwatch.StartNew();
            method();
            Console.WriteLine(action + ", " + watch.ElapsedMilliseconds.ToString() + " ms");
            watch.Stop();
        }
    }
}
