using System.IO.Pipes;
using Newtonsoft.Json;

namespace WinSwitch.NativeHost;

/// <summary>
/// WinSwitch Native Messaging Host
/// 接收浏览器扩展通过 stdin 发送的窗口/标签页信息
/// 并通过命名管道转发给 WinSwitch 主程序
/// 
/// 重要：Chrome Native Messaging 协议要求：
/// 1. 收到消息后必须回复（否则浏览器会断开连接）
/// 2. 每个扩展实例会启动独立的 host 进程
/// 3. stdout 用于发送回复，stdin 用于接收消息
/// </summary>
class Program
{
    private const string PipeName = "WinSwitch.BrowserBridge";

    static async Task Main(string[] args)
    {
        Log("WinSwitch Native Host started");

        try
        {
            // 连接到 WinSwitch 主程序的命名管道
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            
            bool pipeConnected = false;
            try
            {
                await pipeClient.ConnectAsync(5000); // 5秒超时
                pipeConnected = true;
                Log("Connected to WinSwitch main program");
            }
            catch (TimeoutException)
            {
                Log("WinSwitch main program not available, forwarding disabled");
            }

            // 主循环：从浏览器扩展 stdin 读取消息，转发到管道，回复浏览器
            await RunBridgeAsync(pipeClient, pipeConnected);
        }
        catch (Exception ex)
        {
            Log($"Fatal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Native Messaging 协议主循环
    /// </summary>
    static async Task RunBridgeAsync(NamedPipeClientStream pipe, bool pipeConnected)
    {
        var stdin = Console.OpenStandardInput();
        var stdout = Console.OpenStandardOutput();

        while (true)
        {
            try
            {
                // 读取消息长度（4字节，little-endian uint32）
                var lengthBytes = new byte[4];
                var read = await stdin.ReadAsync(lengthBytes, 0, 4);
                if (read < 4) break;

                var length = BitConverter.ToUInt32(lengthBytes, 0);
                if (length == 0 || length > 10 * 1024 * 1024)
                {
                    Log($"Invalid message length: {length}");
                    // 必须回复，否则浏览器断连
                    SendMessage(stdout, "{\"type\":\"error\",\"message\":\"invalid length\"}");
                    continue;
                }

                // 读取消息内容
                var msgBytes = new byte[length];
                var totalRead = 0;
                while (totalRead < length)
                {
                    var r = await stdin.ReadAsync(msgBytes, totalRead, (int)length - totalRead);
                    if (r == 0) break;
                    totalRead += r;
                }

                var json = System.Text.Encoding.UTF8.GetString(msgBytes);
                Log($"Received: {json.Length} bytes");

                // 转发到命名管道（如果已连接）
                if (pipeConnected && pipe.IsConnected)
                {
                    try
                    {
                        var pipeMessage = System.Text.Encoding.UTF8.GetBytes(json);
                        var lengthPrefix = BitConverter.GetBytes((uint)pipeMessage.Length);
                        await pipe.WriteAsync(lengthPrefix, 0, 4);
                        await pipe.WriteAsync(pipeMessage, 0, pipeMessage.Length);
                        await pipe.FlushAsync();
                        Log($"Forwarded to pipe: {json.Length} bytes");
                    }
                    catch (Exception ex)
                    {
                        Log($"Pipe write error: {ex.Message}");
                        pipeConnected = false;
                    }
                }

                // *** 关键：必须回复浏览器，否则浏览器会断开连接 ***
                SendMessage(stdout, "{\"type\":\"ack\",\"status\":\"ok\"}");
            }
            catch (Exception ex)
            {
                Log($"Error in bridge loop: {ex.Message}");
                try
                {
                    SendMessage(stdout, $"{{\"type\":\"error\",\"message\":\"{ex.Message.Replace("\"", "\\\"")}\"}}");
                }
                catch { break; }
            }
        }
    }

    /// <summary>
    /// 向浏览器发送消息（Native Messaging 协议格式：4字节长度 + JSON）
    /// </summary>
    static void SendMessage(System.IO.Stream stdout, string json)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        var lengthPrefix = BitConverter.GetBytes((uint)bytes.Length);
        stdout.Write(lengthPrefix, 0, 4);
        stdout.Write(bytes, 0, bytes.Length);
        stdout.Flush();
    }

    static void Log(string message)
    {
        // stderr 不会被浏览器读取，可安全写日志
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
