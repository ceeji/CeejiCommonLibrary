using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.FastWeb {
    /// <summary>
    /// 代表一个需要异步在后台准备输出的 HtmlPart。
    /// </summary>
    public abstract class AsyncHtmlPart : HtmlPart {
        /// <summary>
        /// 异步准备输出结果。
        /// </summary>
        /// <returns></returns>
        public abstract Task PrepareStreamAsync();
    }
}
