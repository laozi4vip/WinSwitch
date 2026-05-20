using System.IO.Pipes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WinSwitch.NativeHost;

/// <summary>
/// WinSwitch Native Messaging Host
/// 接收浏览器扩展通过 stdin 发送的窗口/标签页信息
/// 并通过命名管道转发给 WinSwitch 主程序
/// </summary>
class Program
{
    // 命名管道名称，与主程序约定
    private const string PipeName = "WinSwitch.BrowserBridge";

    static async Task Main(string[] args)
    {
        Log("WinSwitch Native Host started");

        try
        {
            // 连接到 WinSwitch 主程序的命名管道
            using var pipeClient = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            
            // 等待主程序启动管道（超时30秒）
            try
            {
                await pipeClient.ConnectAsync(30000);
                Log("Connected to WinSwitch main program");
            }
            catch (TimeoutException)
            {
                Log("WinSwitch main program not available, running in standalone mode");
                // 独立模式：直接输出到控制台（调试用）
                await RunStandaloneAsync();
                return;
            }

            // 主循环：从浏览器扩展 stdin 读取消息，转发到管道
            await RunBridgeAsync(pipeClient);
        }
        catch (Exception ex)
        {
            Log($"Fatal error: {ex.Message}");
        }
    }

    /// <summary>
    /// Native Messaging 协议：从 stdin 读取浏览器扩展消息
    /// 格式：前4字节为消息长度（little-endian），后面是 JSON
    /// </summary>
    static async Task RunBridgeAsync(NamedPipeClientStream pipe)
    {
        var stdin = Console.OpenStandardInput();

        while (true)
        {
            try
            {
                // 读取消息长度（4字节）
                var lengthBytes = new byte[4];
                var read = await stdin.ReadAsync(lengthBytes, 0, 4);
                if (read < 4) break;

                var length = BitConverter.ToUInt32(lengthBytes, 0);
                if (length == 0 || length > 10 * 1024 * 1024) // 最大10MB
                {
                    Log($"Invalid message length: {length}");
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

                // 转发到命名管道
                var pipeMessage = System.Text.Encoding.UTF8.GetBytes(json);
                // 管道协议：4字节长度 + JSON
                var lengthPrefix = BitConverter.GetBytes((uint)pipeMessage.Length);
                await pipe.WriteAsync(lengthPrefix, 0, 4);
                await pipe.WriteAsync(pipeMessage, 0, pipeMessage.Length);
                await pipe.FlushAsync();

                Log($"Forwarded message: {json.Length} bytes");
            }
            catch (Exception ex)
            {
                Log($"Error in bridge loop: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// 独立模式：从 stdin 读取并输出到控制台（调试用）
    /// </summary>
    static async Task RunStandaloneAsync()
    {
        var stdin = Console.OpenStandardInput();

        while (true)
        {
            var lengthBytes = new byte[4];
            var read = await stdin.ReadAsync(lengthBytes, 0, 4);
            if (read < 4) break;

            var length = BitConverter.ToUInt32(lengthBytes, 0);
            if (length == 0 || length > 10 * 1024 * 1024) continue;

            var msgBytes = new byte[length];
            var totalRead = 0;
            while (totalRead < length)
            {
                var r = await stdin.ReadAsync(msgBytes, totalRead, (int)length - totalRead);
                if (r == 0) break;
                totalRead += r;
            }

            var json = System.Text.Encoding.UTF8.GetString(msgBytes);
            Console.WriteLine(json);
        }
    }

    static void Log(string message)
    {
        // Native Messaging Host 的 stderr 不会被浏览器读取
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
    }
}
