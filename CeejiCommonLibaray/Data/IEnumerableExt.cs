using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace System {
    /// <summary>
    /// 提供对列表、可枚举对象和集合的迭代方法。
    /// </summary>
    public static class IEnumerableExt {
        /// <summary>
        /// 将指定的 IEnumerable 对象转换为 IEnumerable&lt;object&gt; 对象，以便利用泛型的速度和丰富的扩展方法。 
        /// </summary>
        /// <param name="who">指定要转换的 IEnumerable 对象。</param>
        public static IEnumerable<object> ToGeneric(this IEnumerable who) {
            foreach (var obj in who) {
                    yield return obj;
            }
        }

        /// <summary>
        /// 在指定的元素中运行迭代函数。返回对象本身，便于执行进一步操作。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="act">要执行的迭代委托。</param>
        public static IEnumerable<T> Iterate<T>(this IEnumerable<T> who, Action<T> act) {
            foreach (var obj in who) {
                act(obj);
            }

            return who;
        }

        /// <summary>
        /// 返回类型为 <typeparamref name="T"/> 的第一项。
        /// </summary>
        /// <typeparam name="T">要返回的类型。</typeparam>
        /// <returns>返回类型为 <typeparamref name="T"/> 的第一项。</returns>
        public static T First<T>(this IEnumerable who) {
            foreach (var obj in who) {
                if (obj is T)
                    return (T)obj;
            }

            throw new InvalidOperationException("枚举为空");
        }

        /// <summary>
        /// 返回类型为 <typeparamref name="T"/> 的第一项。
        /// </summary>
        /// <typeparam name="T">要返回的类型。</typeparam>
        /// <returns>返回类型为 <typeparamref name="T"/> 的第一项。</returns>
        public static T First<T, TSource>(this IEnumerable<TSource> who) where T : TSource {
            foreach (var obj in who) {
                if (obj is T)
                    return (T)obj;
            }

            throw new InvalidOperationException("枚举为空");
        }

        /// <summary>
        /// 在指定的元素中运行迭代函数。返回一个枚举数，包括所有委托返回 true 的对象。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="func">要执行的迭代委托。</param>
        public static IEnumerable<T> FindAll<T>(this IEnumerable<T> who, Func<T, bool> func) {
            foreach (var obj in who) {
                if (func(obj))
                    yield return obj;
            }
        }

        /// <summary>
        /// 在指定的列表中运行迭代函数，并可以获知当前迭代位置。返回对象本身，便于执行进一步操作。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="act">要执行的迭代委托。</param>
        public static IList<T> Iterate<T>(this IList<T> who, Action<T, int> act) {
            for (var i = 0; i < who.Count ;++i) {
                act(who[i], i);
            }

            return who;
        }

        /// <summary>
        /// 在指定的列表中运行迭代函数，并可以获知当前迭代位置。返回对象本身，便于执行进一步操作。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="func">要执行的迭代委托。</param>
        public static IEnumerable<T> FindAll<T>(this IList<T> who, Func<T, int, bool> func) {
            for (var i = 0; i < who.Count; ++i) {
                if (func(who[i], i))
                    yield return who[i];
            }
        }

        /// <summary>
        /// 在指定的列表中并行运行迭代函数，并可以获知当前迭代位置。返回对象本身，便于执行进一步操作。【需要 .NET Framework 4，否则返回 NotSupportedException】
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="func">要执行的迭代委托。</param>
        /// <exception cref="NotSupportedException"></exception>
        public static IEnumerable<T> ParallelIterate<T>(this IList<T> who, Action<T, int> func) {
            var method = getParallelFor();

            if (method == null)
                throw new NotSupportedException("当前 .NET Framdwork 版本不支持并行函数。");

            method.Invoke(null, new object[] { 0, who.Count, new Action<int>(i => func(who[i], i)) });

            return who;

        }

        /// <summary>
        /// 填充指定的列表的每一项。返回对象本身，便于执行进一步操作。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="val">要填充的值。</param>
        public static IList Fill(this IList who, object val) {
            for (var i = 0; i < who.Count; ++i) {
                who[i] = val;
            }

            return who;
        }

        /// <summary>
        /// 填充指定的列表的每一项。返回对象本身，便于执行进一步操作。对于引用类型，这可能导致所有的元素都是同一个元素的引用。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="val">要填充的值。</param>
        public static IList<T> Fill<T>(this IList<T> who, T val) {
            for (var i = 0; i < who.Count; ++i) {
                who[i] = val;
            }

            return who;
        }

        /// <summary>
        /// 填充指定的列表的每一项。返回对象本身，便于执行进一步操作。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="val">要填充的值。</param>
        public static IList<T> Fill<T>(this IList<T> who, Func<int, T> creator) {
            for (var i = 0; i < who.Count; ++i) {
                who[i] = creator(i);
            }

            return who;
        }

        /// <summary>
        /// 返回 IList 的带有索引的包装。
        /// </summary>
        /// <param name="who">要为哪个可迭代对象执行迭代。</param>
        /// <param name="val">要填充的值。</param>
        public static IEnumerable<IListItemWithIndex<T>> WithIndex<T>(this IList<T> who) {
            for (var i = 0; i < who.Count; ++i) {
                yield return new IListItemWithIndex<T>() { Index = i, Value = who[i] };
            }
        }

        public struct IListItemWithIndex<T> {
            public T Value { get; internal set; }
            public int Index { get; internal set; }
        }

        /// <summary>
        /// 将指定的“数组的数组”转换为多维数组。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <returns></returns>
        public static T[,] ToMultidimensionalArray<T>(this T[][] array) {
            if (array.Length == 0) // 如果没有元素
                return new T[0, 0];

            if (array[0] == null)
                throw new ArgumentException("指定的数组的数组的每一子数组都必须不为 null。");

            var len1 = array.Length;
            var len2 = array[0].Length;

            if (!Array.TrueForAll<T[]>(array, row => row != null && row.Length == len2)) {
                throw new ArgumentException("指定的数组的数组的每一子数组都必须不为 null，且大小必须都相同。");
            }

            var ret = new T[len1, len2];
            for (var i = 0; i < len1; ++i) {
                for (var j = 0; j < len2; ++j) {
                    ret[i, j] = array[i][j];
                }
            }

            return ret;
        }

        /// <summary>
        /// 将指定的“数组的数组”转换为多维数组。
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <returns></returns>
        public static T[,] ToMultidimensionalArray<T>(this IList<T[]> list) {
            if (list.Count == 0) // 如果没有元素
                return new T[0, 0];

            if (list[0] == null)
                throw new ArgumentException("指定的列表的每一子数组都必须不为 null。");

            var len1 = list.Count;
            var len2 = list[0].Length;

            if (!list.All(row => row != null && row.Length == len2)) {
                throw new ArgumentException("指定的列表的每一子数组都必须不为 null，且大小必须都相同。");
            }

            var ret = new T[len1, len2];
            for (var i = 0; i < len1; ++i) {
                for (var j = 0; j < len2; ++j) {
                    ret[i, j] = list[i][j];
                }
            }

            return ret;
        }

        public static void CopyTo(this byte[] source, Stream s) {
            s.Write(source);
        }

        private static MethodInfo getParallelFor() {
            lock (cachedPForLock) {
                if (cachedPFor != null)
                    return cachedPFor;
                else {
                    Type tx = Type.GetType("System.Threading.Tasks.Parallel");
                    cachedPFor = tx.GetMethod("For", new Type[] { typeof(int), typeof(int), typeof(Action<int>) });

                    return cachedPFor;
                }
            }
        }

        private static MethodInfo cachedPFor = null;
        private static object cachedPForLock = new object();
    }
}
