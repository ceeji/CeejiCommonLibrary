using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.UI {
    /// <summary>
    /// 一个对象显示包装，用于在列表框、组合框等各种使用对象的 ToString 方法显示文本的地方，以及需要 Debug 实时查看数据的地点，以便方便显示对象的名称或内容，同时保留对象的引用。
    /// </summary>
    /// <typeparam name="T">对象的类型。</typeparam>
    public class ObjectDisplayWrapper<T> {
        /// <summary>
        /// 创建 <see cref="Ceeji.UI.ObjectDisplayWrapper&lt;T&gt;"/> 的新实例。使用对象的 ObjectDisplayFormatter 的默认格式化委托进行格式化，如果其为 null，则使用对象的 ToString() 方法来显示其名称。
        /// </summary>
        /// <param name="obj">要包装的对象。</param>
        public ObjectDisplayWrapper(T obj) {
            this.mObject = obj;
        }

        /// <summary>
        /// 创建 <see cref="Ceeji.UI.ObjectDisplayWrapper&lt;T&gt;"/> 的新实例。尝试使用指定的格式化委托进行格式化，或（如果为 null）使用对象的 ObjectDisplayFormatter 的默认格式化委托进行格式化，如果其也为 null，则使用对象的 ToString() 方法来显示其名称。
        /// </summary>
        /// <param name="obj">要包装的对象。</param>
        /// <param name="textFormatter">要使用的文本格式化器。</param>
        public ObjectDisplayWrapper(T obj, Func<T, string> textFormatter) : this(obj) {
            this.TextFormatter = textFormatter;
        }

        /// <summary>
        /// 创建 <see cref="Ceeji.UI.ObjectDisplayWrapper&lt;T&gt;"/> 的新实例。
        /// </summary>
        /// <param name="obj">要包装的对象。</param>
        /// <param name="displayText">要显示的文本。</param>
        public ObjectDisplayWrapper(T obj, string displayText) : this(obj) {
            if (displayText == null) throw new ArgumentNullException("displayText");

            this.DisplayText = displayText;
        }

        /// <summary>
        /// 返回代表被包装对象名称或内容的文本，具体返回内容根据 DisplayText 而定。
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            if (DisplayText != null) {
                return DisplayText;
            }

            if (TextFormatter != null)
                return TextFormatter(this.Object);
            else if (ObjectDisplayDefaultFormatter<T>.TextFormatter != null)
                return ObjectDisplayDefaultFormatter<T>.TextFormatter(Object);
            else
                return Object.ToString();
        }

        /// <summary>
        /// 返回所关联的对象。
        /// </summary>
        public T Object {
            get {
                return this.mObject;
            }
        }

        /// <summary>
        /// 设置显示文本。如果为 null，尝试使用对象的 ObjectDisplayFormatter 的默认格式化委托进行格式化，如果其为 null，则使用对象的 ToString() 方法来显示其名称。
        /// </summary>
        public string DisplayText {
            get {
                return this.mString;
            }
            set {
                this.mString = value;
            }
        }

        private T mObject;
        private string mString;
        /// <summary>
        /// 返回或设置文本格式化器。如果为 null，则使用默认值。
        /// </summary>
        public Func<T, string> TextFormatter { get; set; }
    }

    /// <summary>
    /// 用于存取对于特定类型的对象如何格式化显示的默认实现的静态类。
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static class ObjectDisplayDefaultFormatter<T> {
        /// <summary>
        /// 设置或返回指定类型的默认显示文本格式化器。
        /// </summary>
        public static Func<T, string> TextFormatter { get; set; }
    }
}
