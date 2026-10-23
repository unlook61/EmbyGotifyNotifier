define(['globalize', 'pluginManager', 'emby-input', 'emby-textarea'], function (globalize, pluginManager) {
    'use strict';

    /**
     * Gotify 通知的「条目编辑器」。
     * Emby 在 设置 → 通知 中点击服务时，会加载本模块并把表单渲染到弹窗里。
     *
     * 注意：本模块必须提供 loadTemplate / setFormValues / setObjectValues 三个方法，
     * 否则 Emby 的 notificationeditor 会抛错，表现为"点击没有任何反应"。
     */
    function EntryEditor(options) {
        this.options = options;
    }

    /**
     * 渲染自定义字段（服务器地址/Token/模板都在插件配置页统一设置，这里只放备注名）
     */
    EntryEditor.prototype.loadTemplate = function (context) {
        var url = pluginManager.getConfigurationResourceUrl('gotifynotifyentryeditortemplate');

        return require(['text!' + url]).then(function (html) {
            context.innerHTML = globalize.translateDocument(html);
        });
    };

    /**
     * 保存时把表单值写回 entry
     */
    EntryEditor.prototype.setObjectValues = function (context, entry) {
        var nameInput = context.querySelector('.txtFriendlyName');
        if (nameInput) {
            entry.FriendlyName = nameInput.value;
        }

        entry.Options = entry.Options || {};

        var priorityInput = context.querySelector('.txtEntryPriority');
        setOrRemove(entry.Options, 'Priority', priorityInput ? priorityInput.value : '');

        var titleInput = context.querySelector('.txtEntryTitleTemplate');
        setOrRemove(entry.Options, 'TitleTemplate', titleInput ? titleInput.value : '');

        var messageInput = context.querySelector('.txtEntryMessageTemplate');
        setOrRemove(entry.Options, 'MessageTemplate', messageInput ? messageInput.value : '');
    };

    /**
     * 打开时用 entry 的值填充表单
     */
    EntryEditor.prototype.setFormValues = function (context, entry) {
        var nameInput = context.querySelector('.txtFriendlyName');
        if (nameInput) {
            nameInput.value = entry.FriendlyName || '';
        }

        var options = entry.Options || {};

        var priorityInput = context.querySelector('.txtEntryPriority');
        if (priorityInput) {
            priorityInput.value = options.Priority || '';
        }

        var titleInput = context.querySelector('.txtEntryTitleTemplate');
        if (titleInput) {
            titleInput.value = options.TitleTemplate || '';
        }

        var messageInput = context.querySelector('.txtEntryMessageTemplate');
        if (messageInput) {
            messageInput.value = options.MessageTemplate || '';
        }
    };

    /**
     * 有值就写入，空值则删除（表示沿用全局设置）
     */
    function setOrRemove(options, key, value) {
        var text = (value || '').trim();

        if (text) {
            options[key] = text;
        } else {
            delete options[key];
        }
    }

    EntryEditor.prototype.destroy = function () {
        this.options = null;
    };

    return EntryEditor;
});
