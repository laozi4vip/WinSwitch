/**
 * WinSwitch Browser Extension — Background Service Worker
 * 负责收集浏览器窗口/标签页信息并通过 Native Messaging 发送给 WinSwitch 本地程序
 */

const NATIVE_HOST_NAME = 'com.winswitch.bridge';

// 端口连接
let port = null;
let reconnectTimer = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 5;      // 最多自动重连5次
const RECONNECT_BASE_DELAY = 5000;     // 基础延迟5秒
const RECONNECT_MAX_DELAY = 60000;     // 最大延迟60秒

/**
 * 连接 Native Messaging Host
 */
function connectNative() {
  try {
    port = chrome.runtime.connectNative(NATIVE_HOST_NAME);

    port.onMessage.addListener((msg) => {
      console.log('[WinSwitch] Native host response:', msg);
    });

    port.onDisconnect.addListener(() => {
      const errMsg = chrome.runtime.lastError?.message || 'Unknown error';
      console.warn('[WinSwitch] Native host disconnected:', errMsg);
      port = null;

      // 如果是"host not found"错误，不无限重试
      if (errMsg.includes('not found') || errMsg.includes('not registered')) {
        console.warn('[WinSwitch] Native host not registered. Please run install.ps1 first.');
        reconnectAttempts = MAX_RECONNECT_ATTEMPTS; // 阻止自动重连
      }

      scheduleReconnect();
    });

    console.log('[WinSwitch] Connected to native host');
    reconnectAttempts = 0; // 连接成功，重置计数
    // 连接成功后立即发送一次数据
    collectAndSend();

  } catch (e) {
    console.error('[WinSwitch] Failed to connect native host:', e);
    scheduleReconnect();
  }
}

/**
 * 调度重连（指数退避，有上限）
 */
function scheduleReconnect() {
  if (reconnectTimer) return; // 已有定时器
  if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS) {
    console.warn('[WinSwitch] Max reconnect attempts reached. Will retry when browser restarts or extension reloads.');
    return;
  }

  reconnectAttempts++;
  const delay = Math.min(RECONNECT_BASE_DELAY * Math.pow(2, reconnectAttempts - 1), RECONNECT_MAX_DELAY);
  console.log(`[WinSwitch] Reconnecting in ${delay / 1000}s (attempt ${reconnectAttempts}/${MAX_RECONNECT_ATTEMPTS})`);

  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    connectNative();
  }, delay);
}

/**
 * 收集所有浏览器窗口和标签页信息
 */
async function collectAndSend() {
  try {
    const windows = await chrome.windows.getAll({ populate: true });
    const data = windows.map(w => ({
      browserWindowId: w.id,
      focused: w.focused,
      state: w.state,
      left: w.left,
      top: w.top,
      width: w.width,
      height: w.height,
      tabs: (w.tabs || []).map(t => ({
        tabId: t.id,
        title: t.title || '',
        url: t.url || '',
        active: t.active,
        favIconUrl: t.favIconUrl || ''
      }))
    }));
    sendToNative(data);
  } catch (e) {
    console.error('[WinSwitch] Error collecting window data:', e);
  }
}

/**
 * 发送数据到 Native Host
 */
function sendToNative(data) {
  if (!port) {
    // 静默跳过，不打印大量警告
    return;
  }
  try {
    port.postMessage({
      type: 'browserInfo',
      windows: data,
      timestamp: Date.now()
    });
  } catch (e) {
    console.error('[WinSwitch] Error sending to native host:', e);
    port = null;
    scheduleReconnect();
  }
}

// ========== 事件监听 ==========

// 标签页更新（标题/URL变化）
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.title || changeInfo.url || changeInfo.status === 'complete') {
    collectAndSend();
  }
});

// 标签页创建/删除
chrome.tabs.onCreated.addListener(collectAndSend);
chrome.tabs.onRemoved.addListener(collectAndSend);

// 标签页激活切换
chrome.tabs.onActivated.addListener(collectAndSend);

// 窗口创建/删除/焦点变化
chrome.windows.onCreated.addListener(collectAndSend);
chrome.windows.onRemoved.addListener(collectAndSend);
chrome.windows.onFocusChanged.addListener(collectAndSend);

// 窗口位置/大小变化（定时轮询，chrome.windows 没有 onBoundsChanged）
let boundsPollTimer = null;
let lastBounds = {};

function startBoundsPolling() {
  boundsPollTimer = setInterval(async () => {
    try {
      const windows = await chrome.windows.getAll();
      let changed = false;
      for (const w of windows) {
        const key = `${w.id}`;
        const bounds = `${w.left},${w.top},${w.width},${w.height}`;
        if (lastBounds[key] !== bounds) {
          lastBounds[key] = bounds;
          changed = true;
        }
      }
      if (changed) {
        collectAndSend();
      }
    } catch (e) {
      // ignore
    }
  }, 2000);
}

// ========== 启动 ==========

connectNative();
startBoundsPolling();

// 扩展安装/更新时触发
chrome.runtime.onInstalled.addListener(() => {
  console.log('[WinSwitch] Extension installed/updated');
  // 重置重连计数，尝试连接
  reconnectAttempts = 0;
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  connectNative();
  collectAndSend();
});
