using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Round.SDK.Logger;

public class ConsoleRedirector : IDisposable
{
    private static readonly ConcurrentDictionary<int, string> _threadNames = new();
    private readonly TextWriter _originalOutput;
    private readonly StreamWriter _writer;

    public ConsoleRedirector(string filePath, string timestampFormat = "HH:mm:ss.fff")
    {
        FileName = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        _originalOutput = Console.Out;
        RegisterThread(Thread.CurrentThread, "Main");

        _writer = new StreamWriter(filePath, false, new UTF8Encoding(false)) { AutoFlush = true };
        Console.OutputEncoding = Encoding.UTF8;
        Console.SetOut(new ThreadAwareTextWriter(_writer, _originalOutput, timestampFormat));
    }

    public static string? FileName { get; private set; } = string.Empty;

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _writer?.Dispose();
    }

    public static void RegisterThread(Thread thread, string name) => _threadNames.AddOrUpdate(thread.ManagedThreadId, name, (_, _) => name);
    public static void UnregisterThread(Thread thread) => _threadNames.TryRemove(thread.ManagedThreadId, out _);

    /// <summary>
    /// 获取精简且友好的调用位置信息
    /// </summary>
    private static string GetCallerLocation()
    {
        try
        {
            var stackTrace = new StackTrace(false);
            for (var i = 0; i < stackTrace.FrameCount; i++)
            {
                var method = stackTrace.GetFrame(i)?.GetMethod();
                if (method == null) continue;

                var declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                var typeName = declaringType.FullName ?? "";
                if (typeName.Contains(nameof(ConsoleRedirector)) || typeName.StartsWith("System.") || typeName.StartsWith("Microsoft."))
                    continue;

                // --- 处理异步状态机 (Async/Await) ---
                if (method.Name == "MoveNext" && typeof(IAsyncStateMachine).IsAssignableFrom(declaringType))
                {
                    // 尝试从状态机类型中找回原始方法
                    var realMethod = declaringType.DeclaringType?.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                        .FirstOrDefault(m => m.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType == declaringType);

                    if (realMethod != null)
                    {
                        return FormatName(realMethod.DeclaringType?.Name, realMethod.Name);
                    }

                    // 如果没找到 Attribute，则从类名提取 (例如 <TestSingleIpAsync>d__9)
                    var rawName = declaringType.Name;
                    if (rawName.StartsWith("<") && rawName.Contains(">"))
                    {
                        var cleanName = rawName.Substring(1, rawName.IndexOf('>') - 1);
                        return FormatName(declaringType.DeclaringType?.Name, cleanName);
                    }
                }

                // --- 处理普通方法 ---
                return FormatName(declaringType.Name, method.Name);
            }
        }
        catch { /* ignored */ }
        return "Unknown";
    }

    /// <summary>
    /// 格式化名称：去除泛型后缀、清理乱码
    /// </summary>
    private static string FormatName(string? className, string methodName)
    {
        if (string.IsNullOrEmpty(className)) className = "Global";

        // 清理泛型反引号 (ConfigEntity`1 -> ConfigEntity)
        var backtickIndex = className.IndexOf('`');
        if (backtickIndex > 0) className = className.Substring(0, backtickIndex);

        // 清理匿名方法的名称 (例如 <Main>b__0 -> Main)
        if (methodName.StartsWith("<") && methodName.Contains(">"))
        {
            methodName = methodName.Substring(1, methodName.IndexOf('>') - 1);
        }

        return $"{className}.{methodName}";
    }

    /// <summary>
    /// 判断是否为错误信息（用于决定是否显示红色）
    /// </summary>
    private static bool IsErrorMessage(string content)
    {
        if (string.IsNullOrEmpty(content)) return false;
        
        // 错误关键词列表（不区分大小写）
        var errorKeywords = new[]
        {
            "error",
            "exception",
            "fail",
            "failed",
            "fatal",
            "crash",
            "stack trace",
            "   at "
        };
        
        var lowerContent = content.ToLowerInvariant();
        return errorKeywords.Any(keyword => lowerContent.Contains(keyword));
    }

    private class ThreadAwareTextWriter : TextWriter
    {
        private readonly object _lock = new();
        private readonly TextWriter _fileWriter;
        private readonly TextWriter _consoleOutput;
        private readonly string _tsFormat;
        private readonly StringBuilder _lineBuffer = new();
        private readonly object _consoleLock = new(); // 用于保护控制台颜色状态

        public ThreadAwareTextWriter(TextWriter fileWriter, TextWriter consoleOutput, string tsFormat)
        {
            _fileWriter = fileWriter;
            _consoleOutput = consoleOutput;
            _tsFormat = tsFormat;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            lock (_lock)
            {
                if (value == '\n') FlushBuffer();
                else if (value != '\r') _lineBuffer.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            foreach (var c in value) Write(c);
        }

        public override void WriteLine(string? value)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (value.EndsWith("\n") || value.EndsWith("\r"))
            {
                WriteLine(value.Substring(0, value.Length - 1));
                return;
            }
            Write(value);
            Write('\n');
        }

        private void FlushBuffer()
        {
            var content = _lineBuffer.ToString();
            _lineBuffer.Clear();

            if (string.IsNullOrWhiteSpace(content) && content.Length == 0)
            {
                _consoleOutput.WriteLine();
                _fileWriter.WriteLine();
                return;
            }

            var timestamp = DateTime.Now.ToString(_tsFormat);
            var caller = GetCallerLocation();
            var threadId = Thread.CurrentThread.ManagedThreadId;
            
            // 获取自定义线程名（如果有）
            _threadNames.TryGetValue(threadId, out var threadName);
            var threadDisplay = threadName != null ? $"{threadId}({threadName})" : threadId.ToString();

            // 格式化日志行（不含颜色，用于文件）
            var formattedLine = $"[{timestamp}][#{threadDisplay}][{caller}] {content}";
            
            // 判断是否为错误信息
            var isError = IsErrorMessage(content);
            
            // 带颜色的控制台输出（使用 Console API）
            lock (_consoleLock)
            {
                try
                {
                    // 如果是错误信息，整体使用红色
                    if (isError)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        _consoleOutput.WriteLine(formattedLine);
                        Console.ResetColor();
                    }
                    else
                    {
                        // 正常信息：分段显示不同颜色
                        // 时间戳 - 灰色
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        _consoleOutput.Write($"[{timestamp}]");
                        
                        // 线程ID - 青色
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        _consoleOutput.Write($"[#{threadDisplay}]");
                        
                        // 调用位置 - 黄色
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        _consoleOutput.Write($"[{caller}]");
                        
                        // 消息内容 - 白色
                        Console.ForegroundColor = ConsoleColor.White;
                        _consoleOutput.Write($" {content}");
                        
                        // 换行
                        _consoleOutput.WriteLine();
                        
                        // 重置颜色
                        Console.ResetColor();
                    }
                }
                catch
                {
                    // 如果颜色设置失败（例如输出被重定向），回退到无颜色输出
                    _consoleOutput.WriteLine(formattedLine);
                }
            }
            
            // 写入文件（无颜色）
            _fileWriter.WriteLine(formattedLine);
        }

        public override void Flush()
        {
            lock (_lock) { if (_lineBuffer.Length > 0) FlushBuffer(); }
            _fileWriter.Flush();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Flush();
            base.Dispose(disposing);
        }
    }
}