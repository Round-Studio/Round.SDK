using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices; // 必须引用，用于识别异步状态机
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

    private class ThreadAwareTextWriter : TextWriter
    {
        private readonly object _lock = new();
        private readonly TextWriter _fileWriter;
        private readonly TextWriter _consoleOutput;
        private readonly string _tsFormat;
        private readonly StringBuilder _lineBuffer = new();

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
            
            // 使用对齐格式化，让输出更像生产环境的日志
            var formattedLine = $"[{timestamp}][#{Thread.CurrentThread.ManagedThreadId}][{caller}] {content}";
            // 时间 线程ID 日志地址

            _consoleOutput.WriteLine(formattedLine);
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