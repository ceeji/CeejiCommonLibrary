using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Ceeji.Data {
    /// <summary>
    /// 代表一个简单的，线程安全的对象池对象。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ObjectPool<T> : IDisposable where T : class {
        #region Constructors
        /// <summary>
        /// 创建 <see cref="ObjectPool{T}"/> 的新实例。此实例具有默认的最小、最大池中成员数量。
        /// </summary>
        /// <param name="createFactory">一个委托，用来创建新的对象。</param>
        public ObjectPool(Func<T> createFactory)
            : this(createFactory, ObjectPool<T>.DefaultMinCount, ObjectPool<T>.DefaultMaxCount) {
        }



        /// <summary>
        /// 创建 <see cref="ObjectPool{T}"/> 的新实例。
        /// </summary>
        /// <param name="createFactory">一个委托，用来创建新的对象。</param>
        /// <param name="minCount">对象池中对象的最少数目。系统将维持对象池中至少存在这样数目的对象。</param>
        /// <param name="maxCount">对象池中对象的最多数目。系统将维持对象池中至多存在这样数目的对象。</param>
        public ObjectPool(Func<T> createFactory, int minCount, int maxCount) : this(createFactory, null, minCount, maxCount) {
        }

        /// <summary>
        /// 创建 <see cref="ObjectPool{T}"/> 的新实例。
        /// </summary>
        /// <param name="createFactory">一个委托，用来创建新的对象。</param>
        /// <param name="minCount">对象池中对象的最少数目。系统将维持对象池中至少存在这样数目的对象。</param>
        /// <param name="maxCount">对象池中对象的最多数目。系统将维持对象池中至多存在这样数目的对象。</param>
        /// <param name="resetFactory">设置重置对象以便重新使用的工厂方法。</param>
        public ObjectPool(Func<T> createFactory, Action<T> resetFactory, int minCount, int maxCount) : this(createFactory, resetFactory, null, minCount, maxCount) {
        }

        /// <summary>
        /// 创建 <see cref="ObjectPool{T}"/> 的新实例。
        /// </summary>
        /// <param name="createFactory">一个委托，用来创建新的对象。</param>
        /// <param name="minCount">对象池中对象的最少数目。系统将维持对象池中至少存在这样数目的对象。</param>
        /// <param name="maxCount">对象池中对象的最多数目。系统将维持对象池中至多存在这样数目的对象。</param>
        /// <param name="resetFactory">设置重置对象以便重新使用的工厂方法。</param>
        /// <param name="disposeFactory">设置彻底释放对象的工厂方法。</param>
        public ObjectPool(Func<T> createFactory, Action<T> resetFactory, Action<T> disposeFactory, int minCount, int maxCount) {
            this.CreateFactory = createFactory;
            this.ResetFactory = resetFactory;
            this.DisposeFactory = disposeFactory;

            init(minCount, maxCount);
        }

        #endregion

        #region Public Members

        /// <summary>
        /// 获取或设置创建对象的工厂方法。
        /// </summary>
        public Func<T> CreateFactory { get; set; }

        /// <summary>
        /// 获取或设置重置对象以便重新使用的工厂方法。
        /// </summary>
        public Action<T> ResetFactory { get; set; }

        /// <summary>
        /// 获取或设置彻底释放对象的工厂方法。
        /// </summary>
        public Action<T> DisposeFactory { get; set; }

        /// <summary>
        /// 当超过最大数量时发生，方便测试时通过此事件了解到需要增加 MaxCount 属性。
        /// </summary>
        public event EventHandler MaxCountExceed;

        /// <summary>
        /// 获取对象池中对象的最少数目。系统将维持对象池中至少存在这样数目的对象。
        /// </summary>
        public int MinCount {
            get {
                return this.mMinCount;
            }
            private set {
                if (value < 0) throw new ArgumentOutOfRangeException("MinCount");
                this.mMinCount = value;
                ensureSize();
            }
        }

        /// <summary>
        /// 获取对象池中对象的最大数目。系统将维持对象池中至多存在这样数目的对象。
        /// </summary>
        public int MaxCount {
            get {
                return this.mMaxCount;
            }
            private set {
                if (value < 0) throw new ArgumentOutOfRangeException("MaxCount");
                this.mMaxCount = value;
            }
        }

        /// <summary>
        /// 获取默认的最少可用对象数量。
        /// </summary>
        public const int DefaultMinCount = 3;
        /// <summary>
        /// 获取默认的最多可用对象数量。
        /// </summary>
        public const int DefaultMaxCount = 400;

        #endregion

        #region Internal Or Private Methods

        private void init(int minCount, int maxCount) {
            mBag = new HashSet<PooledObject<T>>();
            this.MaxCount = maxCount;
            this.MinCount = minCount;
        }

        /// <summary>
        /// 回收对象。
        /// </summary>
        /// <param name="obj"></param>
        internal void recycle(PooledObject<T> obj) {
            if (this.mCountInBag > this.MaxCount) {
                Console.WriteLine("Release Element");
                // 如果包满了，则直接释放
                releaseObject(obj);

                if (MaxCountExceed != null) {
                    // 通知包已满
                    MaxCountExceed(this, EventArgs.Empty);
                }
            }
            else {
                // 否则，入队
                reuseObject(obj);
            }
        }

        private void reuseObject(PooledObject<T> obj) {
            if (this.ResetFactory != null) {
                this.ResetFactory(obj.Value);
            }

            obj.reuse();

            addObject(obj);

            Console.WriteLine("Reuse Element, Now have " + this.mCountInBag + " Elements");
        }

        private void releaseObject(PooledObject<T> obj) {
            if (this.DisposeFactory != null) {
                this.DisposeFactory(obj.Value);
            }

            GC.SuppressFinalize(obj);
            obj.Value = null;
        }

        /// <summary>
        /// 获得一个可用的对象。
        /// </summary>
        /// <returns></returns>
        public PooledObject<T> Get() {
            if (mCountInBag == 0) { // 如果目前没有可用元素
                //Console.WriteLine("Getting New Element, Now have " + this.mCountInBag + " Elements");
                return createObject();
            }

            PooledObject<T> ret = null;
            lock (mLockBag) {
                try {
                    ret = mBag.FirstOrDefault();
                    if (ret != null)
                        mBag.Remove(ret);
                }
                catch { }
            }

            if (ret != null) {
                Interlocked.Decrement(ref this.mCountInBag);
                //Console.WriteLine("Getting Exsits Element, Now have " + this.mCountInBag + " Elements");
                return ret;
            }

            return createObject();
        }

        /// <summary>
        /// 确保数量符合要求。
        /// </summary>
        private void ensureSize() {
            while (mCountInBag < this.MinCount) {
                // 如果数量不足，增加
                addObject(null);
            }
        }

        private PooledObject<T> createObject() {
            // 创建成员
            var obj = new PooledObject<T>(CreateFactory(), this);
            // Console.WriteLine("Create, Now Count = " + this.mCountInBag);
            return obj;
        }

        private void addObject(PooledObject<T> obj) {
            if (obj == null)
                obj = createObject();
            else {
                Console.WriteLine("Reuse!");
            }

            // 将成员入队
            lock (mLockBag)
                mBag.Add(obj);

            // 增加数量
            Interlocked.Increment(ref mCountInBag);

            //Console.WriteLine("Add, Now Count = " + this.mCountInBag);
        }

        #endregion

        #region Private Members

        private int mMinCount = DefaultMinCount, mMaxCount = DefaultMaxCount;
        private HashSet<PooledObject<T>> mBag;
        private object mLockBag = new object();
        private volatile int mCountInBag = 0;
        private object mLockProp = new object();

        #region IDisposable Support
        private bool disposedValue = false; // 要检测冗余调用

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // TODO: 释放托管状态(托管对象)。
                    lock (mLockBag) {
                        foreach (var i in mBag) {
                            releaseObject(i);
                        }
                    }
                }

                // TODO: 释放未托管的资源(未托管的对象)并在以下内容中替代终结器。
                // TODO: 将大型字段设置为 null。

                disposedValue = true;
            }
        }

        // TODO: 仅当以上 Dispose(bool disposing) 拥有用于释放未托管资源的代码时才替代终结器。
        // ~ObjectPool() {
        //   // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
        //   Dispose(false);
        // }

        // 添加此代码以正确实现可处置模式。
        public void Dispose() {
            // 请勿更改此代码。将清理代码放入以上 Dispose(bool disposing) 中。
            Dispose(true);
            // TODO: 如果在以上内容中替代了终结器，则取消注释以下行。
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }

    /// <summary>
    /// 代表被对象池所管理的对象。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PooledObject<T> : IDisposable where T : class {
        internal PooledObject(T obj, ObjectPool<T> pool) {
            mPool = pool;
            mObj = obj;
        }

        /// <summary>
        /// 获取对象的值。
        /// </summary>
        public T Value {
            get {
                return this.mObj;
            }
            internal set {
                this.mObj = value;
            }
        }

        private T mObj;
        private ObjectPool<T> mPool;

        // Flag: Has Dispose already been called?
        private bool disposed;

        internal void reuse() {
            this.disposed = false;
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose() {
            Dispose(true);
        }

        // Protected implementation of Dispose pattern.
        protected void Dispose(bool disposing) {
            if (disposed)
                return;

            if (disposing) {
                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            //
            disposed = true;
            mPool.recycle(this);
        }

        ~PooledObject() {
            Dispose(false);
        }
    }
}
