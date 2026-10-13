/* ==========================================
   VegaAgent 前端交互逻辑
   ========================================== */

const chatArea = document.getElementById('chatArea');
const userInput = document.getElementById('userInput');
const sendBtn = document.getElementById('sendBtn');
const clearBtn = document.getElementById('clearBtn');
let isProcessing = false;
let abortController = null;

if (typeof marked !== 'undefined') {
    marked.setOptions({ breaks: true, gfm: true });
}

function renderMarkdown(text) {
    if (typeof marked !== 'undefined') {
        return marked.parse(text || '');
    }
    return (text || '').replace(/\n/g, '<br>');
}

// ========== Vega 连接状态 & 系统监控 ==========

async function checkVegaStatus() {
    const indicator = document.getElementById('vegaIndicator');
    const connText = document.getElementById('vegaConnText');
    try {
        const resp = await fetch('/api/vega/ping');
        const data = await resp.json();
        if (data.status === 'ok' || data.status === 'success') {
            indicator.className = 'vega-indicator online';
            connText.textContent = 'Vega 在线';
        } else {
            indicator.className = 'vega-indicator offline';
            connText.textContent = data.message || 'Vega 离线';
        }
    } catch {
        indicator.className = 'vega-indicator offline';
        connText.textContent = '无法连接';
    }
}

async function updateSystemStats() {
    try {
        const resp = await fetch('/api/system/info');
        const data = await resp.json();
        if (data.status !== 'success') return;

        updateBar('cpu', data.cpu_percent);
        document.getElementById('cpuValue').textContent = data.cpu_percent + '%';

        const mem = data.memory;
        updateBar('mem', mem.percent);
        document.getElementById('memValue').textContent =
            `${mem.used_gb}/${mem.total_gb} GB`;
    } catch { }
}

function updateBar(type, percent) {
    const bar = document.getElementById(type + 'Bar');
    if (!bar) return;
    bar.style.width = percent + '%';
    bar.classList.remove('warn', 'danger');
    if (percent > 85) bar.classList.add('danger');
    else if (percent > 65) bar.classList.add('warn');
}

setInterval(checkVegaStatus, 10000);
setInterval(updateSystemStats, 5000);
checkVegaStatus();
updateSystemStats();

// ========== 工作项面板 ==========

async function loadWorkItems() {
    const list = document.getElementById('workitemsList');
    list.innerHTML = '<div class="loading-text">加载中...</div>';

    try {
        const resp = await fetch('/api/workitems/new');
        const data = await resp.json();

        if (data.status === 'success' && data.items && data.items.length > 0) {
            list.innerHTML = data.items.map(item => `
                <div class="workitem-card" data-id="${item.id}" data-url="${item.url || ''}">
                    <div class="workitem-header">
                        <span class="workitem-id">#${item.id}</span>
                        <span class="workitem-state">${item.state}</span>
                    </div>
                    <div class="workitem-title">${escapeHtml(item.title)}</div>
                    <div class="workitem-footer">
                        <span class="workitem-area">${escapeHtml(item.area_path)}</span>
                    </div>
                </div>
            `).join('');

            // 绑定点击事件
            document.querySelectorAll('.workitem-card').forEach(card => {
                card.addEventListener('click', (e) => {
                    const id = card.dataset.id;
                    const url = card.dataset.url;

                    // 如果有URL，在默认浏览器中打开
                    if (url) {
                        window.open(url, '_blank');
                    } else {
                        // 否则发送消息分析工作项
                        sendMessage(`请分析工作项 #${id}`);
                    }
                });
            });
        } else {
            list.innerHTML = '<div class="empty-text">暂无待处理工作项</div>';
        }
    } catch (e) {
        list.innerHTML = '<div class="error-text">加载失败: ' + escapeHtml(e.message) + '</div>';
    }
}

document.getElementById('refreshWorkItemsBtn').addEventListener('click', loadWorkItems);
loadWorkItems();
setInterval(loadWorkItems, 60000); // 每分钟刷新一次

// ========== 聊天功能 ==========

function removeWelcome() {
    const welcome = chatArea.querySelector('.welcome-msg');
    if (welcome) welcome.remove();
}

function scrollToBottom() {
    requestAnimationFrame(() => {
        chatArea.scrollTop = chatArea.scrollHeight;
    });
}

function addUserMessage(text) {
    removeWelcome();
    const div = document.createElement('div');
    div.className = 'message user';
    div.innerHTML = `
        <div class="message-avatar">&#128100;</div>
        <div class="message-body">
            <div class="message-content">${escapeHtml(text)}</div>
        </div>
    `;
    chatArea.appendChild(div);
    scrollToBottom();
}

// ========== 流式助手消息（渐进式渲染） ==========

function createAssistantContainer() {
    const div = document.createElement('div');
    div.className = 'message assistant';
    div.innerHTML = `
        <div class="message-avatar">&#9883;</div>
        <div class="message-body">
            <div class="step-status"><span class="status-dot"></span><span class="status-text">思考中…</span></div>
            <div class="tool-steps"></div>
        </div>
    `;
    chatArea.appendChild(div);
    scrollToBottom();
    return div;
}

function handleStreamStep(container, step) {
    const body = container.querySelector('.message-body');
    const statusEl = body.querySelector('.step-status');
    const toolSteps = body.querySelector('.tool-steps');

    switch (step.type) {
        case 'status':
            if (statusEl) statusEl.querySelector('.status-text').textContent = step.content;
            break;

        case 'content_delta': {
            let streamEl = body.querySelector('.streaming-content');
            if (!streamEl) {
                streamEl = document.createElement('div');
                streamEl.className = 'streaming-content';
                streamEl.textContent = '';
                toolSteps.before(streamEl);
            }
            streamEl.textContent += step.content;
            scrollToBottom();
            break;
        }

        case 'thinking': {
            const streamEl = body.querySelector('.streaming-content');
            if (streamEl) streamEl.remove();

            const block = document.createElement('div');
            block.className = 'thinking-block';
            block.innerHTML = `
                <div class="thinking-toggle" onclick="this.parentElement.classList.toggle('open')">
                    <svg class="thinking-chevron" width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 18l6-6-6-6"/></svg>
                    <span>Agent 思考</span>
                </div>
                <div class="thinking-content">${renderMarkdown(step.content)}</div>
            `;
            toolSteps.before(block);
            break;
        }

        case 'tool_call': {
            const toolDiv = document.createElement('div');
            toolDiv.className = 'tool-step executing';
            toolDiv.dataset.tool = step.tool;
            const argsPreview = step.content.length > 80 ? step.content.slice(0, 80) + '…' : step.content;
            toolDiv.innerHTML = `<span class="tool-icon spinner">&#9881;</span> <span class="tool-name">${escapeHtml(step.tool)}</span><span class="tool-args">(${escapeHtml(argsPreview)})</span>`;
            toolSteps.appendChild(toolDiv);
            scrollToBottom();
            break;
        }

        case 'tool_result': {
            const executing = toolSteps.querySelectorAll(`.tool-step.executing[data-tool="${step.tool}"]`);
            const target = executing.length > 0 ? executing[executing.length - 1] : null;
            if (target) {
                const isError = step.content && step.content.status === 'error';
                target.className = `tool-step ${isError ? 'failed' : 'done'}`;
                target.querySelector('.tool-icon').innerHTML = isError ? '&#10060;' : '&#10003;';
            }
            break;
        }

        case 'answer': {
            const streamEl = body.querySelector('.streaming-content');
            if (streamEl) streamEl.remove();

            if (statusEl) statusEl.remove();
            const answerDiv = document.createElement('div');
            answerDiv.className = 'message-content';
            answerDiv.innerHTML = renderMarkdown(step.content);
            body.appendChild(answerDiv);
            scrollToBottom();
            break;
        }

        case 'error': {
            const streamEl = body.querySelector('.streaming-content');
            if (streamEl) streamEl.remove();

            if (statusEl) statusEl.remove();
            const errDiv = document.createElement('div');
            errDiv.className = 'message-content';
            errDiv.innerHTML = `<span style="color:var(--red)">&#10060; ${escapeHtml(step.content)}</span>`;
            body.appendChild(errDiv);
            scrollToBottom();
            break;
        }

        case 'done': {
            const streamEl = body.querySelector('.streaming-content');
            if (streamEl) streamEl.remove();
            if (statusEl) statusEl.remove();
            break;
        }
    }
    scrollToBottom();
}

function escapeHtml(text) {
    const el = document.createElement('div');
    el.textContent = text;
    return el.innerHTML;
}

async function sendMessage(text) {
    if (isProcessing || !text.trim()) return;

    isProcessing = true;
    sendBtn.innerHTML = '<span class="btn-icon">&#9632;</span> 停止';
    sendBtn.classList.add('stop-btn');
    userInput.value = '';
    autoResize();

    addUserMessage(text);
    const container = createAssistantContainer();

    abortController = new AbortController();
    const timeoutId = setTimeout(() => abortController.abort(), 300000);

    try {
        const resp = await fetch('/api/chat/stream', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: text }),
            signal: abortController.signal,
        });

        const reader = resp.body.getReader();
        const decoder = new TextDecoder();
        let buffer = '';

        while (true) {
            const { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            const lines = buffer.split('\n');
            buffer = lines.pop();

            for (const line of lines) {
                if (!line.startsWith('data: ')) continue;
                const payload = line.slice(6).trim();
                if (!payload) continue;
                try {
                    const step = JSON.parse(payload);
                    handleStreamStep(container, step);
                } catch (_) { }
            }
        }

        // 处理 buffer 中剩余的数据
        if (buffer.startsWith('data: ')) {
            try {
                const step = JSON.parse(buffer.slice(6).trim());
                handleStreamStep(container, step);
            } catch (_) { }
        }

    } catch (err) {
        if (err.name === 'AbortError') {
            handleStreamStep(container, { type: 'error', content: '请求已取消或超时，请重试' });
        } else {
            handleStreamStep(container, { type: 'error', content: '网络请求失败: ' + err.message });
        }
    } finally {
        handleStreamStep(container, { type: 'done' });
        clearTimeout(timeoutId);
        isProcessing = false;
        sendBtn.innerHTML = '<span class="btn-icon">&#10148;</span> 发送';
        sendBtn.classList.remove('stop-btn');
        abortController = null;
        userInput.focus();
    }
}

async function clearChat() {
    try {
        await fetch('/api/clear', { method: 'POST' });
    } catch { }
    chatArea.innerHTML = `
        <div class="welcome-msg">
            <div class="welcome-icon">&#9883;</div>
            <h2>欢迎使用 VegaAgent</h2>
            <p>对话已清空，可以开始新的操作</p>
        </div>
    `;
}

// ========== 模型管理 ==========

const modelSelector = document.getElementById('modelSelector');
const modelSelectorBtn = document.getElementById('modelSelectorBtn');
const modelDropdownList = document.getElementById('modelDropdownList');
const activeModelName = document.getElementById('activeModelName');
const addModelBtn = document.getElementById('addModelBtn');
const modelModal = document.getElementById('modelModal');
const modalClose = document.getElementById('modalClose');
const modalCancelBtn = document.getElementById('modalCancelBtn');
const modalConfirmBtn = document.getElementById('modalConfirmBtn');

let currentModels = [];
let currentActiveId = '';

async function loadModels() {
    try {
        const resp = await fetch('/api/models');
        const data = await resp.json();
        currentModels = data.models;
        currentActiveId = data.active_id;
        renderModelList();
        const active = currentModels.find(m => m.id === currentActiveId);
        activeModelName.textContent = active ? active.name : '未选择';
    } catch { }
}

function renderModelList() {
    modelDropdownList.innerHTML = '';
    for (const m of currentModels) {
        const noKey = !m.has_key;
        const div = document.createElement('div');
        div.className = 'model-dropdown-item' + (m.id === currentActiveId ? ' active' : '') + (noKey ? ' no-key' : '');
        div.innerHTML = `
            <span class="model-item-name">${escapeHtml(m.name)}${noKey ? ' <span class="key-hint">需配置Key</span>' : ''}</span>
            <span class="model-item-tag">${escapeHtml(m.provider)}</span>
            <button class="model-delete-btn" data-id="${m.id}" title="删除">&times;</button>
        `;
        div.addEventListener('click', (e) => {
            if (e.target.classList.contains('model-delete-btn')) return;
            if (noKey) {
                closeDropdown();
                openEditModal(m);
                return;
            }
            switchModel(m.id);
        });
        const delBtn = div.querySelector('.model-delete-btn');
        if (delBtn) {
            delBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                deleteModel(m.id, m.name);
            });
        }
        modelDropdownList.appendChild(div);
    }
}

let editingModelId = null;

function openEditModal(m) {
    editingModelId = m.id;
    document.getElementById('mName').value = m.name;
    document.getElementById('mProvider').value = m.provider;
    document.getElementById('mModel').value = m.model;
    document.getElementById('mApiKey').value = '';
    document.getElementById('mBaseUrl').value = m.base_url || '';
    updateBaseUrlVisibility();
    document.getElementById('modalTitle').textContent = '配置 API Key';
    modalConfirmBtn.textContent = '保存';
    modelModal.classList.add('show');
    document.getElementById('mApiKey').focus();
}

async function switchModel(id) {
    if (id === currentActiveId) {
        closeDropdown();
        return;
    }
    try {
        await fetch('/api/models/switch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id })
        });
        await loadModels();
        closeDropdown();
    } catch { }
}

async function deleteModel(id, name) {
    if (!confirm(`确定删除模型「${name}」吗？`)) return;
    try {
        await fetch('/api/models/delete', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ id })
        });
        await loadModels();
    } catch { }
}

function closeDropdown() {
    modelSelector.classList.remove('open');
}

modelSelectorBtn.addEventListener('click', (e) => {
    e.stopPropagation();
    modelSelector.classList.toggle('open');
});

document.addEventListener('click', (e) => {
    if (!modelSelector.contains(e.target)) closeDropdown();
});

addModelBtn.addEventListener('click', () => {
    closeDropdown();
    openModal();
});

function openModal() {
    editingModelId = null;
    document.getElementById('mName').value = '';
    document.getElementById('mProvider').value = 'gemini';
    document.getElementById('mModel').value = '';
    document.getElementById('mApiKey').value = '';
    document.getElementById('mBaseUrl').value = '';
    updateBaseUrlVisibility();
    document.getElementById('modalTitle').textContent = '添加模型';
    modalConfirmBtn.textContent = '添加';
    modelModal.classList.add('show');
    document.getElementById('mName').focus();
}

function closeModal() {
    modelModal.classList.remove('show');
}

function updateBaseUrlVisibility() {
    const provider = document.getElementById('mProvider').value;
    const group = document.getElementById('baseUrlGroup');
    // Gemini 和智谱都不需要 Base URL
    group.style.display = (provider === 'zhipu' || provider === 'gemini') ? 'none' : 'flex';
}

document.getElementById('mProvider').addEventListener('change', updateBaseUrlVisibility);

modalClose.addEventListener('click', closeModal);
modalCancelBtn.addEventListener('click', closeModal);
modelModal.addEventListener('click', (e) => {
    if (e.target === modelModal) closeModal();
});

modalConfirmBtn.addEventListener('click', async () => {
    const name = document.getElementById('mName').value.trim();
    const provider = document.getElementById('mProvider').value;
    const model = document.getElementById('mModel').value.trim();
    const api_key = document.getElementById('mApiKey').value.trim();
    const base_url = document.getElementById('mBaseUrl').value.trim();

    if (!name || !model || !api_key) {
        alert('请填写名称、模型名称和 API Key');
        return;
    }

    modalConfirmBtn.disabled = true;
    const btnLabel = editingModelId ? '保存' : '添加';
    modalConfirmBtn.textContent = btnLabel + '中...';

    try {
        if (editingModelId) {
            const resp = await fetch('/api/models/update', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: editingModelId, name, provider, model, api_key, base_url })
            });
            const data = await resp.json();
            if (data.status === 'ok') {
                closeModal();
                await loadModels();
                await switchModel(editingModelId);
            } else {
                alert(data.message || '保存失败');
            }
        } else {
            const resp = await fetch('/api/models/add', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ name, provider, model, api_key, base_url })
            });
            const data = await resp.json();
            if (data.status === 'ok') {
                closeModal();
                await loadModels();
                await switchModel(data.model.id);
            } else {
                alert(data.message || '添加失败');
            }
        }
    } catch (err) {
        alert('请求失败: ' + err.message);
    } finally {
        modalConfirmBtn.disabled = false;
        modalConfirmBtn.textContent = btnLabel;
        editingModelId = null;
    }
});

loadModels();

// ========== 标签页切换 ==========

const tabBtns = document.querySelectorAll('.tab-btn');
const tabViews = {
    chat: document.getElementById('tabChat'),
    vega: document.getElementById('tabVega'),
};
const inputArea = document.getElementById('inputArea');

function switchTab(tabName) {
    tabBtns.forEach(b => b.classList.toggle('active', b.dataset.tab === tabName));
    Object.entries(tabViews).forEach(([k, v]) => v.classList.toggle('active', k === tabName));
    inputArea.style.display = tabName === 'chat' ? '' : 'none';
    if (tabName === 'vega') refreshDashboard();
}

tabBtns.forEach(b => b.addEventListener('click', () => switchTab(b.dataset.tab)));

// ========== Vega 仪表盘 ==========

const refreshDashBtn = document.getElementById('refreshDashboard');
if (refreshDashBtn) {
    refreshDashBtn.addEventListener('click', refreshDashboard);
}

async function refreshDashboard() {
    const dashConn = document.getElementById('dashConn');
    const dashInfo = document.getElementById('dashInfo');
    const dashDetail = document.getElementById('dashboardDetail');

    dashConn.textContent = '检测中...';
    dashInfo.textContent = '加载中...';
    dashDetail.textContent = '正在获取 Vega 状态...';

    try {
        const pingResp = await fetch('/api/vega/ping');
        const pingData = await pingResp.json();
        if (pingData.status === 'ok' || pingData.status === 'success') {
            dashConn.innerHTML = '<span style="color:var(--green)">&#9679;</span> Vega 在线';
        } else {
            dashConn.innerHTML = '<span style="color:var(--red)">&#9679;</span> ' + (pingData.message || 'Vega 离线');
            dashInfo.textContent = 'N/A';
            dashDetail.textContent = JSON.stringify(pingData, null, 2);
            return;
        }
    } catch (e) {
        dashConn.innerHTML = '<span style="color:var(--red)">&#9679;</span> 无法连接';
        dashInfo.textContent = 'N/A';
        dashDetail.textContent = '错误: ' + e.message;
        return;
    }

    try {
        const sysResp = await fetch('/api/system/info');
        const sysData = await sysResp.json();
        if (sysData.status === 'success') {
            dashInfo.textContent = `${sysData.hostname} | CPU: ${sysData.cpu_percent}% | 内存: ${sysData.memory.percent}%`;
        }
    } catch { }

    try {
        const statusResp = await fetch('/api/vega/ping');
        const statusData = await statusResp.json();
        dashDetail.textContent = JSON.stringify(statusData, null, 2);
    } catch (e) {
        dashDetail.textContent = '获取详情失败: ' + e.message;
    }
}

// ========== 事件绑定 ==========

sendBtn.addEventListener('click', () => {
    if (isProcessing && abortController) {
        abortController.abort();
    } else {
        sendMessage(userInput.value);
    }
});

userInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        sendMessage(userInput.value);
    }
});

function autoResize() {
    userInput.style.height = 'auto';
    userInput.style.height = Math.min(userInput.scrollHeight, 120) + 'px';
}
userInput.addEventListener('input', autoResize);

document.querySelectorAll('.action-btn').forEach(btn => {
    btn.addEventListener('click', () => {
        const action = btn.dataset.action;
        if (action === 'clear') {
            clearChat();
        } else if (action) {
            switchTab('chat');
            sendMessage(action);
        }
    });
});

// ========== 侧边栏调整大小 ==========

const sidebar = document.getElementById('sidebar');
const resizer = document.getElementById('sidebarResizer');

let isResizing = false;
let startX = 0;
let startWidth = 0;

resizer.addEventListener('mousedown', (e) => {
    isResizing = true;
    startX = e.clientX;
    startWidth = sidebar.offsetWidth;
    resizer.classList.add('resizing');
    document.body.style.cursor = 'col-resize';
    document.body.style.userSelect = 'none';
});

document.addEventListener('mousemove', (e) => {
    if (!isResizing) return;

    const delta = e.clientX - startX;
    const newWidth = startWidth + delta;

    const minWidth = 200;
    const maxWidth = 600;

    if (newWidth >= minWidth && newWidth <= maxWidth) {
        sidebar.style.width = newWidth + 'px';
    }
});

document.addEventListener('mouseup', () => {
    if (isResizing) {
        isResizing = false;
        resizer.classList.remove('resizing');
        document.body.style.cursor = '';
        document.body.style.userSelect = '';
    }
});

// ========== 设置 & 工作区路径配置 ==========

const settingsBtn = document.getElementById('settingsBtn');
const settingsModal = document.getElementById('settingsModal');
const settingsClose = document.getElementById('settingsClose');
const settingsCancelBtn = document.getElementById('settingsCancelBtn');
const settingsSaveBtn = document.getElementById('settingsSaveBtn');
const sWorkspaceRoot = document.getElementById('sWorkspaceRoot');

const workspaceWarning = document.getElementById('workspaceWarning');
const warningClose = document.getElementById('warningClose');
const warningSkipBtn = document.getElementById('warningSkipBtn');
const warningSaveBtn = document.getElementById('warningSaveBtn');
const wWorkspaceRoot = document.getElementById('wWorkspaceRoot');

function openSettings() {
    fetch('/api/config/workspace').then(r => r.json()).then(data => {
        sWorkspaceRoot.value = data.workspace_root || '';
        settingsModal.classList.add('show');
        sWorkspaceRoot.focus();
    });
}

function closeSettings() { settingsModal.classList.remove('show'); }

settingsBtn.addEventListener('click', openSettings);
settingsClose.addEventListener('click', closeSettings);
settingsCancelBtn.addEventListener('click', closeSettings);
settingsModal.addEventListener('click', e => { if (e.target === settingsModal) closeSettings(); });

settingsSaveBtn.addEventListener('click', async () => {
    settingsSaveBtn.disabled = true;
    settingsSaveBtn.textContent = '保存中...';
    try {
        const resp = await fetch('/api/config/workspace', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ workspace_root: sWorkspaceRoot.value.trim() })
        });
        const data = await resp.json();
        if (data.status === 'ok') {
            closeSettings();
        } else {
            alert(data.message || '保存失败');
        }
    } catch (err) {
        alert('请求失败: ' + err.message);
    } finally {
        settingsSaveBtn.disabled = false;
        settingsSaveBtn.textContent = '保存';
    }
});

function closeWarning() { workspaceWarning.classList.remove('show'); }
warningClose.addEventListener('click', closeWarning);
warningSkipBtn.addEventListener('click', closeWarning);
workspaceWarning.addEventListener('click', e => { if (e.target === workspaceWarning) closeWarning(); });

warningSaveBtn.addEventListener('click', async () => {
    const val = wWorkspaceRoot.value.trim();
    if (!val) { alert('请填写路径'); return; }
    warningSaveBtn.disabled = true;
    warningSaveBtn.textContent = '保存中...';
    try {
        const resp = await fetch('/api/config/workspace', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ workspace_root: val })
        });
        const data = await resp.json();
        if (data.status === 'ok') {
            closeWarning();
        } else {
            alert(data.message || '路径无效');
        }
    } catch (err) {
        alert('请求失败: ' + err.message);
    } finally {
        warningSaveBtn.disabled = false;
        warningSaveBtn.textContent = '保存';
    }
});

// 启动时检查工作区路径
fetch('/api/config/workspace').then(r => r.json()).then(data => {
    if (!data.workspace_root) {
        workspaceWarning.classList.add('show');
        wWorkspaceRoot.focus();
    }
});
