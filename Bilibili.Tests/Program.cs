

using Community.PowerToys.Run.Plugin.Bilibili;

namespace Community.Powertoys.Run.Plugin.Bilibili.Tests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var pluginDir = Directory.GetCurrentDirectory();
            var resultList = new List<Result>();
            var value = Console.ReadLine() ?? "";

            #region 搜索哔哩哔哩

            #region 处理搜索词
            var bili = new BilibiliHelper(pluginDir);
            var bvid = bili.MatchBilibili(value);
            var res = bili.GetBilibiliInfo(bvid);
            if (res is null)
            {
                Console.WriteLine("获取数据异常");
                Console.ReadKey();
                return;
            }
            #endregion

            #region 处理结果
            var searchRes = GetNewResult();
            var code = res.code;
            var data = res.data;
            if (data is null)
            {
                var errtype = "";
                switch (code)
                {
                    case -352:
                        {
                            errtype = "风控校验失败 (UA 或 wbi 参数不合法)";
                        }
                        break;
                    case -400:
                        {
                            errtype = "请求错误";
                        }
                        break;
                    case -401:
                        {
                            errtype = "未认证 (或非法请求)";
                        }
                        break;
                    case -403:
                        {
                            errtype = "访问权限不足";
                        }
                        break;
                    case -412:
                        {
                            errtype = "请求被拦截 (客户端 ip 被服务端风控)";
                        }
                        break;
                    default:
                        {
                            errtype = $"CODE{code}";
                        }
                        break;
                }
                searchRes.Title = $"API 错误: {errtype} ({bvid})";
                searchRes.SubTitle = $"biliapimsg: {res.message}";
                resultList.Add(searchRes);
            }
            else
            {
                var title = data.title;
                var owner_name = data.owner.name;
                var owner_pic = data.owner.face;
                var owner_mid = data.owner.mid;
                // 缓存up主头像
                bili.CachePic(owner_mid, owner_pic);
                var area = data.tname;
                var like = data.stat.like;
                var coin = data.stat.coin;
                var favorite = data.stat.favorite;
                var share = data.stat.share;
                searchRes.Title = $"打开视频: {title} ({bvid})";
                searchRes.SubTitle = $"UP主：{owner_name} · {area} · {bili.Numbers(like)}点赞 · {bili.Numbers(coin)}投币 · {bili.Numbers(favorite)}收藏 · {bili.Numbers(share)}分享";
                searchRes.IcoPath = Path.Join(pluginDir, "cache", $"cache-{owner_mid}.jpg");
                searchRes.Action = action =>
                {
                    OpenShell($"https://bilibili.com/video/{bvid}");
                    return true;
                };
                resultList.Add(searchRes);
                searchRes = GetNewResult();
                searchRes.Title = $"下载封面: {title} ({bvid})";
                searchRes.SubTitle = $"点击下载视频封面";
                searchRes.Action = action =>
                {
                    var path = bili.DownloadCover(data.pic);
                    if (string.IsNullOrWhiteSpace(path)) return false;
                    OpenShell($"{path}");
                    return true;
                };
                resultList.Add(searchRes);
            }
            #endregion

            #endregion

            // 循环输出结果
            foreach (var item in resultList)
            {
                Console.WriteLine("-------");
                Console.WriteLine($"标题：{item.Title}");
                Console.WriteLine($"副标题：{item.SubTitle}");
                Console.WriteLine("-------");
            }
            Console.ReadKey();
        }

        #region 内部方法
        /// <summary>
        /// 创建一个新的结果
        /// </summary>
        /// <returns></returns>
        private static Result GetNewResult()
        {
            return new Result();
        }

        /// <summary>
        /// 执行Shell
        /// </summary>
        /// <param name="comand"></param>
        private static void OpenShell(string comand)
        {
            // pass...
        }
        #endregion
    }

    public class Result
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string IcoPath { get; set; }
        public Func<object, bool> Action { get; set; }
    }
}