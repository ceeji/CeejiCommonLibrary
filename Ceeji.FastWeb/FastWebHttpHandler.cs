using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Routing;

namespace Ceeji.FastWeb {
    /// <summary>
    /// 使用 FastWeb 引擎处理 Http 请求的 HttpHandler。
    /// </summary>
    public class FastWebHttpHandler : IHttpHandler, IRouteHandler {
        /// <summary>
        /// 创建使用 FastWeb 引擎处理 Http 请求的 HttpHandler。
        /// </summary>
        public FastWebHttpHandler(RequestProcesser func) {
            mFunc = func;
        }

        #region IHttpHandler 成员

        public bool IsReusable {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context) {
            // 确保禁用输出缓存
            context.Response.Buffer = false;
            context.Response.BufferOutput = false;

            HtmlResponse r = new HtmlResponse(DefaultTitle);
            mFunc(context, r);

            r.WriteToStream(context.Response.OutputStream, HtmlOutputFormat.Zipped, 0);
            context.Response.OutputStream.Flush();
            context.Response.End();
        }

        #endregion

        #region IRouteHandler 成员

        public IHttpHandler GetHttpHandler(RequestContext requestContext) {
            return this;
        }

        #endregion

        /// <summary>
        /// 获取或设置页面的默认标题。
        /// </summary>
        public static string DefaultTitle { get; set; }

        private RequestProcesser mFunc;
    }

    /// <summary>
    /// 处理请求的委托。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="response"></param>
    public delegate void RequestProcesser(HttpContext context, HtmlResponse response);
}
