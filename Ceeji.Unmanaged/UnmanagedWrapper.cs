using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Ceeji.Unmanaged {
    /// <summary>
    /// 一个将托管结构或类封装到非托管区域，将会复制其所有值，并保证内存在本对象被回收时回收。用于向非托管代码封送托管对象使用。
    /// </summary>
    public class UnmanagedWrapper<T> : IDisposable {
        // Flag: Has Dispose already been called?
        private bool mDisposed = false;
        private IntPtr mPtrToUnmanaged = IntPtr.Zero;
        private T mManaged;

        /// <summary>
        /// 存储非托管的类型的实例实际大小。
        /// </summary>
        public int UnmanagedSize { get; private set; }

        /// <summary>
        /// 返回指向非托管区域的指针。
        /// </summary>
        public IntPtr Pointer {
            get {
                return this.mPtrToUnmanaged;
            }
        }

        public T Value {
            get {
                return mManaged;
            }
            private set {
                mManaged = value;
            }
        }

        /// <summary>
        /// 创建 Ceeji.Unmanaged.UnmanagedWraper 的新实例。
        /// </summary>
        /// <param name="obj">要在非托管复制托管结构的类或结构。将会复制其所有值，并保证内存在本对象被回收时回收。</param>
        public UnmanagedWrapper(T obj) {
            this.Value = obj;
            this.UnmanagedSize = Marshal.SizeOf(obj);
            this.mPtrToUnmanaged = Marshal.AllocHGlobal(this.UnmanagedSize);
            Marshal.StructureToPtr(obj, this.mPtrToUnmanaged, false);
        }

        /// <summary>
        /// 销毁所有非托管资源。
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing) {
            if (mDisposed)
                return;

            if (disposing) {
                // Free any other managed objects here.
                //
            }

            // Free any unmanaged objects here.
            Marshal.DestroyStructure(this.mPtrToUnmanaged, typeof(T));
            Marshal.FreeHGlobal(this.mPtrToUnmanaged);
            mDisposed = true;
        }

        ~UnmanagedWrapper() {
            Dispose(false);
        }

        public static implicit operator IntPtr(UnmanagedWrapper<T> who) {
            return who.Pointer;
        }
    }
}
