using System.Runtime;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Threading;
using System;

namespace Ceeji {

    /// <summary>
    /// Provides support for lazy initialization. 
    /// </summary>
    /// <typeparam name="T">Specifies the type of element being laziliy initialized.</typeparam> 
    /// <remarks> 
    /// <para>
    /// By default, all public and protected members of <see cref="Delayed{T}"> are thread-safe and may be used 
    /// concurrently from multiple threads.  These thread-safety guarantees may be removed optionally and per instance
    /// using parameters to the type's constructors.
    /// </see></para>
    /// </remarks> 
    [Serializable]
    [ComVisible(false)]
    [HostProtection(Synchronization = true, ExternalThreading = true)]
    [DebuggerTypeProxy(typeof(System_LazyDebugView<>))]
    [DebuggerDisplay("ThreadSafetyMode={Mode}, IsValueCreated={IsValueCreated}, IsValueFaulted={IsValueFaulted}, Value={ValueForDebugDisplay}")]
    public class Delayed<T> {

        #region Inner classes
        /// <summary>
        /// wrapper class to box the initialized value, this is mainly created to avoid boxing/unboxing the value each time the value is called in case T is 
        /// a value type 
        /// </summary>
        [Serializable]
        class Boxed {
            internal Boxed(T value) {
                m_value = value;
            }
            internal T m_value;
        }


        /// <summary>
        /// Wrapper class to wrap the excpetion thrown by the value factory
        /// </summary> 
        class LazyInternalExceptionHolder {
            internal Exception m_exception;
            internal LazyInternalExceptionHolder(Exception ex) {
                m_exception = ex;
            }
        }
        #endregion

        // A dummy delegate used as a  : 
        // 1- Flag to avoid recursive call to Value in None and ExecutionAndPublication modes in m_valueFactory 
        // 2- Flag to PublicationOnly mode in m_threadSafeObj
        static Func<T> PUBLICATION_ONLY_OR_ALREADY_INITIALIZED = delegate { return default(T); };

        //null --> value is not created
        //m_value is Boxed --> the value is created, and m_value holds the value
        //m_value is LazyExceptionHolder --> it holds an exception 
        private volatile object m_boxed;

        // The factory delegate that returns the value. 
        // In None and ExecutionAndPublication modes, this will be set to PUBLICATION_ONLY_OR_ALREADY_INITIALIZED as a flag to avoid recursive calls
        [NonSerialized]
        private Func<T> m_valueFactory;

        // null if it is not thread safe mode
        // PUBLICATION_ONLY_OR_ALREADY_INITIALIZED if PublicationOnly mode 
        // object if ExecutionAndPublication mode
        [NonSerialized]
        private readonly object m_threadSafeObj;


        /// <summary>
        /// Initializes a new instance of the <see cref="T:Ceeji.Delayed{T}"> class that
        /// uses <typeparamref name="T">'s default constructor for lazy initialization.
        /// </typeparamref></see></summary> 
        /// <remarks>
        /// An instance created with this constructor may be used concurrently from multiple threads. 
        /// </remarks> 
        public Delayed()
            : this(DelayThreadSafetyMode.ExecutionAndPublication) {
        }

        /// <summary> 
        /// Initializes a new instance of the <see cref="T:Ceeji.Delayed{T}"> class that uses a
        /// specified initialization function. 
        /// </see></summary> 
        /// <param name="valueFactory">
        /// The <see cref="T:System.Func{T}"> invoked to produce the lazily-initialized value when it is 
        /// needed.
        /// 
        /// <exception cref="System.ArgumentNullException"><paramref name="valueFactory"> is a null
        /// reference (Nothing in Visual Basic).</paramref></exception> 
        /// <remarks>
        /// An instance created with this constructor may be used concurrently from multiple threads. 
        /// </remarks> 
        public Delayed(Func<T> valueFactory)
            : this(valueFactory, DelayThreadSafetyMode.ExecutionAndPublication) {
        }

        /// <summary> 
        /// Initializes a new instance of the <see cref="T:Ceeji.Delayed{T}">
        /// class that uses <typeparamref name="T">'s default constructor and a specified thread-safety mode. 
        /// </typeparamref></see></summary> 
        /// <param name="isThreadSafe">true if this instance should be usable by multiple threads concurrently; false if the instance will only be used by one thread at a time.
        ///  
        public Delayed(bool isThreadSafe) :
            this(isThreadSafe ? DelayThreadSafetyMode.ExecutionAndPublication : DelayThreadSafetyMode.None) {
        }

        /// <summary> 
        /// Initializes a new instance of the <see cref="T:Ceeji.Delayed{T}"> 
        /// class that uses <typeparamref name="T">'s default constructor and a specified thread-safety mode.
        /// </typeparamref></see></summary> 
        /// <param name="mode">The lazy thread-safety mode mode
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="mode"> mode contains an invalid valuee</paramref></exception>
        public Delayed(DelayThreadSafetyMode mode) {
            m_threadSafeObj = GetObjectFromMode(mode);
        }


        /// <summary> 
        /// Initializes a new instance of the <see cref="T:Ceeji.Delayed{T}"> class
        /// that uses a specified initialization function and a specified thread-safety mode.
        /// </see></summary>
        /// <param name="valueFactory"> 
        /// The <see cref="T:System.Func{T}"> invoked to produce the lazily-initialized value when it is needed.
        ///  
        /// <param name="isThreadSafe">true if this instance should be usable by multiple threads concurrently; false if the instance will only be used by one thread at a time. 
        /// 
        /// <exception cref="System.ArgumentNullException"><paramref name="valueFactory"> is 
        /// a null reference (Nothing in Visual Basic).</paramref></exception>
        public Delayed(Func<T> valueFactory, bool isThreadSafe)
            : this(valueFactory, isThreadSafe ? DelayThreadSafetyMode.ExecutionAndPublication : DelayThreadSafetyMode.None) {
        }

        /// <summary> 
        /// Initializes a new instance of the <see cref="T:Ceeji.Delayed{T}"> class
        /// that uses a specified initialization function and a specified thread-safety mode. </see>
        /// </summary>
        /// <param name="valueFactory">
        /// The <see cref="T:System.Func{T}"> invoked to produce the lazily-initialized value when it is needed.
        ///  
        /// <param name="mode">The lazy thread-safety mode.
        /// <exception cref="System.ArgumentNullException"><paramref name="valueFactory"> is 
        /// a null reference (Nothing in Visual Basic).</paramref></exception> 
        /// <exception cref="System.ArgumentOutOfRangeException"><paramref name="mode"> mode contains an invalid value.</paramref></exception>
        public Delayed(Func<T> valueFactory, DelayThreadSafetyMode mode) {
            if (valueFactory == null)
                throw new ArgumentNullException("valueFactory");

            m_threadSafeObj = GetObjectFromMode(mode);
            m_valueFactory = valueFactory;
        }

        /// <summary> 
        /// Static helper function that returns an object based on the given mode. it also throws an exception if the mode is invalid
        /// </summary>
        private static object GetObjectFromMode(DelayThreadSafetyMode mode) {
            if (mode == DelayThreadSafetyMode.ExecutionAndPublication)
                return new object();
            else if (mode == DelayThreadSafetyMode.PublicationOnly)
                return PUBLICATION_ONLY_OR_ALREADY_INITIALIZED;
            else if (mode != DelayThreadSafetyMode.None)
                throw new ArgumentOutOfRangeException("mode", "无效的延迟模式");

            return null; // None mode
        }

        /// <summary>
        /// Forces initialization during serialization.</summary> 
        /// <param name="context">The StreamingContext for the serialization operation. </param>
        [OnSerializing]
        private void OnSerializing(StreamingContext context) {
            // Force initialization
            T dummy = Value;
        }

        /// <summary>Creates and returns a string representation of this instance.</summary> 
        /// <returns>The result of calling <see cref="System.Object.ToString"> on the <see cref="Value">.</see></see></returns>
        /// <exception cref="T:System.NullReferenceException"> 
        /// The <see cref="Value"> is null.
        /// </see></exception>
        public override string ToString() {
            return IsValueCreated ? Value.ToString() : "值尚未创建。";
        }

        /// <summary>
        /// Gets the value of the Delayed&lt;T&gt; for debugging display purposes.
        /// </summary>
        internal T ValueForDebugDisplay {
            get {
                if (!IsValueCreated) {
                    return default(T);
                }
                return ((Boxed)m_boxed).m_value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance may be used concurrently from multiple threads. 
        /// </summary>
        internal DelayThreadSafetyMode Mode {
            get {
                if (m_threadSafeObj == null) return DelayThreadSafetyMode.None;
                if (m_threadSafeObj == (object)PUBLICATION_ONLY_OR_ALREADY_INITIALIZED) return DelayThreadSafetyMode.PublicationOnly;
                return DelayThreadSafetyMode.ExecutionAndPublication;
            }
        }

        /// <summary> 
        /// Gets whether the value creation is faulted or not
        /// </summary> 
        internal bool IsValueFaulted {
            get { return m_boxed is LazyInternalExceptionHolder; }
        }

        /// <summary>Gets a value indicating whether the <see cref="T:Ceeji.Delayed{T}"> has been initialized. 
        /// </see></summary> 
        /// <value>true if the <see cref="T:Ceeji.Delayed{T}"> instance has been initialized;
        /// otherwise, false.</see></value> 
        /// <remarks>
        /// The initialization of a <see cref="T:Ceeji.Delayed{T}"> instance may result in either
        /// a value being produced or an exception being thrown.  If an exception goes unhandled during initialization,
        /// the <see cref="T:Ceeji.Delayed{T}"> instance is still considered initialized, and that exception 
        /// will be thrown on subsequent accesses to <see cref="Value">.  In such cases, <see cref="IsValueCreated">
        /// will return true. 
        /// </see></see></see></see></remarks> 
        public bool IsValueCreated {
            get {
                return m_boxed != null && m_boxed is Boxed;
            }
        }

        /// <summary>Gets the lazily initialized value of the current <see cref="T:Ceeji.Delayed{T}">.</see></summary>
        /// <value>The lazily initialized value of the current <see cref="T:Ceeji.Delayed{T}">.</see></value> 
        /// <exception cref="T:System.MissingMemberException">
        /// The <see cref="T:Ceeji.Delayed{T}"> was initialized to use the default constructor 
        /// of the type being lazily initialized, and that type does not have a public, parameterless constructor. 
        /// </see></exception>
        /// <exception cref="T:System.MemberAccessException"> 
        /// The <see cref="T:Ceeji.Delayed{T}"> was initialized to use the default constructor
        /// of the type being lazily initialized, and permissions to access the constructor were missing.
        /// </see></exception>
        /// <exception cref="T:System.InvalidOperationException"> 
        /// The <see cref="T:Ceeji.Delayed{T}"> was constructed with the <see cref="T:System.Threading.DelayThreadSafetyMode.ExecutionAndPublication"> or
        /// <see cref="T:System.Threading.DelayThreadSafetyMode.None">  and the initialization function attempted to access <see cref="Value"> on this instance. 
        /// </see></see></see></see></exception> 
        /// <remarks>
        /// If <see cref="IsValueCreated"> is false, accessing <see cref="Value"> will force initialization. 
        /// Please <see cref="System.Threading.DelayThreadSafetyMode"> for more information on how <see cref="T:Ceeji.Delayed{T}"> will behave if an exception is thrown
        /// from initialization delegate.
        /// </see></see></see></see></remarks>
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Value {
            get {
                Boxed boxed = null;
                if (m_boxed != null) {
                    // Do a quick check up front for the fast path.
                    boxed = m_boxed as Boxed;
                    if (boxed != null) {
                        return boxed.m_value;
                    }

                    LazyInternalExceptionHolder exc = m_boxed as LazyInternalExceptionHolder;

                    throw exc.m_exception;
                }

                // Fall through to the slow path. 
                // We call NOCTD to abort attempts by the debugger to funceval this property (e.g. on mouseover) 
                //   (the debugger proxy is the correct way to look at state/value of this object)
#if !PFX_LEGACY_3_5
                //Debugger.NotifyOfCrossThreadDependency();
#endif
                return LazyInitValue();

            }
        }

        /// <summary>
        /// local helper method to initialize the value 
        /// </summary>
        /// <returns>The inititialized T value</returns>
        private T LazyInitValue() {
            Boxed boxed = null;
            DelayThreadSafetyMode mode = Mode;
            if (mode == DelayThreadSafetyMode.None) {
                boxed = CreateValue();
                m_boxed = boxed;
            }
            else if (mode == DelayThreadSafetyMode.PublicationOnly) {
                boxed = CreateValue();
                if (Interlocked.CompareExchange(ref m_boxed, boxed, null) != null)
                    boxed = (Boxed)m_boxed; // set the boxed value to the succeeded thread value 
            }
            else {
                lock (m_threadSafeObj) {
                    if (m_boxed == null) {
                        boxed = CreateValue();
                        m_boxed = boxed;
                    }
                    else // got the lock but the value is not null anymore, check if it is created by another thread or faulted and throw if so 
                    {
                        boxed = m_boxed as Boxed;
                        if (boxed == null) // it is not Boxed, so it is a LazyInternalExceptionHolder
                        {
                            LazyInternalExceptionHolder exHolder = m_boxed as LazyInternalExceptionHolder;
                            //Contract.Assert(exHolder != null);
                            throw exHolder.m_exception;
                        }
                    }
                }
            }
            //Contract.Assert(boxed != null);
            return boxed.m_value;
        }

        /// <summary>Creates an instance of T using m_valueFactory in case its not null or use reflection to create a new T()</summary> 
        /// <returns>An instance of Boxed.</returns>
        private Boxed CreateValue() {
            Boxed boxed = null;
            DelayThreadSafetyMode mode = Mode;
            if (m_valueFactory != null) {
                try {
                    // check for recursion
                    if (mode != DelayThreadSafetyMode.PublicationOnly && m_valueFactory == PUBLICATION_ONLY_OR_ALREADY_INITIALIZED)
                        throw new InvalidOperationException("Lazy_Value_RecursiveCallsToValue");

                    Func<T> factory = m_valueFactory;
                    if (mode != DelayThreadSafetyMode.PublicationOnly) // only detect recursion on None and ExecutionAndPublication modes 
                        m_valueFactory = PUBLICATION_ONLY_OR_ALREADY_INITIALIZED;
                    boxed = new Boxed(factory());
                }
                catch (Exception ex) {
                    if (mode != DelayThreadSafetyMode.PublicationOnly) // don't cache the exception for PublicationOnly mode
                    {
//#if PFX_LEGACY_3_5
                    m_boxed = new LazyInternalExceptionHolder(ex); 
//#else
                        //m_boxed = new LazyInternalExceptionHolder(ex.PrepForRemoting());// copy the call stack by calling the internal method PrepForRemoting 
//#endif
                    }
                    throw;
                }
            }
            else {
                try {
                    boxed = new Boxed((T)Activator.CreateInstance(typeof(T)));

                }
                catch (System.MissingMethodException) {
                    Exception ex = new System.MissingMemberException("Lazy_CreateValue_NoParameterlessCtorForT");
                    if (mode != DelayThreadSafetyMode.PublicationOnly) // don't cache the exception for PublicationOnly mode 
                        m_boxed = new LazyInternalExceptionHolder(ex);
                    throw ex;
                }
            }

            return boxed;
        }

        public static implicit operator T(Delayed<T> value) {
            return value.Value;
        }

    }

    /// <summary>A debugger view of the Delayed<T> to surface additional debugging properties and 
    /// to ensure that the Delayed<T> does not become initialized if it was not already.</summary> 
    internal sealed class System_LazyDebugView<t> {
        //The Lazy object being viewed.
        private readonly Delayed<t> m_lazy;

        /// <summary>Constructs a new debugger view object for the provided Lazy object.</summary> 
        /// <param name="lazy">A Lazy object to browse in the debugger.
        public System_LazyDebugView(Delayed<t> lazy) {
            m_lazy = lazy;
        }

        /// <summary>Returns whether the Lazy object is initialized or not.</summary>
        public bool IsValueCreated {
            get { return m_lazy.IsValueCreated; }
        }

        /// <summary>Returns the value of the Lazy object.</summary>
        public t Value {
            get { return m_lazy.ValueForDebugDisplay; }
        }

        /// <summary>Returns the execution mode of the Lazy object</summary> 
        public DelayThreadSafetyMode Mode {
            get { return m_lazy.Mode; }
        }

        /// <summary>Returns the execution mode of the Lazy object</summary>
        public bool IsValueFaulted {
            get { return m_lazy.IsValueFaulted; }
        }
    }
}