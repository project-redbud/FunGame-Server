namespace Milimoe.FunGame.WebAPI.Models
{
    public class PayloadModel<T> where T : struct, Enum
    {
        /// <summary>
        /// 请求类型
        /// </summary>
        public T RequestType { get; set; } = default;

        /// <summary>
        /// 状态码
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// 处理结果
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// 响应时间
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 业务数据
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = [];
    }
}
