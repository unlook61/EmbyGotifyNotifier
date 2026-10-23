# Emby Gotify 通知插件（Emby.GotifyNotifier）

> ⚠️ **本仓库及其中全部内容（包括但不限于源码、文档、截图）完全由 AI 生成和制作。**
> 未经许可，禁止对仓库内容进行任何形式的修改、复制或二次分发。

将 Emby 的**媒体库入库通知**通过 [Gotify](https://gotify.net) 推送到手机。

- **入库推送**（`library.new`）+ **媒体删除**（`library.deleted`）
- **播放通知**：谁、在哪台设备上开始/暂停/停止了播放（`playback.*`）
- **系统与任务事件**：服务器启动、需要重启、有新版本、计划任务完成/失败、用户评分
- 一次扫描新增大量媒体时，多条通知**自动合并**为一条，避免手机连续震动
- 通知可附带海报/横版剧照，点击通知直达 Emby 详情页（需配置外部访问地址）
- 自定义标题/正文模板，支持 `{SeriesName}` `{UserName}` `{DeviceName}` `{Rating}` `{Genres}` 等变量
- **Markdown 排版**、**规则过滤**（媒体类型/媒体库/标签/评分/年份/关键字）、**追剧模式**、**每条通知独立覆盖优先级与模板**
- 配置页自带「发送测试通知」，按当前模板生成示意通知，直接使用页面填写的内容测试，无需先保存
- Token 只以掩码显示（`A1b2********wxyz`），接口与页面均不出现明文
- 配置页内置**诊断面板**：查看哪些用户订阅了入库通知、最近推送记录，并可「模拟一次入库」快速定位真实入库不推送的原因
- 已在 Emby Server **4.10.1.0** 实机验证（插件加载、配置页、测试推送、入库通知与合并去重全部通过）；netstandard2.0，4.8 及以上版本均可加载

## 文件说明

| 文件 | 说明 |
| --- | --- |
| `src/` | 插件源码（C#，.NET SDK 8 可重新编译） |
| `README.md` | 使用、构建与常见问题文档 |
| `screenshots/` | 配置页界面截图 |

> 编译好的插件 `Emby.GotifyNotifier.dll` 在 **Releases** 页下载（`netstandard2.0`，直接放入 Emby 插件目录即可）；也可以按第九节自行编译。

## 一、Gotify 端准备（2 分钟）

1. 打开你的 Gotify 网页端（如 `https://gotify.example.com`），登录。
2. 进入 **应用（Applications）** → **创建应用**，名称随意（如 `Emby`）。
3. 复制该应用的 **Token**（形如 `AxxxxxxxxXXXXX.xxxxxxxxxxxxx`），下一步使用。
4. 手机安装 Gotify Android 客户端，登录同一账号即可接收推送。

## 二、安装插件

1. 从本仓库 **Releases** 页下载 `Emby.GotifyNotifier.dll`（或按第九节自行编译，产物在 `src/bin/Release/netstandard2.0/`）。
2. 把它复制到 Emby 服务器的插件目录，然后**重启 Emby Server**。

各安装方式对应的插件目录：

| 安装方式 | 插件目录 |
| --- | --- |
| Linux 官方 deb/rpm 包 | `/opt/emby-server/system/plugins/` |
| Docker（`emby/embyserver` 镜像） | 挂载的 `/config/plugins/`（即宿主机的 `.../plugins/`） |
| Windows 安装版 | 安装目录下 `system\plugins\` |
| Synology / 群晖 套件 | 套件目录下 `system/plugins/` |

> 注意：DLL 需**平铺**放在 plugins 目录根下（不要新建子目录），文件名保持 `Emby.GotifyNotifier.dll`。
>
> 插件配置保存于 `{programdata}/plugins/configurations/Gotify Notifications.json`。

重启后可在 **设置 → 插件** 中看到 `Gotify Notifications`。

## 三、插件配置

进入 **设置 → 插件 → Gotify Notifications**，点击插件即可打开配置页（自包含页面，无需任何前端框架），填写：

| 选项 | 说明 |
| --- | --- |
| Gotify 服务器地址 | 如 `https://gotify.example.com`，末尾不带斜杠 |
| 应用 Token | 第一步创建的 Gotify 应用 Token。**页面只显示掩码**（如 `A1b2********wxyz`），源码与接口响应中均不出现明文；不修改即保留原值。旁边的「显示」仅切换掩码的可读性，「清除」可删除已保存的 Token（需再点保存）|
| 消息优先级 | 0–10，默认 5。数值越大提醒越强 |
| 附带海报大图 | 推送中附带海报/封面。**需要 Emby 已配置「远程访问」外部地址**，否则图片链接手机打不开 |
| 跳过 HTTPS 证书校验 | Gotify 使用自签名证书时勾选 |
| 标题模板 | 通知标题，默认 `🎬 新片入库` |
| 内容模板 | 通知正文，默认 `{TitleLine}\n\n{Overview}` |
| 合并入库通知 | 开启后，同一时间窗口内的多条入库通知合并为一条（默认开启） |
| 合并窗口（秒） | 合并等待时间，默认 10 秒，最小 2 秒 |

**「内容增强」区域：**

| 选项 | 说明 |
| --- | --- |
| Markdown 排版 | 正文用 Markdown 生成（加粗标题、评分/年份/类型一行、简介引用块）。仅在正文模板为默认值时生效 |
| 使用横版剧照 | 通知大图用 16:9 剧照而非竖版海报；没有剧照时自动回退 |
| 外部访问地址 | 如 `https://emby.example.com`。填写后点通知直达该媒体的详情页 |

**「过滤与个性化」区域：**

| 选项 | 说明 |
| --- | --- |
| 启用过滤规则 | 总开关；关闭时全部推送。系统/计划任务类通知不受过滤影响 |
| 媒体类型 | 逗号分隔白名单：`Movie`、`Episode`、`Series`、`Audio` 等，留空不限 |
| 媒体库 | 勾选后只推送这些媒体库，不勾选表示全部 |
| 包含标签 | 命中任意一个标签即推送，留空不限 |
| 最低评分 | 只推社区评分 ≥ 该值的作品；**未评分的不受限制** |
| 最早年份 | 只推该年份及以后的作品；**无年份信息的不受限制** |
| 忽略关键字 | 名称或简介包含即不推送，逗号分隔 |
| 追剧模式 | 只推「已经有人看过该剧」的新剧集；电影不受影响 |

配置页底部有两个按钮：

- **发送测试通知**：按**当前编辑好的标题/正文模板**生成一条**示意通知**推送到手机，排版与真实入库通知完全一致（示例数据：《星际穿越》(2014)、示例剧集 S01E02）。不需要先保存，直接使用页面上的服务器地址、Token 与模板；Token 显示为掩码时自动沿用已保存的真实 Token。结果会同时显示在页面顶部的状态条和弹窗里，失败时给出具体原因（如 `Token 无效或已失效`、`目标端口拒绝连接`、`Emby 对该地址有 30 秒冷却`）。
- **保存**：将配置写入 `{programdata}/plugins/configurations/Gotify Notifications.json`，即时生效（无需重启）。

> 页面最下方会显示插件版本（如 `Gotify Notifications v2.2.0`）。若看不到版本号，说明加载的仍是旧 DLL，请确认文件已替换并重启过 Emby。
>
> 关于 Token 存储：配置页与 HTTP 接口只返回掩码，浏览器里不会出现明文；但服务端调用 Gotify 需要明文，因此 `{programdata}/plugins/configurations/Gotify Notifications.json` 内仍是明文，请确保该目录仅管理员可读。

## 四、启用通知（关键步骤）

插件安装后还需要在 Emby 通知设置中**添加并勾选事件**，否则不会推送（测试按钮能收到、真实入库收不到，九成是这一步没做）：

1. 进入 **设置 → 通知**（Notifications）。
2. 点 **Add Notification / 添加通知**，在列表中选择 **Gotify**。
3. 弹窗中可填「备注名称」（可留空），在 **Events → Library** 下勾选 **New Media Added（新媒体入库）**，也可同时勾选“媒体已移除”等。
4. 点弹窗底部的 **Add Notification / 添加通知** 保存。之后扫描发现新媒体即会推送。

每条通知还可以**单独覆盖**优先级、标题模板与正文模板（在弹窗中填写，留空沿用插件设置里的全局模板）——例如给「播放通知」单独设低优先级、给「入库通知」用不同的标题。

> 弹窗里的 **Send Test Notification** 也会走 Emby 的通知链路发送一条示例通知（按你配置的模板渲染），可用来快速验证。
>
> 插件通过 Emby 官方通知框架（`IUserNotifier`）接入，事件由 Emby 服务端在媒体入库时触发；插件配置页的「发送测试通知」不经过用户订阅，仅验证 Gotify 连通性。

## 五、模板变量

标题模板和内容模板支持以下变量（大小写不敏感）。

配置页里每个模板输入框下方都列出了可用变量徽标：**鼠标悬停**即可看到含义与示例，**点击**可直接插入到光标位置。

| 变量 | 含义 | 示例 |
| --- | --- | --- |
| `{TitleLine}` | 自动拼装的中文标题行 | `《阿凡达》(2009)`、`《某剧》S01E02 第二集` |
| `{Name}` | 媒体自身名称 | `阿凡达` |
| `{SeriesName}` | 所属剧集/专辑名 | `某剧` |
| `{Season}` | 季号（数字，电影为 0） | `1` |
| `{Episode}` | 集号（数字，电影为 0） | `2` |
| `{Year}` | 年份 | `2009` |
| `{Type}` | Emby 媒体类型 | `Movie` / `Episode` |
| `{TypeName}` | 中文类型名 | `电影` / `剧集` |
| `{Overview}` | 简介（无元数据时为空） | `...` |
| `{Title}` | Emby 原始通知标题 | `Avatar has been added to ...` |
| `{ServerName}` | Emby 服务器名称 | `MyEmby` |
| `{Url}` | 媒体详情页链接（Emby 提供时） | `http://...` |
| `{UserName}` | 触发事件的用户名 | `alice` |
| `{DeviceName}` | 播放设备名（播放事件） | `客厅电视` |
| `{ClientName}` | 播放客户端（播放事件） | `Emby for Android` |
| `{Progress}` | 播放进度（播放事件） | `12:34` |
| `{Genres}` | 类型标签 | `科幻 / 冒险` |
| `{Rating}` | 社区评分 | `8.7` |
| `{Runtime}` | 片长 | `128 分钟` |
| `{OfficialRating}` | 分级 | `PG-13` |
| `{Event}` | 事件 ID | `library.new` |
| `{EventName}` | 事件中文名称 | `新媒体入库` / `媒体被删除` |
| `{EventEmoji}` | 事件图标（emoji） | `🎬` / `🗑` / `▶` |
| `{Date}` | 入库时间 | `2026-10-22 11:30` |
| `{Count}` | 合并通知的条目数 | `12` |

示例：

- 标题模板：`📺 {SeriesName} 更新了`（内容模板 `S{Season}E{Episode} {Name}`）
- 标题模板：`新片入库 {Count} 部`

## 六、支持的事件与内置标题

在「设置 → 通知」中为 Gotify 勾选事件即可订阅；未自定义标题模板时，插件会按事件使用内置中文标题：

| 事件 | 说明 | 内置标题示例 |
| --- | --- | --- |
| `library.new` | 新媒体入库 | 🎬 新片入库 |
| `library.deleted` | 媒体被删除 | 🗑 已删除 |
| `playback.start` / `pause` / `unpause` / `stop` | 播放开始/暂停/继续/停止 | ▶ alice 开始播放 |
| `scheduledtasks.completed` / `failed` | 计划任务完成/失败 | ✅ 计划任务完成 |
| `system.serverstartup` | 服务器启动 | 🚀 Emby 已启动 |
| `system.updateavailable` | 有新版本 | ⬆ Emby 有新版本 |
| `system.serverrestartrequired` | 需要重启 | 🔁 Emby 需要重启 |
| `item.rate` | 用户评分 | ⭐ alice 评分 |

> 注意：Emby 4.10 **没有 `library.updated` 事件**，「媒体更新」通知实际不会触发。
> 自定义了标题模板时，所有事件都会套用该模板（`{TitleLine}` `{UserName}` 等变量按事件填充）。

## 七、合并通知效果

默认开启合并。一次扫描新增 N 条媒体时，只会收到 1 条推送：

- 标题：套用标题模板（变量按第一条媒体渲染）；模板含 `{Count}` 时替换为条数，否则自动追加 `（N 项）`。未设置模板时标题为 `🎬 新片入库（N 项）`
- 正文：固定为条目列表——最多列出前 10 项名称，超出显示"……等共 N 项"，并附带第一条的简介（合并时不再套用正文模板）
- 只新增 1 条时，完全按标题/正文模板渲染（与示意通知一致，含简介、海报）

> 多个用户都订阅了 Gotify 通知时，**同一部影片只会推送一次**（按「事件 + 媒体 Id」60 秒去重），不会因为用户多而重复轰手机。关闭合并时同样生效。

## 八、常见问题

**点击插件没反应 / 没有弹窗？**

1. 确认 Emby 版本与插件版本：`设置 → 插件` 中应显示 `Gotify Notifications 2.2.0.0`；配置页底部应有 `Gotify Notifications v2.2.0`。
2. 替换 DLL 后必须**重启 Emby Server**（不是刷新网页）。
3. 浏览器可能缓存了旧的插件页面：在配置页按 `Ctrl + F5`（Mac 为 `Cmd + Shift + R`）强制刷新。
4. 也可直接访问：`http://<Emby地址>:8096/web/index.html#!/configurationpage?name=gotifynotifierconfig`
5. 若页面能打开但点按钮无任何反应，请按 `F12` 打开浏览器控制台，查看是否有红色报错并反馈。

**点了测试按钮，手机没有弹窗？**

1. 看配置页顶部状态条：会明确显示成功或失败原因，不再一律提示"已发送"。
2. 失败常见原因：Token 填成了 Gotify 用户密码（应为「应用」的 Token）、地址多了 `/`、Emby 服务器访问不到 Gotify。
3. 成功但手机没收到：检查手机 Gotify 客户端是否登录同一账号、是否被系统省电策略限制后台。

**在「设置 → 通知」里点击 Gotify 没有任何反应？**

v1.4.0 及更早版本存在此问题：插件未提供通知条目编辑器（`SetupModuleUrl` 为 `null`），Emby 加载编辑器时抛错，界面静默无响应。**v1.5.0 已修复**——点击后会弹出配置窗口（备注名称 + 事件列表）。

若升级后仍无反应：

1. 确认插件版本为 `2.2.0.0`，并已重启 Emby；
2. 按 `Ctrl + F5` 强制刷新，避免浏览器缓存旧页面；
3. 按 `F12` 打开控制台，若看到 `configurationpage?name=gotifynotifyentryeditor` 404，说明新 DLL 未生效。

**测试能收到，但真实影视入库收不到？**（最常见问题）

> 原因几乎都不是插件发送失败——`发送测试通知` 直接调用插件发送，绕过了 Emby 的「用户通知配置」；而真实入库必须先由 Emby 通知系统判定"这个用户订阅了 library.new"，才会调用插件。

配置页底部新增了 **诊断** 区域，按下面三步即可定位：

1. **看「用户通知配置」**
   - 显示红色「没有任何用户启用 Gotify 通知」→ 去 **设置 → 通知**，点「添加通知」，服务选 **Gotify**，勾选 **新媒体入库**，再点页面底部的**保存**。
   - 显示「已关闭」→ 打开该通知的开关并保存。
   - 事件里没有 `library.new` → 勾选「新媒体入库」。
2. **点「模拟一次入库」**
   - 手机能收到 → 链路完全正常，问题在于**媒体没有被判定为新增**：文件早已入库（只是元数据/字幕更新）、入库时 Emby 未运行、或手动"刷新元数据"不会触发 `library.new`。换一部真正的新影片再试。
   - 手机收不到，但「最近通知记录」里出现 `失败` → 看失败原因（Token 无效、连接被拒、超时等）。
   - 点完「最近通知记录」仍为空 → Emby 没有把事件路由给插件，回到第 1 步检查通知配置。
3. **看「最近通知记录」**
   - `排队中`：已收到事件，正在等合并窗口（默认 10 秒）结束，稍等即可。
   - `已发送`：已成功推送到 Gotify；若手机仍无提示，检查手机端权限/省电策略。
   - `失败` / `已跳过`：括号内即原因。

其他排查点：

- 服务器地址是否可从 Emby 服务器访问（在 Emby 服务器上 `curl http://<gotify地址>/version` 测试）；
- 查看 `{programdata}/logs/embyserver.txt` 中 `[GotifyNotifier]` 开头的记录，入库时会打印"入库通知已加入合并队列"。

**收不到图？** 海报需要 Emby 配置了「远程访问 → 外部域名/地址」，且该地址手机可达；未配置时自动发送无图通知。

**Gotify 是自签名 HTTPS？** 勾选"跳过 HTTPS 证书校验"。

**Docker 用户**：确认 Gotify 地址写的是容器视角可访问的地址（如 `http://宿主机IP:8080`），不要写 `localhost`。

**卸载**：删除插件 DLL 并重启 Emby；配置文件位于 `{programdata}/plugins/configurations/Gotify Notifications.json`，可一并删除。

## 九、重新编译（可选）

```bash
cd src
dotnet build -c Release
# 产物：src/bin/Release/netstandard2.0/Emby.GotifyNotifier.dll
```

编译环境：.NET SDK 8，依赖 NuGet 包 `mediabrowser.server.core`（4.8.11，提供 Emby 插件 API 引用，netstandard2.0 产物可运行于 Emby 4.8 及以上各版本）。

## 技术说明

- 插件入口实现 `BasePlugin + IHasWebPages`，通过嵌入资源提供**自包含配置页**（`configurationpage?name=gotifynotifierconfig`，HTML + JS 控制器，参考官方 CinemaMode 插件模式），不依赖 genericui 前端框架。
- 配置读写通过插件 API 服务 `GotifyNotifierService`（`BaseApiService`）暴露：`GET/POST /emby/GotifyNotifier/Configuration`、`POST /emby/GotifyNotifier/Test`（示意通知）、`GET /emby/GotifyNotifier/Diagnostics`（诊断）、`POST /emby/GotifyNotifier/SimulateIngest`（模拟入库），均限管理员角色。GET 只返回 Token 掩码 + `HasToken`，POST/Test 收到掩码时用已保存的真实 Token 回填（`TokenHelper`）。
- 诊断通过 `IUserManager.GetTypedUserSetting(userId, "usernotifications")` 读取用户通知配置（该类型的强类型定义在 Emby 内部，这里经 JSON 中转成插件自己的轻量 DTO），并用 `IApplicationHost.GetExports<IUserNotifier>()` 确认通知器已注册。
- 模拟入库调用 `INotificationManager.SendNotification(new NotificationRequest { EventId = "library.new", ... })`，与真实入库共用同一条 Emby 通知路由（含用户订阅过滤），仅事件来源不同。
- `NotificationHistory` 在内存里保存最近 30 次处理结果（sent/failed/queued/skipped），供诊断面板展示；重启 Emby 后清空。
- 过滤规则由 `NotificationFilter` 实现，任一规则异常时放行，避免误吞通知；追剧模式通过 `ILibraryManager.GetItemList(IsPlayed/IsResumable)` 按单集判断。
- 每条通知的覆盖字段存于通知条目的 `Options` 字典（`Priority`/`TitleTemplate`/`MessageTemplate`），由 `Notifier.ApplyEntryOverrides` 在发送前应用。
- 示意通知由 `SampleNotification` 按 `TitleTemplate` / `MessageTemplate` 渲染，变量替换逻辑与真实通知保持一致。
- 配置以 JSON 持久化到 `{programdata}/plugins/configurations/Gotify Notifications.json`（`ConfigurationStore` 负责读写与缓存）。
- 通知器实现 `IUserNotifier`（`Name`/`Key`/`SetupModuleUrl`/`SendNotification`），由 Emby MEF 自动发现，用户在通知页面勾选事件后由服务端触发。`SetupModuleUrl` 指向嵌入的条目编辑器 `gotifynotifyentryeditor`（`Configuration/entryeditor.js`），Emby 在「设置 → 通知」点击服务时 require 该模块渲染表单；返回 `null` 会导致点击无响应。
- 推送调用 Gotify REST API：`POST {server}/message?token={token}`，请求体 `{"message","title","priority","extras"}`，其中 `extras.client::notification` 携带点击跳转（`click.url`）与大图（`bigImageUrl`）。发送逻辑封装在 `GotifyClient`，入库通知与测试通知共用，返回 `GotifySendResult`（含 HTTP 状态码、错误说明与 `Retryable` 标记）；`Retryable` 为 true 时自动重试一次（间隔 2 秒）。
- 入库通知在合并窗口内按媒体 Id 去重：Emby 对同一文件可能重复触发 `ItemAdded`（发现文件一次、元数据刷新后再一次），去重后不会出现"2 项"却只有一部影片的情况。
- 日志中不会输出含 Token 的完整 URL（已脱敏），Emby 的 HTTP 请求日志对插件请求已关闭。

## 版本记录

### v2.2.0（当前）

- 新增 `{EventEmoji}` 变量：渲染为事件对应的 emoji（`🎬` `🗑` `▶` `⏸` `✅` `🚀` `⭐` 等），与 `{EventName}` 搭配即可还原内置标题，如 `🎬 新媒体入库`。

### v2.1.0

- 新增 `{EventName}` 变量：渲染为事件的中文名称（`新媒体入库`、`媒体被删除`、`开始播放`、`计划任务完成` 等），标题/正文模板均可使用；`{Event}` 变量同步加入标题模板可用列表。

### v2.0.0

- **事件扩展**：支持播放（开始/暂停/继续/停止，带用户与设备）、计划任务完成/失败、服务器启动/需要重启/有新版本、媒体删除、用户评分；每个事件都有内置中文标题，自定义模板时全部套用。
- **规则过滤**：媒体类型、媒体库（多选）、标签、最低评分、最早年份、忽略关键字；被过滤的通知在诊断面板显示原因。系统/任务类通知不受过滤影响。
- **追剧模式**：只推「已经有人看过该剧」的新剧集（按单集观看记录判断，整剧标记已看也算）；电影不受影响。
- **Markdown 排版**：默认正文改为加粗标题 + 评分/年份/类型一行 + 简介引用块。
- **横版剧照**：通知大图可选 16:9 Backdrop，无剧照自动回退海报。
- **深链跳转**：配置「外部访问地址」后，点通知直达该媒体详情页（`#!/item?id=...&serverId=...`）。
- **每条通知独立覆盖**：通知条目编辑器支持单独设置优先级、标题模板、正文模板，留空沿用全局。
- **模拟事件增强**：`POST /emby/GotifyNotifier/SimulateIngest` 支持 `EventId`、`UserId` 参数，可模拟播放、评分等任意事件验证链路。
- 新模板变量：`{UserName}` `{DeviceName}` `{ClientName}` `{Progress}` `{Genres}` `{Rating}` `{Runtime}` `{OfficialRating}`。

### v1.6.0

- **发送失败自动重试一次**：遇到 `5xx`、`429`、`408` 或连接类错误（端口拒绝、DNS 临时故障等）时，间隔 2 秒重试一次，降低 Gotify 重启/网络抖动导致的丢通知概率。4xx（Token 无效、地址错误）、证书错误、请求超时（Emby 对同一地址有 30 秒冷却）不重试，避免无意义的等待与重复请求。
- **支持清除 Token**：配置页 Token 右侧新增「清除」，点击后输入框清空并提示“尚未保存”，保存后服务端 Token 置空（接口返回 `HasToken: false`）；重新输入即可再次保存。

### v1.5.1

- **修复模板变量大小写**：此前字典按忽略大小写匹配，但实际替换用的是 `string.Replace`（区分大小写），导致 `{name}`、`{TITLELINE}` 这类写法原样输出。现在统一由 `TemplateRenderer` 用正则替换，文档承诺的“大小写不敏感”真正生效，未知变量保持原样。
- **同一媒体 60 秒内只推送一次**：Emby 会为**每个订阅了该事件的用户**各调用一次通知器，此前多个用户都启用时（或关闭合并时）同一部影片会收到多条完全相同的推送。现在按「事件 + 媒体 Id」做 60 秒去重，重复事件记为 `已跳过` 并在诊断面板显示。

### v1.5.0

- **修复「设置 → 通知」中点击 Gotify 没有反应**：Emby 4.10 的通知编辑器会用 `require(getConfigurationResourceUrl(entry.SetupModuleUrl))` 加载服务自带的表单，插件此前返回 `null` 导致 `null.toLowerCase()` 抛错、界面静默。现在提供 `Configuration/entryeditor.js` + 模板，点击后正常弹出配置窗口（备注名称 + 事件列表 + 发送测试通知）。
- **Emby 自带测试通知按模板渲染**：从「设置 → 通知」点 **Send Test Notification** 时，Emby 不传媒体项，之前会渲染出 `📺 ｜ (0)` 这样的空变量；现在自动改用示意数据（与插件配置页的「示意通知」一致）。

### v1.4.0

- **新增「诊断」面板**（配置页底部），用于定位"测试能收到、真实入库收不到"：
  - 显示插件通知器是否已注册、服务器/Token 是否就绪、合并窗口设置；
  - 列出**哪些用户启用了 Gotify 通知、勾选了哪些事件**——这是真实入库不推送的头号原因；
  - 「最近通知记录」展示最近 15 次处理：`排队中 / 已发送 / 失败 / 已跳过`，失败带具体原因；
  - 根据当前状态给出对应排查建议。
- **新增「模拟一次入库」**：通过 Emby 通知系统（`INotificationManager` → 用户通知配置 → 插件）发送一条真实 `library.new` 通知。能收到说明链路正常，收不到则说明通知未启用或配置有问题，一步区分故障位置。

### v1.3.0

- **Token 不再明文显示**：`GET /GotifyNotifier/Configuration` 只返回掩码（前 4 位 + `********` + 后 4 位）与 `HasToken` 标志，接口响应、页面源码、浏览器表单里都不再出现明文 Token；输入框为密码类型，可点「显示」临时查看掩码。保存/测试时若提交的是掩码，服务端自动沿用已保存的真实 Token。
- **变量悬浮提示**：标题/正文模板下方新增变量徽标，悬停显示含义与示例，点击插入到光标处。
- **测试通知改为"示意通知"**：按当前标题/正文模板渲染一条示例通知（《星际穿越》(2014)、示例剧集 S01E02），手机上的排版与真实入库通知一致，结尾附「测试消息」标记。
- **错误提示更准确**：区分端口拒绝、DNS 解析失败、证书校验失败，以及 Emby 对同一地址的 30 秒冷却（上一次请求超时后触发）。
- **修复合并队列中的模板失效**：开启「合并入库通知」时，单条入库此前只替换 `{Count}`，其他变量会原样发出（如标题变成 `📺 {TypeName}｜{Name}`）；现在入队即按模板渲染，单条与合并标题的变量都能正确替换。

### v1.2.0

- **修复"点击测试按钮没有反应/没有弹窗"**：
  - 前端明确指定 `dataType: 'json'`。此前 Emby 的 `ApiClient.ajax` 返回的是原始 Response 对象，`result.Message` 永远为空，界面固定显示"测试通知已发送"，实际可能一条都没发（假成功）。
  - 测试接口改为接收**页面当前填写的内容**（`request.Options`），未保存也能测试，不会误用旧的空配置。
  - 新增页面内状态条（成功/失败/进行中），不再只依赖弹窗模块；弹窗对 ES module 形式的 `alert` 做兼容，最差退回原生 `alert`。
  - 失败时展示具体原因与 HTTP 状态码（如 401 Token 无效、连接被拒绝、超时）。
- **修复保存按钮点不动**：原样式类 `.button-submit` 与 Emby 页头"Get Emby Premiere"按钮重名，导致测试脚本/误点命中了别的按钮；改用独立的 `.btnSave`，保存后状态条即时反馈。
- **修复重复入库通知**：同一媒体在合并窗口内只计一次。
- 配置页底部显示插件版本，便于确认 DLL 是否已更新。

### v1.1.0

- 改为自包含配置页（`BasePlugin + IHasWebPages`），修复点击插件无反应。
- 新增「发送测试通知」按钮与对应 API。

### v1.0.0

- 首个版本：`library.new` 入库通知经 Gotify 推送到手机。
