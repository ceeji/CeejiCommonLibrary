using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ceeji.FastWeb {
    /// <summary>
    /// 代表 URI 路由表。
    /// </summary>
    public abstract class Router {
        public abstract Func<dynamic, HtmlResponse> GetResponse(string url);
    }
}
