using System;
using System.Collections.Generic;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// Emby 插件入口。实现 <see cref="IHasWebPages"/> 后，
    /// Emby 会在「设置 - 插件」列表中显示本插件，点击后打开
    /// 嵌入的 configurationpage 配置页（自包含 HTML/JS，不依赖 genericui）。
    /// </summary>
    public class Plugin : BasePlugin, IHasWebPages
    {
        /// <summary>
        /// 固定插件 Id（同一插件的多个版本必须保持一致，否则会被视为新插件）
        /// </summary>
        private static readonly Guid PluginId = new Guid("6f2b8c34-9a1d-4e7b-b5c2-8d3e6f1a0c47");

        /// <summary>
        /// 主配置页名称（配置页 URL: configurationpage?name=gotifynotifierconfig）
        /// </summary>
        public const string ConfigPageName = "gotifynotifierconfig";

        /// <summary>
        /// 配置页控制器 JS 页面名称（由 HTML 的 data-controller="__plugin/..." 引用）
        /// </summary>
        public const string ConfigPageJsName = "gotifynotifierjs";

        /// <summary>
        /// 通知条目编辑器模块名称（SetupModuleUrl），
        /// Emby 的「设置 → 通知」点击本服务时加载它渲染弹窗
        /// </summary>
        public const string EntryEditorName = "gotifynotifyentryeditor";

        /// <summary>
        /// 通知条目编辑器的模板页面名称（由编辑器通过 text! 加载）
        /// </summary>
        public const string EntryEditorTemplateName = "gotifynotifyentryeditortemplate";

        private readonly IApplicationHost _applicationHost;

        /// <summary>
        /// 配置存储实例（与 API 服务、通知器共享同一缓存）
        /// </summary>
        public ConfigurationStore Store { get; }

        public Plugin(IApplicationHost applicationHost)
        {
            _applicationHost = applicationHost;

            ILogger logger = applicationHost.Resolve<ILogManager>().GetLogger(Name);
            Store = new ConfigurationStore(
                applicationHost.Resolve<IApplicationPaths>(),
                applicationHost.Resolve<IJsonSerializer>(),
                logger);
        }

        public override string Name => "Gotify Notifications";

        public override string Description => "通过 Gotify 服务器向手机推送 Emby 媒体库入库等通知。";

        public override Guid Id => PluginId;

        /// <summary>
        /// 当前配置（供 <see cref="Notifier"/> 等内部组件读取）
        /// </summary>
        public PluginOptions CurrentOptions
        {
            get { return Store.GetOptions(); }
        }

        /// <summary>
        /// 保存配置（供 API 服务调用）
        /// </summary>
        public void SaveOptions(PluginOptions options)
        {
            Store.SaveOptions(options);
        }

        /// <summary>
        /// 注册插件页面：主配置页（HTML）+ 控制器（JS）。
        /// HTML 根元素通过 data-controller="__plugin/gotifynotifierjs"
        /// 关联嵌入的 JS 模块，页面加载时由 Emby 前端自动 require。
        /// </summary>
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new List<PluginPageInfo>
            {
                new PluginPageInfo
                {
                    Name = ConfigPageName,
                    DisplayName = "Gotify 通知设置",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configpage.html"
                },
                new PluginPageInfo
                {
                    Name = ConfigPageJsName,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configpage.js",
                    IsMainConfigPage = false
                },
                // 「设置 → 通知」里的条目编辑器（缺失会导致点击服务无反应）
                new PluginPageInfo
                {
                    Name = EntryEditorName,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.entryeditor.js",
                    IsMainConfigPage = false
                },
                new PluginPageInfo
                {
                    Name = EntryEditorTemplateName,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.entryeditor.template.html",
                    IsMainConfigPage = false
                }
            };
        }

        /// <summary>
        /// 从宿主中查找本插件实例（供未通过构造函数获得插件引用的组件使用）
        /// </summary>
        public static Plugin GetPluginInstance(IApplicationHost applicationHost)
        {
            if (applicationHost == null)
            {
                return null;
            }

            foreach (IPlugin plugin in applicationHost.Plugins)
            {
                Plugin instance = plugin as Plugin;
                if (instance != null)
                {
                    return instance;
                }
            }

            return null;
        }
    }
}
