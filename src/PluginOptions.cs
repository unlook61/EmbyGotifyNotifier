using System.ComponentModel;
using MediaBrowser.Model.Attributes;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 插件全局配置。以 JSON 持久化到
    /// {PluginConfigurationsPath}/Gotify Notifications.json，
    /// 由自包含配置页（configurationpage）读写。
    /// </summary>
    public class PluginOptions
    {
        /// <summary>
        /// Gotify 服务器地址
        /// </summary>
        [DisplayName("Gotify 服务器地址")]
        [Description("Gotify 服务地址，例如 https://gotify.example.com，末尾不要带斜杠。")]
        [Required]
        public string ServerUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gotify 应用 Token
        /// </summary>
        [DisplayName("应用 Token")]
        [Description("在 Gotify 网页端「应用」页面创建应用后获得的 Token，形如 AxxxxxxxxXXXXX.xxxxxxxx。")]
        [Required]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// 消息优先级
        /// </summary>
        [DisplayName("消息优先级")]
        [Description("Gotify 消息优先级，0-10，数字越大通知提醒越强。默认 5。")]
        public int Priority { get; set; } = 5;

        /// <summary>
        /// 是否附带海报大图
        /// </summary>
        [DisplayName("附带海报大图")]
        [Description("在手机通知中附带影视海报/剧集封面。需要 Emby 已配置「远程访问」外部地址。")]
        public bool EnableImages { get; set; } = true;

        /// <summary>
        /// 是否跳过 HTTPS 证书校验
        /// </summary>
        [DisplayName("跳过 HTTPS 证书校验")]
        [Description("Gotify 使用自签名证书且无法替换时勾选。正常有效证书无需勾选。")]
        public bool DisableTlsValidation { get; set; }

        /// <summary>
        /// 通知标题模板
        /// </summary>
        [DisplayName("标题模板")]
        [Description("推送通知的标题。可用变量：{TitleLine} {SeriesName} {Name} {TypeName} {ServerName} {Date}。")]
        public string TitleTemplate { get; set; } = "🎬 新片入库";

        /// <summary>
        /// 通知内容模板
        /// </summary>
        [DisplayName("内容模板")]
        [Description("推送通知的正文。可用变量：{TitleLine} {Name} {SeriesName} {Season} {Episode} {Year} {TypeName} {Overview} {ServerName} {Url} {Date}。")]
        public string MessageTemplate { get; set; } = "{TitleLine}\n\n{Overview}";

        /// <summary>
        /// 是否合并短时间内的大量入库通知
        /// </summary>
        [DisplayName("合并入库通知")]
        [Description("一次扫描新增大量媒体时，将多条入库通知合并为一条发送，避免手机连续震动。")]
        public bool MergeLibraryNotifications { get; set; } = true;

        /// <summary>
        /// 合并窗口（秒）
        /// </summary>
        [DisplayName("合并窗口（秒）")]
        [Description("在此时间窗口内到达的入库通知会被合并为一条。默认 10 秒，最小 2 秒。")]
        public int MergeWindowSeconds { get; set; } = 10;

        /// <summary>
        /// 使用 Markdown 排版正文（Gotify 客户端支持）
        /// </summary>
        [DisplayName("Markdown 排版")]
        [Description("用 Markdown 生成正文（加粗标题、评分/年份/类型一行、简介引用块）。仅在正文模板为默认值时生效。")]
        public bool UseMarkdown { get; set; }

        /// <summary>
        /// 使用横版剧照（Backdrop）而非竖版海报
        /// </summary>
        [DisplayName("使用横版剧照")]
        [Description("手机通知横幅更适合 16:9 的剧照；没有剧照时自动回退到海报。")]
        public bool UseBackdropImage { get; set; }

        /// <summary>
        /// Emby 外部访问地址（用于通知点击跳转）
        /// </summary>
        [DisplayName("外部访问地址")]
        [Description("例如 https://emby.example.com。填写后通知会带上「点击打开详情页」链接；留空则使用 Emby 自带的地址。")]
        public string ExternalUrl { get; set; } = string.Empty;

        /// <summary>
        /// 是否启用推送过滤规则
        /// </summary>
        [DisplayName("启用过滤")]
        [Description("开启后按下面的规则筛选，只有满足条件的媒体才会推送。")]
        public bool FilterEnabled { get; set; }

        /// <summary>
        /// 只推送这些媒体类型（逗号分隔，如 Movie,Episode）
        /// </summary>
        [DisplayName("媒体类型")]
        [Description("逗号分隔，如 Movie,Episode,Series。留空表示不限。")]
        public string IncludeItemTypes { get; set; } = string.Empty;

        /// <summary>
        /// 只推送这些媒体库（逗号分隔的库 Id）
        /// </summary>
        [DisplayName("媒体库")]
        [Description("只推送勾选的媒体库；不勾选表示全部。")]
        public string IncludeLibraryIds { get; set; } = string.Empty;

        /// <summary>
        /// 只推送包含这些标签的媒体（逗号分隔）
        /// </summary>
        [DisplayName("包含标签")]
        [Description("逗号分隔，命中任意一个即推送，如 4K,HDR。留空表示不限。")]
        public string IncludeTags { get; set; } = string.Empty;

        /// <summary>
        /// 最低社区评分（0 表示不限）
        /// </summary>
        [DisplayName("最低评分")]
        [Description("只推送社区评分不低于该值的媒体，0 表示不限。")]
        public int MinCommunityRating { get; set; }

        /// <summary>
        /// 最早年份（0 表示不限）
        /// </summary>
        [DisplayName("最早年份")]
        [Description("只推送该年份及以后的作品，0 表示不限。")]
        public int MinYear { get; set; }

        /// <summary>
        /// 忽略包含这些关键字的媒体（逗号分隔）
        /// </summary>
        [DisplayName("忽略关键字")]
        [Description("名称或简介包含这些词就不推送，逗号分隔，如 样片,预告,花絮。")]
        public string ExcludeKeywords { get; set; } = string.Empty;

        /// <summary>
        /// 追剧模式：只推送有人正在看的剧集
        /// </summary>
        [DisplayName("追剧模式")]
        [Description("只推送「已经有人看过该剧」的新剧集，避免整季入库刷屏。")]
        public bool FollowOnly { get; set; }

        /// <summary>
        /// 复制一份配置（用于按通知条目覆盖字段，避免污染全局配置）
        /// </summary>
        public PluginOptions Clone()
        {
            return (PluginOptions)MemberwiseClone();
        }
    }
}
