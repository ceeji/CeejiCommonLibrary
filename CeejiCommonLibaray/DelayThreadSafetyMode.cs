using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji {
    // 摘要: 
    //     指定 Ceeji.Delayed 实例如何同步多个线程间的访问。
    public enum DelayThreadSafetyMode {
        /// <summary>
        /// Ceeji.Delayed 实例不是线程安全的；如果从多个线程访问该实例，则其行为不确定。 仅应在高性能至关重要并且保证决不会从多个线程初始化 Ceeji.Delayed 实例时才使用该模式。 如果使用指定初始化方法（valueFactory 参数）的 Ceeji.Delayed 构造函数，并且如果此初始化方法在您首次调用
        ///     Ceeji.Delayed.Value 属性时引发了一个异常（或无法处理异常），则会缓存该异常并在随后调用 Ceeji.Delayed.Value
        ///     属性时再次引发该异常。 如果您使用不指定初始化方法的 Ceeji.Delayed 构造函数，则不会缓存 T 默认构造函数引发的异常。 在此情况下，对
        ///     Ceeji.Delayed.Value 属性进行后续调用可成功初始化 Ceeji.Delayed 实例。 如果初始化方法递归访问 Ceeji.Delayed
        ///    实例的 Ceeji.Delayed.Value 属性，则引发 System.InvalidOperationException。
        /// </summary>
        None = 0,
        /// <summary>
        /// 当多个线程尝试同时初始化一个 Ceeji.Delayed 实例时，允许所有线程都运行初始化方法（如果没有初始化方法，则为默认构造函数）。 完成初始化的第一个线程设置
        /// Ceeji.Delayed 实例的值。 该值将返回给同时运行初始化方法的所有其他线程，除非该初始化方法对这些线程引发异常。 争用线程创建的任何
        ///  T 实例都将被丢弃。 如果初始化方法对任何线程引发异常，则该异常会从该线程上的 Ceeji.Delayed.Value 属性传播出去。 不缓存该异常。
        ///  Ceeji.Delayed.IsValueCreated 属性的值仍然为 false，并且随后通过其中引发异常的线程或通过其他线程对 Ceeji.Delayed.Value
        ///  属性的调用会导致初始化方法再次运行。 如果初始化方法递归访问 Ceeji.Delayed 实例的 Ceeji.Delayed.Value 属性，则不会引发异常。
        /// </summary>
        PublicationOnly = 1,
        /// <summary>
        ///     使用锁来确保只有一个线程可以在线程安全的方式下初始化 Ceeji.Delayed 实例。 如果初始化方法（如果没有初始化方法，则为默认构造函数）在内部使用锁，则可能会发生死锁。
        ///     如果使用指定初始化方法（valueFactory 参数）的 Ceeji.Delayed 构造函数，并且如果此初始化方法在您首次调用 Ceeji.Delayed.Value
        ///     属性时引发了一个异常（或无法处理异常），则会缓存该异常并在随后调用 Ceeji.Delayed.Value 属性时再次引发该异常。 如果您使用不指定初始化方法的
        ///     Ceeji.Delayed 构造函数，则不会缓存 T 默认构造函数引发的异常。 在此情况下，对 Ceeji.Delayed.Value 属性进行后续调用可成功初始化
        ///     Ceeji.Delayed 实例。 如果初始化方法递归访问 Ceeji.Delayed 实例的 Ceeji.Delayed.Value 属性，则引发
        ///     System.InvalidOperationException。
        /// </summary>
        ExecutionAndPublication = 2,
    }
}
