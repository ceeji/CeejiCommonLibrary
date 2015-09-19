using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Ceeji.Network {
    /// <summary>
    /// 用来简化 HTTP 请求和响应的方法集。
    /// </summary>
    public static class HttpHelper {
        /// <summary>
        /// 使用 GET 方法异步获取某个特定 的 Uri 的资源，获取完成后无论成功还是失败，都将调用 HttpAsyncCompletedCallback 委托，并支持通过 HttpAsyncProgressCallback 报告下载进度。
        /// </summary>
        /// <param name="uri">要下载的资源。</param>
        /// <param name="completedCallback">完成下载后要执行的委托。</param>
        /// <param name="progressCallback">进度报告委托，可以为 null。</param>
        public static void Get(Uri uri, HttpAsyncCompletedCallback<byte[]> completedCallback, HttpAsyncProgressCallback progressCallback = null) {
            System.Net.WebClient client = new System.Net.WebClient();
            client.DownloadDataCompleted += (sender, e) => {
                completedCallback(e.Cancelled == false && e.Error == null && e.Result != null, e.Error, e.Result);
            };
            if (progressCallback != null) {
                client.DownloadProgressChanged += (sender, e) => {
                    progressCallback(e.BytesReceived, e.TotalBytesToReceive, e.ProgressPercentage);
                };
            }
            client.DownloadDataAsync(uri);
        }

        public static void Get(Uri uri) {
            System.Net.WebClient client = new System.Net.WebClient();
            client.DownloadData(uri);

        }

        /// <summary>
        /// 用于指示 Http 异步操作已经完成。
        /// </summary>
        /// <typeparam name="TData">指示操作结果的数据类型。</typeparam>
        /// <param name="successful">指示操作是否成功完成并得到了数据。</param>
        /// <param name="exception">返回操作中发生的异常，或者 null。</param>
        /// <param name="result">获取操作结果。</param>
        public delegate void HttpAsyncCompletedCallback<TData>(bool successful, Exception exception, TData result);
        /// <summary>
        /// 用于获取 Http 异步操作的进度。
        /// </summary>
        /// <param name="finishedSize"></param>
        /// <param name="totalSize"></param>
        /// <param name="percent"></param>
        public delegate void HttpAsyncProgressCallback(long finishedSize, long totalSize, int percent);
    }
}
