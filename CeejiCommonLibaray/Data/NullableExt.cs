using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System {
    public static class NullableExt {
        /// <summary>
        /// 针对 bool? 类型的三种状态分别返回不同的值。
        /// </summary>
        /// <typeparam name="T">值的类型。</typeparam>
        /// <param name="v">bool? 类型的值</param>
        /// <param name="valTrue">值为 true 时返回的内容</param>
        /// <param name="valFalse">值为 false 时返回的内容</param>
        /// <param name="valNull">值为 null 时返回的内容</param>
        /// <returns></returns>
        public static T BooleanSelect<T>(this Nullable<bool> v, T valTrue, T valFalse, T valNull) {
            if (!v.HasValue)
                return valNull;

            return v.Value ? valTrue : valFalse;
        }
    }
}
