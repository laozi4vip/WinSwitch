/**
 * WinSwitch Browser Extension — Background Service Worker
 * 事件驱动 + 防抖 + 增量同步，确保快捷键响应无延迟
 */

const NATIVE_HOST_NAME = 'com.winswitch.bridge';

// 端口连接
let port = null;
let reconnectTimer = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 5;
const RECONNECT_BASE_DELAY = 5000;
const RECONNECT_MAX_DELAY = 60000;

// 防抖定时器
let syncTimer = null;
const DEBOUNCE_DELAY = 300; // 300ms 防抖

// 上次全量同步的窗口数据（用于增量检测）
let lastSyncedWindows = [];

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

      if (errMsg.includes('not found') || errMsg.includes('not registered')) {
        console.warn('[WinSwitch] Native host not registered. Please run install.ps1 first.');
        reconnectAttempts = MAX_RECONNECT_ATTEMPTS;
      }

      scheduleReconnect();
    });

    console.log('[WinSwitch] Connected to native host');
    reconnectAttempts = 0;

    // 连接成功后全量同步一次
    fullSync();

  } catch (e) {
    console.error('[WinSwitch] Failed to connect native host:', e);
    scheduleReconnect();
  }
}

/**
 * 调度重连（指数退避）
 */
function scheduleReconnect() {
  if (reconnectTimer) return;
  if (reconnectAttempts >= MAX_RECONNECT_ATTEMPTS) {
    console.warn('[WinSwitch] Max reconnect attempts reached. Will retry on browser restart.');
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
 * 防抖调度：合并短时间内的多次事件为一次同步
 */
function scheduleDebouncedSync() {
  if (syncTimer) clearTimeout(syncTimer);
  syncTimer = setTimeout(() => {
    syncTimer = null;
    fullSync();
  }, DEBOUNCE_DELAY);
}

/**
 * 全量同步：收集所有窗口和标签页信息并发送
 */
async function fullSync() {
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
        active: t.active
        // 不传 favIconUrl，减少数据量
      }))
    }));

    sendToNative(data);
    lastSyncedWindows = data;
  } catch (e) {
    console.error('[WinSwitch] Error collecting window data:', e);
  }
}

/**
 * 发送数据到 Native Host
 */
function sendToNative(data) {
  if (!port) return; // 静默跳过，不刷日志
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

// ========== 事件监听（全部防抖）==========

// 标签页更新（标题/URL变化）— 防抖合并
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
  if (changeInfo.title || changeInfo.url || changeInfo.status === 'complete') {
    scheduleDebouncedSync();
  }
});

// 标签页创建/删除
chrome.tabs.onCreated.addListener(scheduleDebouncedSync);
chrome.tabs.onRemoved.addListener(scheduleDebouncedSync);

// 标签页激活切换
chrome.tabs.onActivated.addListener(scheduleDebouncedSync);

// 窗口创建/删除/焦点变化
chrome.windows.onCreated.addListener(scheduleDebouncedSync);
chrome.windows.onRemoved.addListener(scheduleDebouncedSync);
chrome.windows.onFocusChanged.addListener(scheduleDebouncedSync);

// 窗口位置/大小变化 — 低频轮询（2秒），只在变化时同步
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
        scheduleDebouncedSync();
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
  reconnectAttempts = 0;
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  connectNative();
});
