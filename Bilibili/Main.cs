using Community.PowerToys.Run.Plugin.Bilibili.Properties;
using ManagedCommon;
using Wox.Infrastructure;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.Bilibili
{
#pragma warning disable CA1416 // 验证平台兼容性
    public class Bilibili : IPlugin, IReloadable, IDisposable
    {
        #region 插件元信息
        /// <summary>
        /// 插件ID
        /// 需要与plugin.json的ID一致
        /// </summary>
        public static string PluginID => "D0D9D4B371794898B779EC0C8BDDDD6E";
        /// <summary>
        /// 插件名称
        /// 需要与plugin.json的Name一致
        /// </summary>
        public string Name => Resources.PluginName;
        /// <summary>
        /// 插件说明
        /// </summary>
        public string Description => Resources.PluginDescription;
        #endregion

        #region 插件内部信息
        /// <summary>
        /// 插件目录
        /// </summary>
        private string pluginDir = "";
        /// <summary>
        /// 插件图标路径
        /// </summary>
        private string iconPath = "";
        /// <summary>
        /// 插件上下文
        /// </summary>
        private PluginInitContext? _context;
        /// <summary>
        /// 插件元信息
        /// </summary>
        private PluginMetadata? _meta;
        /// <summary>
        /// 是否销毁
        /// </summary>
        private bool _disposed;
        #endregion

        #region 插件暴露方法
        /// <summary>
        /// 插件初始化
        /// </summary>
        /// <param name="context"></param>
        public void Init(PluginInitContext context)
        {
            _context = context;
            _meta = context.CurrentPluginMetadata;
            _context.API.ThemeChanged += this.OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());
            var pluginDir = _meta.PluginDirectory;
            this.pluginDir = pluginDir;

            // 创建缓存目录
            var pluginCacheDir = Path.Join(pluginDir, "cache");
            if (!Directory.Exists(pluginCacheDir))
            {
                Directory.CreateDirectory(pluginCacheDir);
            }

            // 创建下载目录
            var pluginDownDir = Path.Join(pluginDir, "download");
            if (!Directory.Exists(pluginDownDir))
            {
                Directory.CreateDirectory(pluginDownDir);
            }
        }

        /// <summary>
        /// 插件查询
        /// </summary>
        /// <param name="query">查询内容</param>
        /// <returns></returns>
        public List<Result> Query(Query query)
        {
            var resultList = new List<Result>();
            if (query?.Search is null) return resultList;
            var value = query.Search;

            #region 搜索哔哩哔哩

            #region 处理搜索词
            var bili = new BilibiliHelper(pluginDir);
            var bvid = bili.MatchBilibili(value);
            if (string.IsNullOrWhiteSpace(bvid)) return resultList;
            var res = bili.GetBilibiliInfo(bvid);
            if (res is null) return resultList;
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
            return resultList;
        }

        /// <summary>
        /// 插件重载
        /// </summary>
        public void ReloadData()
        {
            if (_context is null)
            {
                return;
            }
            UpdateIconPath(_context.API.GetCurrentTheme());
        }
        /// <summary>
        /// 插件销毁
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// 插件销毁
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_context != null && _context.API != null)
                {
                    _context.API.ThemeChanged -= OnThemeChanged;
                }

                _disposed = true;
            }
        }
        #endregion

        #region 内部方法
        /// <summary>
        /// 创建一个新的结果
        /// </summary>
        /// <returns></returns>
        private Result GetNewResult()
        {
            var result = new Result
            {
                IcoPath = iconPath
            };
            return result;
        }
        /// <summary>
        /// 主题变更
        /// </summary>
        /// <param name="oldtheme">旧主题</param>
        /// <param name="newTheme">新主题</param>
        private void OnThemeChanged(Theme oldtheme, Theme newTheme) => UpdateIconPath(newTheme);
        /// <summary>
        /// 更新图标
        /// </summary>
        /// <param name="theme">当前主题</param>
        private void UpdateIconPath(Theme theme) => iconPath = theme is Theme.Light or Theme.HighContrastWhite ? _meta.IcoPathLight : _meta.IcoPathDark;

        /// <summary>
        /// 执行Shell
        /// </summary>
        /// <param name="comand"></param>
        private static void OpenShell(string comand)
        {
            Helper.OpenInShell(comand);
        }
        #endregion
    }
#pragma warning restore CA1416 // 验证平台兼容性
}
