define(['baseView', 'loading', 'globalize', 'emby-linkbutton', 'emby-checkbox', 'emby-input', 'emby-button', 'emby-scroller', 'emby-toggle', 'emby-textarea'], function (BaseView, loading, globalize) {
    'use strict';

    // 与插件 AssemblyVersion 保持一致，方便用户确认当前加载的是哪一版 DLL
    var PluginVersion = '2.3.0';

    /**
     * 模板变量说明。groups 决定该变量出现在「标题模板」还是「正文模板」下方。
     */
    var TemplateVariables = [
        {
            token: '{TitleLine}',
            label: '标题行',
            desc: '插件自动拼装的中文标题行（电影带年份，剧集带 SxxExx）',
            example: '《星际穿越》(2014) / 《某剧》S01E02 第二集',
            groups: ['title', 'message']
        },
        {
            token: '{Name}',
            label: '名称',
            desc: '媒体自身的名称，不含年份与季集',
            example: '星际穿越',
            groups: ['title', 'message']
        },
        {
            token: '{SeriesName}',
            label: '剧集名',
            desc: '剧集所属的剧名；电影为空',
            example: '某剧',
            groups: ['title', 'message']
        },
        {
            token: '{TypeName}',
            label: '类型',
            desc: '媒体类型的中文名称',
            example: '电影 / 剧集 / 音乐',
            groups: ['title', 'message']
        },
        {
            token: '{Year}',
            label: '年份',
            desc: '上映/发行年份',
            example: '2014',
            groups: ['message']
        },
        {
            token: '{Season}',
            label: '季号',
            desc: '剧集的季号（数字，无前导零）',
            example: '1',
            groups: ['message']
        },
        {
            token: '{Episode}',
            label: '集号',
            desc: '剧集的集号（数字，无前导零）',
            example: '2',
            groups: ['message']
        },
        {
            token: '{Overview}',
            label: '简介',
            desc: '媒体简介；没有元数据时为空',
            example: '影片的剧情简介……',
            groups: ['message']
        },
        {
            token: '{ServerName}',
            label: '服务器名',
            desc: 'Emby 服务器名称',
            example: '我的 Emby',
            groups: ['title', 'message']
        },
        {
            token: '{Date}',
            label: '时间',
            desc: '入库时间，格式 yyyy-MM-dd HH:mm',
            example: '2026-10-22 18:30',
            groups: ['title', 'message']
        },
        {
            token: '{Count}',
            label: '条数',
            desc: '合并通知中的媒体条数；单条时为 1',
            example: '12',
            groups: ['title', 'message']
        },
        {
            token: '{Url}',
            label: '链接',
            desc: '媒体详情页地址（Emby 提供时）',
            example: 'https://emby.example.com/web/index.html#!/item?id=123',
            groups: ['message']
        },
        {
            token: '{Type}',
            label: '类型 ID',
            desc: 'Emby 内部类型标识（英文）',
            example: 'Movie / Episode',
            groups: ['message']
        },
        {
            token: '{Title}',
            label: '原始标题',
            desc: 'Emby 生成的原始通知标题（英文环境为英文）',
            example: '星际穿越 已添加到 我的 Emby',
            groups: ['message']
        },
        {
            token: '{Event}',
            label: '事件',
            desc: '触发通知的事件 ID',
            example: 'library.new',
            groups: ['title', 'message']
        },
        {
            token: '{EventName}',
            label: '事件名称',
            desc: '触发通知的中文事件名',
            example: '新媒体入库 / 媒体被删除',
            groups: ['title', 'message']
        },
        {
            token: '{EventEmoji}',
            label: '事件图标',
            desc: '触发事件对应的 emoji（不含文字）',
            example: '🎬 / 🗑 / ▶',
            groups: ['title', 'message']
        },
        {
            token: '{UserName}',
            label: '用户名',
            desc: '触发事件的用户（播放、评分等事件）',
            example: 'alice',
            groups: ['title', 'message']
        },
        {
            token: '{DeviceName}',
            label: '设备名',
            desc: '播放设备名称（播放事件）',
            example: '客厅电视',
            groups: ['message']
        },
        {
            token: '{ClientName}',
            label: '客户端',
            desc: '播放客户端（播放事件）',
            example: 'Emby for Android',
            groups: ['message']
        },
        {
            token: '{Progress}',
            label: '播放进度',
            desc: '当前播放位置（播放事件）',
            example: '12:34',
            groups: ['message']
        },
        {
            token: '{Genres}',
            label: '类型',
            desc: '媒体类型标签，多个用 / 分隔',
            example: '科幻 / 冒险',
            groups: ['message']
        },
        {
            token: '{Rating}',
            label: '评分',
            desc: '社区评分（0-10，一位小数）',
            example: '8.7',
            groups: ['message']
        },
        {
            token: '{Runtime}',
            label: '片长',
            desc: '媒体时长',
            example: '128 分钟',
            groups: ['message']
        },
        {
            token: '{OfficialRating}',
            label: '分级',
            desc: '官方分级（如 PG-13、TV-MA）',
            example: 'PG-13',
            groups: ['message']
        }
    ];

    var StylesInjected = false;

    /**
     * 注入组件样式（只注入一次）
     */
    function injectStyles() {
        if (StylesInjected || document.getElementById('gotifyPluginStyles')) {
            StylesInjected = true;
            return;
        }

        var style = document.createElement('style');
        style.id = 'gotifyPluginStyles';
        style.textContent = [
            '.gotifyVars{display:flex;flex-wrap:wrap;gap:6px;margin:8px 0 4px}',
            '.gotifyVarsLabel{font-size:12px;opacity:.6;margin-right:2px;align-self:center}',
            '.gotifyVar{display:inline-block;padding:2px 9px;border-radius:11px;font-size:12px;line-height:18px;cursor:pointer;',
            'border:1px solid rgba(128,128,128,.45);color:inherit;background:rgba(128,128,128,.12);user-select:none;white-space:nowrap}',
            '.gotifyVar:hover{background:rgba(128,128,128,.28)}',
            '.gotifyVarTip{position:fixed;z-index:100000;max-width:300px;padding:8px 11px;border-radius:6px;',
            'background:rgba(28,28,30,.97);color:#fff;font-size:12px;line-height:1.6;box-shadow:0 6px 20px rgba(0,0,0,.45);',
            'pointer-events:none;display:none;white-space:normal}',
            '.gotifyVarTip b{color:#8ab4f8;font-weight:600}',
            '.gotifyVarTip .ex{opacity:.8;margin-top:2px}',
            '.gotifyLinkButton{background:none;border:0;color:inherit;opacity:.8;cursor:pointer;font-size:inherit;font-family:inherit;padding:0 2px;text-decoration:underline;vertical-align:baseline}',
            '.gotifyLinkButton:hover{opacity:1}'
        ].join('');

        document.head.appendChild(style);
        StylesInjected = true;
    }

    /**
     * 全局浮层提示：用 fixed 定位挂在 body 上，避免被滚动容器裁切
     */
    function ensureTooltip() {
        var tip = document.getElementById('gotifyVarTip');
        if (tip) {
            return tip;
        }

        tip = document.createElement('div');
        tip.id = 'gotifyVarTip';
        tip.className = 'gotifyVarTip';
        document.body.appendChild(tip);
        return tip;
    }

    function showVarTip(chip) {
        var tip = ensureTooltip();
        var name = chip.getAttribute('data-token');
        var desc = chip.getAttribute('data-desc');
        var example = chip.getAttribute('data-example');

        tip.innerHTML = '';

        var title = document.createElement('div');
        title.innerHTML = '<b>' + name + '</b>　' + chip.getAttribute('data-label');
        tip.appendChild(title);

        var descEl = document.createElement('div');
        descEl.textContent = desc;
        tip.appendChild(descEl);

        if (example) {
            var exEl = document.createElement('div');
            exEl.className = 'ex';
            exEl.textContent = '示例：' + example;
            tip.appendChild(exEl);
        }

        var hint = document.createElement('div');
        hint.className = 'ex';
        hint.textContent = '点击可插入到模板光标处';
        tip.appendChild(hint);

        tip.style.display = 'block';

        var rect = chip.getBoundingClientRect();
        var tipRect = tip.getBoundingClientRect();
        var left = Math.min(rect.left, window.innerWidth - tipRect.width - 12);
        var top = rect.top - tipRect.height - 8;

        if (top < 8) {
            top = rect.bottom + 8;
        }

        tip.style.left = Math.max(8, left) + 'px';
        tip.style.top = top + 'px';
    }

    function hideVarTip() {
        var tip = document.getElementById('gotifyVarTip');
        if (tip) {
            tip.style.display = 'none';
        }
    }

    /**
     * 把变量插入到输入框的光标位置
     */
    function insertToken(input, token) {
        if (!input) {
            return;
        }

        var value = input.value || '';
        var start = value.length;
        var end = value.length;

        if (document.activeElement === input && typeof input.selectionStart === 'number') {
            start = input.selectionStart;
            end = input.selectionEnd;
        }

        input.value = value.substring(0, start) + token + value.substring(end);
        input.focus();

        var pos = start + token.length;
        try {
            input.setSelectionRange(pos, pos);
        } catch (e) {
        }
    }

    /**
     * 渲染某一组变量徽标
     */
    function renderVariables(page, group, inputSelector) {
        var container = page.querySelector('.gotifyVars[data-group="' + group + '"]');
        if (!container) {
            return;
        }

        container.innerHTML = '';

        var label = document.createElement('span');
        label.className = 'gotifyVarsLabel';
        label.textContent = '可用变量（悬停查看说明）：';
        container.appendChild(label);

        var input = page.querySelector(inputSelector);

        TemplateVariables.forEach(function (item) {
            if (item.groups.indexOf(group) < 0) {
                return;
            }

            var chip = document.createElement('span');
            chip.className = 'gotifyVar';
            chip.textContent = item.token;
            chip.setAttribute('data-token', item.token);
            chip.setAttribute('data-label', item.label);
            chip.setAttribute('data-desc', item.desc);
            chip.setAttribute('data-example', item.example || '');

            chip.addEventListener('mouseenter', function () {
                showVarTip(chip);
            });
            chip.addEventListener('mouseleave', hideVarTip);

            chip.addEventListener('click', function (e) {
                e.preventDefault();
                hideVarTip();
                insertToken(input, item.token);
            });

            container.appendChild(chip);
        });
    }

    /**
     * 在页面内显示一条状态（不依赖弹窗模块，保证任何情况下都有可见反馈）
     * @param {string} kind ok | error | info
     * @param {string} title
     * @param {string} detail
     */
    function showStatus(kind, title, detail) {
        var el = document.querySelector('.gotifyStatus');
        if (!el) {
            return;
        }

        el.className = 'gotifyStatus gotifyStatus-' + kind;
        el.innerHTML = '';

        // 用内联样式，避免不同 Emby 版本主题类名差异导致状态条不可见
        el.style.display = 'block';
        el.style.padding = '10px 14px';
        el.style.marginBottom = '1em';
        el.style.borderRadius = '4px';
        el.style.borderLeft = '4px solid ' + (kind === 'ok' ? '#4caf50' : (kind === 'error' ? '#e57373' : '#64b5f6'));
        el.style.background = (kind === 'ok'
            ? 'rgba(76, 175, 80, 0.12)'
            : (kind === 'error' ? 'rgba(229, 115, 115, 0.12)' : 'rgba(100, 181, 246, 0.12)'));

        var titleEl = document.createElement('div');
        titleEl.style.fontWeight = '600';
        titleEl.textContent = title;
        el.appendChild(titleEl);

        if (detail) {
            var detailEl = document.createElement('div');
            detailEl.style.marginTop = '4px';
            detailEl.style.opacity = '0.85';
            detailEl.style.whiteSpace = 'pre-wrap';
            detailEl.textContent = detail;
            el.appendChild(detailEl);
        }
    }

    /**
     * 弹窗提示。alert 模块在部分版本里是 ES module（需取 .default），
     * 这里做兼容，最差情况退回浏览器原生 alert，避免“点了没反应”。
     */
    function showAlert(title, text) {
        try {
            require(['alert'], function (alertModule) {
                var alertFn = alertModule && alertModule.default ? alertModule.default : alertModule;

                if (typeof alertFn === 'function') {
                    var result = alertFn({ title: title, text: text || '' });
                    if (result && typeof result.catch === 'function') {
                        result.catch(function () { });
                    }
                } else {
                    window.alert(title + (text ? '\n\n' + text : ''));
                }
            });
        } catch (e) {
            window.alert(title + (text ? '\n\n' + text : ''));
        }
    }

    function report(kind, title, detail) {
        showStatus(kind, title, detail);
        showAlert(title, detail);
    }

    /**
     * 从失败响应里尽力取出服务端返回的错误说明
     */
    function describeFailure(response) {
        if (!response) {
            return Promise.resolve('');
        }

        try {
            if (typeof response.json === 'function') {
                return response.json().then(function (data) {
                    if (!data) {
                        return '';
                    }
                    return data.Message || data.message || data.Detail || data.detail || '';
                }, function () {
                    return 'HTTP ' + (response.status || '');
                });
            }

            if (typeof response.status === 'number') {
                return Promise.resolve('HTTP ' + response.status);
            }
        } catch (e) {
        }

        return Promise.resolve('');
    }

    function loadPage(page, config) {
        config = config || {};

        page.querySelector('.txtServerUrl').value = config.ServerUrl || '';

        var tokenInput = page.querySelector('.txtToken');
        tokenInput.value = config.Token || '';
        tokenInput.setAttribute('data-has-token', config.HasToken ? '1' : '0');
        tokenInput.setAttribute('data-clear-token', '0');

        page.querySelector('.txtPriority').value = (config.Priority === 0 ? 0 : (config.Priority || 5));
        page.querySelector('.txtMergeWindowSeconds').value = config.MergeWindowSeconds || 10;
        page.querySelector('.txtTitleTemplate').value = config.TitleTemplate || '';
        page.querySelector('.txtMessageTemplate').value = config.MessageTemplate || '';

        page.querySelector('.chkEnableImages').checked = config.EnableImages !== false;
        page.querySelector('.chkDisableTlsValidation').checked = config.DisableTlsValidation === true;
        page.querySelector('.chkMergeLibraryNotifications').checked = config.MergeLibraryNotifications !== false;

        page.querySelector('.chkUseMarkdown').checked = config.UseMarkdown === true;
        page.querySelector('.chkUseBackdrop').checked = config.UseBackdropImage === true;
        page.querySelector('.txtExternalUrl').value = config.ExternalUrl || '';

        page.querySelector('.chkFilterEnabled').checked = config.FilterEnabled === true;
        page.querySelector('.txtIncludeItemTypes').value = config.IncludeItemTypes || '';
        page.querySelector('.txtIncludeTags').value = config.IncludeTags || '';
        page.querySelector('.txtMinRating').value = config.MinCommunityRating || 0;
        page.querySelector('.txtMinYear').value = config.MinYear || 0;
        page.querySelector('.txtExcludeKeywords').value = config.ExcludeKeywords || '';
        page.querySelector('.chkFollowOnly').checked = config.FollowOnly === true;

        renderLibraries(page, config.IncludeLibraryIds);

        loading.hide();
    }

    function collectOptions(page) {
        var tokenInput = page.querySelector('.txtToken');

        return {
            ServerUrl: page.querySelector('.txtServerUrl').value.trim(),
            Token: tokenInput.value.trim(),
            Priority: parseInt(page.querySelector('.txtPriority').value || '5', 10),
            EnableImages: page.querySelector('.chkEnableImages').checked,
            DisableTlsValidation: page.querySelector('.chkDisableTlsValidation').checked,
            TitleTemplate: page.querySelector('.txtTitleTemplate').value,
            MessageTemplate: page.querySelector('.txtMessageTemplate').value,
            MergeLibraryNotifications: page.querySelector('.chkMergeLibraryNotifications').checked,
            MergeWindowSeconds: parseInt(page.querySelector('.txtMergeWindowSeconds').value || '10', 10),

            UseMarkdown: page.querySelector('.chkUseMarkdown').checked,
            UseBackdropImage: page.querySelector('.chkUseBackdrop').checked,
            ExternalUrl: page.querySelector('.txtExternalUrl').value.trim(),

            FilterEnabled: page.querySelector('.chkFilterEnabled').checked,
            IncludeItemTypes: page.querySelector('.txtIncludeItemTypes').value.trim(),
            IncludeLibraryIds: collectLibraries(page),
            IncludeTags: page.querySelector('.txtIncludeTags').value.trim(),
            MinCommunityRating: parseInt(page.querySelector('.txtMinRating').value || '0', 10) || 0,
            MinYear: parseInt(page.querySelector('.txtMinYear').value || '0', 10) || 0,
            ExcludeKeywords: page.querySelector('.txtExcludeKeywords').value.trim(),
            FollowOnly: page.querySelector('.chkFollowOnly').checked
        };
    }

    /**
     * 已勾选的媒体库 Id，逗号拼接
     */
    function collectLibraries(page) {
        var boxes = page.querySelectorAll('.chkLibrary');
        var ids = [];

        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].checked) {
                ids.push(boxes[i].value);
            }
        }

        return ids.join(',');
    }

    /**
     * 渲染媒体库多选（勾选项用于过滤规则）
     */
    function renderLibraries(page, selectedIds) {
        var container = page.querySelector('.gotifyLibraryList');
        if (!container) {
            return;
        }

        container.textContent = '加载中…';

        ApiClient.getJSON(ApiClient.getUrl('Library/VirtualFolders')).then(function (folders) {
            container.innerHTML = '';

            if (!folders || !folders.length) {
                container.textContent = '（尚未创建媒体库）';
                return;
            }

            var selected = (selectedIds || '').split(',')
                .map(function (v) { return v.trim().toLowerCase(); })
                .filter(function (v) { return v.length > 0; });

            folders.forEach(function (folder) {
                var label = document.createElement('label');
                label.style.display = 'block';
                label.style.margin = '3px 0';
                label.style.fontSize = '13px';

                var checkbox = document.createElement('input');
                checkbox.type = 'checkbox';
                checkbox.className = 'chkLibrary';
                checkbox.value = folder.ItemId;
                checkbox.checked = selected.indexOf((folder.ItemId || '').toLowerCase()) >= 0;
                checkbox.style.marginRight = '6px';

                label.appendChild(checkbox);
                label.appendChild(document.createTextNode(folder.Name || '(未命名)'));
                container.appendChild(label);
            });
        }, function () {
            container.textContent = '媒体库列表读取失败';
        });
    }

    function validateOptions(options, page) {
        if (!options.ServerUrl) {
            return '请填写 Gotify 服务器地址，例如 https://push.example.com';
        }

        if (options.ServerUrl.indexOf('http://') !== 0 && options.ServerUrl.indexOf('https://') !== 0) {
            return '服务器地址需要以 http:// 或 https:// 开头。';
        }

        var tokenInput = page.querySelector('.txtToken');
        var hasSavedToken = tokenInput && tokenInput.getAttribute('data-has-token') === '1';
        var clearToken = tokenInput && tokenInput.getAttribute('data-clear-token') === '1';

        if (!options.Token && !hasSavedToken && !clearToken) {
            return '请填写 Gotify 应用 Token（在 Gotify 网页端「应用」页面创建应用后获得）。';
        }

        return null;
    }

    function onSubmit(e) {
        if (e) {
            e.preventDefault();
            e.stopPropagation();
        }

        var page = this;
        var options = collectOptions(page);
        var invalid = validateOptions(options, page);

        if (invalid) {
            report('error', '配置不完整', invalid);
            return false;
        }

        var tokenInput = page.querySelector('.txtToken');
        var clearToken = tokenInput.getAttribute('data-clear-token') === '1';

        var submitButton = page.querySelector('.btnSave');
        if (submitButton) {
            submitButton.disabled = true;
        }

        loading.show();

        ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('GotifyNotifier/Configuration'),
            data: JSON.stringify({ Options: options, ClearToken: clearToken }),
            contentType: 'application/json',
            dataType: 'json'
        }).then(function (result) {
            loading.hide();
            if (submitButton) {
                submitButton.disabled = false;
            }

            // 保存后用服务端返回的掩码回填，保持 Token 始终不是明文
            if (result) {
                var tokenInput = page.querySelector('.txtToken');
                tokenInput.value = result.Token || '';
                tokenInput.setAttribute('data-has-token', result.HasToken ? '1' : '0');
                tokenInput.setAttribute('data-clear-token', '0');
            }

            report('ok', '配置已保存', '服务器：' + options.ServerUrl + '\n保存后立即生效，无需重启 Emby。');
        }, function (response) {
            loading.hide();
            if (submitButton) {
                submitButton.disabled = false;
            }
            describeFailure(response).then(function (detail) {
                report('error', '保存失败', detail || '请查看 Emby 控制台 > 日志中的详细错误。');
            });
        });

        return false;
    }

    function onTestClick(e) {
        if (e) {
            e.preventDefault();
            e.stopPropagation();
        }

        var page = document.querySelector('.gotifyConfigurationForm');
        var options = collectOptions(page);
        var invalid = validateOptions(options, page);

        if (invalid) {
            report('error', '无法发送测试通知', invalid);
            return false;
        }

        var button = page.querySelector('.btnTest');
        if (button) {
            button.disabled = true;
        }

        showStatus('info', '正在按当前模板发送示意通知…', '目标：' + options.ServerUrl);
        loading.show();

        // 关键：把表单当前填写的内容直接发给后端测试，
        // 用户不必先点“保存”，也不会误用旧的空配置。
        ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('GotifyNotifier/Test'),
            data: JSON.stringify({ Options: options }),
            contentType: 'application/json',
            dataType: 'json'
        }).then(function (result) {
            loading.hide();
            if (button) {
                button.disabled = false;
            }

            if (result && result.Success) {
                report('ok', '示意通知已发送', (result.Message || '') + (result.Detail ? '\n' + result.Detail : ''));
            } else {
                var message = (result && result.Message) || '发送失败，请检查服务器地址与 Token。';
                report('error', '测试失败', message + (result && result.Detail ? '\n' + result.Detail : ''));
            }
        }, function (response) {
            loading.hide();
            if (button) {
                button.disabled = false;
            }

            describeFailure(response).then(function (detail) {
                report('error', '测试请求失败', detail || '服务器未返回有效结果，请查看 Emby 控制台 > 日志。');
            });
        });

        return false;
    }

    // ---------------------------------------------------------------------
    // 诊断面板
    // ---------------------------------------------------------------------

    var StatusText = {
        sent: '已发送',
        failed: '失败',
        queued: '排队中',
        skipped: '已跳过'
    };

    function statusColor(status, success) {
        if (status === 'sent') {
            return '#4caf50';
        }
        if (status === 'failed') {
            return '#e57373';
        }
        if (status === 'queued') {
            return '#64b5f6';
        }
        return success ? '#4caf50' : '#9e9e9e';
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text !== undefined && text !== null) {
            node.textContent = text;
        }
        return node;
    }

    function renderDiagRow(container, label, value, ok) {
        var row = el('div', 'gotifyDiagRow');
        row.style.display = 'flex';
        row.style.alignItems = 'baseline';
        row.style.padding = '5px 0';
        row.style.borderBottom = '1px solid rgba(128,128,128,.15)';

        var dot = el('span', null, ok ? '●' : '○');
        dot.style.color = ok ? '#4caf50' : '#e57373';
        dot.style.marginRight = '8px';
        dot.style.fontSize = '11px';
        row.appendChild(dot);

        var labelEl = el('span', null, label);
        labelEl.style.opacity = '0.65';
        labelEl.style.minWidth = '96px';
        labelEl.style.fontSize = '13px';
        row.appendChild(labelEl);

        var valueEl = el('span', null, value);
        valueEl.style.fontSize = '13px';
        row.appendChild(valueEl);

        container.appendChild(row);
    }

    function renderDiagnostics(page, diag) {
        var summary = page.querySelector('.gotifyDiagSummary');
        var usersEl = page.querySelector('.gotifyDiagUsers');
        var historyEl = page.querySelector('.gotifyDiagHistory');

        if (!summary) {
            return;
        }

        summary.innerHTML = '';
        usersEl.innerHTML = '';
        historyEl.innerHTML = '';

        if (!diag) {
            summary.textContent = '诊断信息读取失败';
            return;
        }

        renderDiagRow(summary, '插件通知器', diag.NotifierRegistered ? '已注册' : '未注册（请重启 Emby）', diag.NotifierRegistered);
        renderDiagRow(summary, '服务器', diag.ServerUrl || '未配置', !!diag.ServerUrl);
        renderDiagRow(summary, 'Token', diag.HasToken ? '已保存（掩码显示）' : '未配置', diag.HasToken);
        renderDiagRow(summary, '合并入库',
            diag.MergeLibraryNotifications ? ('开启，' + diag.MergeWindowSeconds + ' 秒窗口') : '关闭',
            true);
        renderDiagRow(summary, '插件版本', diag.PluginVersion || '-', true);

        // 用户通知配置
        var users = diag.Users || [];
        var title = el('div', null, '用户通知配置');
        title.style.fontWeight = '600';
        title.style.margin = '14px 0 6px';
        usersEl.appendChild(title);

        if (!users.length) {
            var none = el('div', null, '✗ 没有任何用户启用 Gotify 通知 —— 请到「设置 → 通知」添加一条通知并勾选「新媒体入库」。');
            none.style.color = '#e57373';
            none.style.fontSize = '13px';
            usersEl.appendChild(none);
        } else {
            users.forEach(function (u) {
                var line = el('div', null);
                line.style.fontSize = '13px';
                line.style.padding = '4px 0';

                var badge = el('span', null, u.Enabled ? '已启用' : '已关闭');
                badge.style.color = u.Enabled ? '#4caf50' : '#e57373';
                badge.style.marginRight = '8px';
                line.appendChild(badge);

                line.appendChild(el('span', null, u.Name + '　事件：' + ((u.Events && u.Events.length) ? u.Events.join(', ') : '（未勾选）')));
                usersEl.appendChild(line);
            });
        }

        // 建议
        if (diag.Hint) {
            var hint = el('div', null, '提示：' + diag.Hint);
            hint.style.marginTop = '10px';
            hint.style.padding = '8px 10px';
            hint.style.borderRadius = '4px';
            hint.style.background = 'rgba(100, 181, 246, 0.12)';
            hint.style.borderLeft = '3px solid #64b5f6';
            hint.style.fontSize = '13px';
            hint.style.whiteSpace = 'pre-wrap';
            usersEl.appendChild(hint);
        }

        // 最近记录
        var histTitle = el('div', null, '最近通知记录（最多 15 条）');
        histTitle.style.fontWeight = '600';
        histTitle.style.margin = '16px 0 6px';
        historyEl.appendChild(histTitle);

        var recent = diag.Recent || [];
        if (!recent.length) {
            var empty = el('div', null, '暂无记录。入库后若这里仍为空，说明 Emby 没有把事件交给插件（多数是通知未启用）。');
            empty.style.fontSize = '13px';
            empty.style.opacity = '0.75';
            historyEl.appendChild(empty);
            return;
        }

        recent.forEach(function (item) {
            var row = el('div', null);
            row.style.fontSize = '13px';
            row.style.padding = '5px 0';
            row.style.borderBottom = '1px solid rgba(128,128,128,.12)';

            var time = el('span', null, item.Time || '');
            time.style.opacity = '0.6';
            time.style.marginRight = '8px';
            row.appendChild(time);

            var badge = el('span', null, StatusText[item.Status] || (item.Success ? '已发送' : '失败'));
            badge.style.color = statusColor(item.Status, item.Success);
            badge.style.marginRight = '8px';
            badge.style.fontWeight = '600';
            row.appendChild(badge);

            var text = el('span', null, (item.Title || '') + (item.Detail ? '　' + item.Detail : ''));
            row.appendChild(text);

            historyEl.appendChild(row);
        });
    }

    function loadDiagnostics(page) {
        return ApiClient.getJSON(ApiClient.getUrl('GotifyNotifier/Diagnostics')).then(function (diag) {
            renderDiagnostics(page, diag);
        }, function () {
            renderDiagnostics(page, null);
        });
    }

    function onRefreshDiagClick(e) {
        if (e) {
            e.preventDefault();
        }

        loading.show();
        loadDiagnostics(document.querySelector('.gotifyConfigurationForm')).then(function () {
            loading.hide();
        }, function () {
            loading.hide();
        });
    }

    function onSimulateClick(e) {
        if (e) {
            e.preventDefault();
        }

        var page = document.querySelector('.gotifyConfigurationForm');
        var button = page.querySelector('.btnSimulateIngest');

        if (button) {
            button.disabled = true;
        }

        loading.show();

        ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl('GotifyNotifier/SimulateIngest'),
            data: JSON.stringify({}),
            contentType: 'application/json',
            dataType: 'json'
        }).then(function (result) {
            loading.hide();
            if (button) {
                button.disabled = false;
            }

            if (result && result.Success) {
                showStatus('info', '已触发模拟入库', (result.Message || '') + (result.Detail ? '\n' + result.Detail : ''));
            } else {
                report('error', '模拟入库未触发',
                    ((result && result.Message) || '未知原因') + (result && result.Detail ? '\n' + result.Detail : ''));
            }

            // 合并窗口内不会立即发送，稍后再刷新几次诊断
            setTimeout(function () { loadDiagnostics(page); }, 1500);
            setTimeout(function () { loadDiagnostics(page); }, 12000);
        }, function (response) {
            loading.hide();
            if (button) {
                button.disabled = false;
            }

            describeFailure(response).then(function (detail) {
                report('error', '模拟入库请求失败', detail || '请查看 Emby 控制台 > 日志。');
            });
        });

        return false;
    }

    function View(view, params) {
        BaseView.apply(this, arguments);

        var page = view;

        injectStyles();

        page.querySelector('.gotifyConfigurationForm').addEventListener('submit', onSubmit.bind(page));
        page.querySelector('.btnTest').addEventListener('click', onTestClick);
        page.querySelector('.btnSimulateIngest').addEventListener('click', onSimulateClick);
        page.querySelector('.btnRefreshDiag').addEventListener('click', onRefreshDiagClick);

        // Token 显示/隐藏
        page.querySelector('.btnToggleToken').addEventListener('click', function (e) {
            e.preventDefault();

            var input = page.querySelector('.txtToken');
            var showing = input.type === 'text';
            input.type = showing ? 'password' : 'text';
            this.textContent = showing ? '显示' : '隐藏';
        });

        // 掩码状态下聚焦时全选，直接输入即可替换，不会把字符插进掩码里
        page.querySelector('.txtToken').addEventListener('focus', function () {
            if (this.value && this.value.indexOf('*') >= 0) {
                this.select();
            }
        });

        // 用户重新输入内容时，取消"清除"标记
        page.querySelector('.txtToken').addEventListener('input', function () {
            if (this.value) {
                this.setAttribute('data-clear-token', '0');
            }
        });

        // 清除已保存的 Token（保存后生效）
        page.querySelector('.btnClearToken').addEventListener('click', function (e) {
            e.preventDefault();

            var input = page.querySelector('.txtToken');
            input.value = '';
            input.type = 'password';
            input.setAttribute('data-clear-token', '1');
            input.setAttribute('data-has-token', '0');

            var toggle = page.querySelector('.btnToggleToken');
            if (toggle) {
                toggle.textContent = '显示';
            }

            showStatus('info', '已清空 Token（尚未保存）', '点击“保存”后生效。清除后需要重新填写 Token 才能推送通知。');
        });

        renderVariables(page, 'title', '.txtTitleTemplate');
        renderVariables(page, 'message', '.txtMessageTemplate');

        var versionEl = page.querySelector('.pluginVersion');
        if (versionEl) {
            versionEl.textContent = 'Gotify Notifications v' + PluginVersion;
        }
    }

    Object.assign(View.prototype, BaseView.prototype);

    View.prototype.onResume = function (options) {

        BaseView.prototype.onResume.apply(this, arguments);

        loading.show();

        var page = this.view;

        ApiClient.getJSON(ApiClient.getUrl('GotifyNotifier/Configuration')).then(function (config) {
            loadPage(page, config);

            if (config && config.ServerUrl) {
                showStatus('info', '当前已保存的服务器：' + config.ServerUrl, '修改后请点“保存”才会生效；点“发送测试通知”会直接用页面上的内容测试。');
            }

            loadDiagnostics(page);
        }, function (response) {
            loading.hide();
            describeFailure(response).then(function (detail) {
                report('error', '读取配置失败', detail || '请查看 Emby 控制台 > 日志中的详细错误。');
            });
        });
    };

    return View;
});
