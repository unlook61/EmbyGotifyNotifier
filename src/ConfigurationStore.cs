using System;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace Emby.Plugin.GotifyNotifications
{
    /// <summary>
    /// 插件配置存储。以 JSON 形式持久化到
    /// {PluginConfigurationsPath}/Gotify Notifications.json，
    /// 与 Emby 其他插件（如 CinemaMode 的命名配置）保持一致的目录约定。
    /// </summary>
    public class ConfigurationStore
    {
        /// <summary>配置文件名（含扩展名）</summary>
        public const string FileName = "Gotify Notifications.json";

        private readonly IApplicationPaths _applicationPaths;
        private readonly IJsonSerializer _jsonSerializer;
        private readonly ILogger _logger;

        /// <summary>缓存当前配置，避免每次读取都解析 JSON</summary>
        private PluginOptions _cachedOptions;

        /// <summary>读写锁，防止并发写入损坏文件</summary>
        private readonly object _syncLock = new object();

        public ConfigurationStore(
            IApplicationPaths applicationPaths,
            IJsonSerializer jsonSerializer,
            ILogger logger)
        {
            _applicationPaths = applicationPaths;
            _jsonSerializer = jsonSerializer;
            _logger = logger;
        }

        /// <summary>
        /// 配置文件的完整路径
        /// </summary>
        public string ConfigurationFilePath
        {
            get { return Path.Combine(_applicationPaths.PluginConfigurationsPath, FileName); }
        }

        /// <summary>
        /// 取得当前配置。文件不存在或解析失败时返回默认配置。
        /// </summary>
        public PluginOptions GetOptions()
        {
            lock (_syncLock)
            {
                if (_cachedOptions != null)
                {
                    return _cachedOptions;
                }
            }

            PluginOptions options = LoadFromDisk();

            lock (_syncLock)
            {
                if (_cachedOptions == null)
                {
                    _cachedOptions = options;
                }

                return _cachedOptions;
            }
        }

        /// <summary>
        /// 保存配置到磁盘并更新缓存
        /// </summary>
        public void SaveOptions(PluginOptions options)
        {
            if (options == null)
            {
                return;
            }

            try
            {
                string json = _jsonSerializer.SerializeToString(options);
                string path = ConfigurationFilePath;

                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json);

                lock (_syncLock)
                {
                    _cachedOptions = options;
                }

                _logger.Info("[GotifyNotifier] 配置已保存: {0}", path);
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 保存配置失败", ex);
            }
        }

        /// <summary>
        /// 从磁盘读取配置；失败时返回默认配置
        /// </summary>
        private PluginOptions LoadFromDisk()
        {
            try
            {
                string path = ConfigurationFilePath;
                if (!File.Exists(path))
                {
                    return new PluginOptions();
                }

                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new PluginOptions();
                }

                PluginOptions options = _jsonSerializer.DeserializeFromString<PluginOptions>(json);
                return options ?? new PluginOptions();
            }
            catch (Exception ex)
            {
                _logger.ErrorException("[GotifyNotifier] 读取配置失败，使用默认配置", ex);
                return new PluginOptions();
            }
        }
    }
}
