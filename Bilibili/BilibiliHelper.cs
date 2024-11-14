using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Community.PowerToys.Run.Plugin.Bilibili
{
    public class BilibiliHelper
    {
        /// <summary>
        /// HTTP请求对象
        /// </summary>
        private readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// 工作目录
        /// </summary>
        private string workDir = "";

        public BilibiliHelper(string path = "")
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Directory.GetCurrentDirectory();
            }
            workDir = path;
        }

        /// <summary>
        /// 哔哩哔哩AV号转BV号
        /// </summary>
        /// <param name="aid">V号</param>
        /// <returns></returns>
        private string av2bv(string aidStr)
        {
            string table = "fZodR9XQDSUm21yCkr6zBqiveYah8bt4xsWpHnJE7jL5VG3guMTKNPAwcF";
            var tr = new Dictionary<char, int>();
            for (int i = 0; i < 58; i++)
            {
                tr[table[i]] = i;
            }
            int[] s = { 11, 10, 3, 8, 4, 6 };
            long xor = 177451812;
            long add = 8728348608;

            var aid = long.Parse(aidStr);
            aid = (aid ^ xor) + add;
            char[] r = "BV1  4 1 7  ".ToCharArray();
            for (int i = 0; i < 6; i++)
            {
                r[s[i]] = table[(int)(aid / Math.Pow(58, i) % 58)];
            }
            return new string(r);
        }

        /// <summary>
        /// 获得301/302跳转后的地址
        /// </summary>
        /// <param name="url">地址</param>
        /// <returns></returns>
        private string getRedirectUrl(string url)
        {
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            var res = httpClient.Send(req);
            return res.Headers.Location?.ToString() ?? "";
        }

        /// <summary>
        /// 下载图片到指定位置
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private string DownloadPic(string url, string path)
        {
            // 获得保存地址目录
            var dir = Path.GetDirectoryName(path);
            // 判断目录是否存在，不存在则创建
            if(!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            using HttpClient client = new();
            try
            {
                var imageBytes = client.GetByteArrayAsync(url).Result;
                File.WriteAllBytes(path, imageBytes);
                return path;
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// 匹配哔哩哔哩
        /// </summary>
        /// <param name="text">文本</param>
        /// <returns></returns>
        public string MatchBilibili(string text)
        {
            // 短链接匹配
            var matchB23Url = Regex.Match(text, @".*((?:http|https)://(?:(?:bili(?:22|23|33|2233).cn)|(?:b23.tv))/[A-Za-z0-9]+)", RegexOptions.Singleline);
            if (matchB23Url.Success)
            {
                string b23Url = matchB23Url.Groups[1].Value;
                text = getRedirectUrl(b23Url);
            }

            // av号匹配
            var matchAid = Regex.Match(text, @".*(?<![A-Za-z0-9])(?:AV|av)(\d+)", RegexOptions.Singleline);
            if (matchAid.Success)
            {
                string aid = matchAid.Groups[1].Value;
                return av2bv(aid);
            }

            // bv号匹配
            var matchBvid = Regex.Match(text, @".*(?<![A-Za-z0-9])(BV[A-Za-z0-9]{10})(?![A-Za-z0-9])", RegexOptions.Singleline);
            if (matchBvid.Success)
            {
                return matchBvid.Groups[1].Value;
            }

            return "";
        }

        /// <summary>
        /// 使用bvid获得哔哩哔哩视频信息
        /// </summary>
        /// <param name="bvid"></param>
        /// <returns></returns>
        public apiBilibili? GetBilibiliInfo(string bvid)
        {
            var result = new apiBilibili();

            var api = $"https://api.bilibili.com/x/web-interface/view?bvid={bvid}";
            var req = new HttpRequestMessage(HttpMethod.Get, api);
            req.Headers.Add("origin", "https://space.bilibili.com");
            req.Headers.Add("referer", "https://www.bilibili.com/");
            req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/97.0.4692.99 Safari/537.36");
            var res = httpClient.Send(req);
            if (res.IsSuccessStatusCode)
            {
                var str = res.Content.ReadAsStringAsync().Result;
                result = JsonSerializer.Deserialize<apiBilibili>(str);
            }

            return result;
        }

        /// <summary>
        /// 格式化数字
        /// </summary>
        /// <param name="n">数字</param>
        /// <returns></returns>
        public string Numbers(double n)
        {
            if (n >= 100000000)
            {
                return $"{n / 100000000:F2}亿";
            }
            else if (n >= 10000)
            {
                return $"{n / 10000:F2}万";
            }
            else
            {
                return n.ToString();
            }
        }

        /// <summary>
        /// 缓存up主头像
        /// </summary>
        /// <param name="mid">UP主UID</param>
        /// <param name="url">头像地址</param>
        public void CachePic(int mid, string url)
        {
            var cache_pic = Path.Join(workDir, "cache", $"cache-{mid}.jpg");
            if (File.Exists(cache_pic)) return;
            DownloadPic(url, cache_pic);
        }

        /// <summary>
        /// 下载视频封面
        /// </summary>
        /// <param name="url"></param>
        public string DownloadCover(string url)
        {
            var fileName = Path.GetFileName(url);
            var down_pic = Path.Join(workDir, "download", fileName);
            var path = "";
            if (File.Exists(down_pic))
            {
                path = down_pic;
            }
            else
            {
                path = DownloadPic(url, down_pic);
            }
            return path;
        }

        #region 哔哩哔哩接口构造
        /// <summary>
        /// 哔哩哔哩接口类
        /// </summary>
        public class apiBilibili
        {
            /// <summary>
            /// 响应码
            /// </summary>
            public int code { get; set; }
            /// <summary>
            /// 响应信息
            /// </summary>
            public string message { get; set; }
            /// <summary>
            /// 响应数据
            /// </summary>
            public apiBilibiliData data { get; set; }
        }
        /// <summary>
        /// 哔哩哔哩接口数据
        /// </summary>
        public class apiBilibiliData
        {
            /// <summary>
            /// BV号
            /// </summary>
            public string bvid { get; set; }
            /// <summary>
            /// AV号
            /// </summary>
            public long aid { get; set; }
            /// <summary>
            /// 分区名
            /// </summary>
            public string tname { get; set; }
            /// <summary>
            /// 封面
            /// </summary>
            public string pic { get; set; }
            /// <summary>
            /// 标题
            /// </summary>
            public string title { get; set; }
            /// <summary>
            /// 投稿人
            /// </summary>
            public apiBilibiliOwner owner { get; set; }
            /// <summary>
            /// 视频数据统计
            /// </summary>
            public apiBilibiliStat stat { get; set; }
        }
        /// <summary>
        /// 哔哩哔哩投稿人数据
        /// </summary>
        public class apiBilibiliOwner
        {
            /// <summary>
            /// 投稿人UID
            /// </summary>
            public int mid { get; set; }
            /// <summary>
            /// 投稿人名字
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// 投稿人头像
            /// </summary>
            public string face { get; set; }
        }
        /// <summary>
        /// 哔哩哔哩统计数据
        /// </summary>
        public class apiBilibiliStat
        {
            /// <summary>
            /// 收藏数
            /// </summary>
            public long favorite { get; set; }
            /// <summary>
            /// 硬币数
            /// </summary>
            public long coin { get; set; }
            /// <summary>
            /// 分享数
            /// </summary>
            public long share { get; set; }
            /// <summary>
            /// 点赞数
            /// </summary>
            public long like { get; set; }
        }
        #endregion

        #region 
        #endregion
    }
}
