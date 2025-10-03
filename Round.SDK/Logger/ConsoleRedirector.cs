using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Round.SDK.Logger;

public class ConsoleRedirector : IDisposable
{
    public static string FileName { get; private set; }
    
    private StreamWriter _writer;
    private TextWriter _originalOutput;
    private readonly string _timestampFormat;
    private static readonly ConcurrentDictionary<int, string> _threadNames = new ConcurrentDictionary<int, string>();

    /// <summary>
    /// 初始化控制台重定向器
    /// </summary>
    /// <param name="filePath">日志文件路径</param>
    /// <param name="timestampFormat">时间戳格式(默认: HH:mm:ss.fff)</param>
    public ConsoleRedirector(string filePath, string timestampFormat = "HH:mm:ss.fff")
    {
        FileName = filePath;
        if (!Directory.Exists(Path.GetDirectoryName(filePath)))
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
        
        _originalOutput = Console.Out;
        _timestampFormat = timestampFormat;
        
        // 注册主线程名称
        RegisterThread(Thread.CurrentThread, "Main");
        
        // 创建 UTF-8 编码的 StreamWriter（不带 BOM）
        _writer = new StreamWriter(filePath, false, new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        
        // 设置控制台输出编码为 UTF-8
        Console.OutputEncoding = Encoding.UTF8;
        
        Console.SetOut(new ThreadAwareTextWriter(_writer, _originalOutput, _timestampFormat));
    }

    /// <summary>
    /// 注册线程名称
    /// </summary>
    public static void RegisterThread(Thread thread, string name)
    {
        _threadNames.AddOrUpdate(thread.ManagedThreadId, name, (id, oldName) => name);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOutput);
        _writer?.Dispose();
        // 恢复原来的控制台编码（可选）
        // Console.OutputEncoding = Encoding.Default;
    }

    /// <summary>
    /// 获取调用位置信息
    /// </summary>
    private static string GetCallerLocation()
    {
        try
        {
            // 创建堆栈跟踪，跳过足够的帧数来找到真正的调用者
            StackTrace stackTrace = new StackTrace(fNeedFileInfo: true);

            // 遍历堆栈帧，找到第一个不在 ThreadAwareTextWriter 中的调用者
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                StackFrame frame = stackTrace.GetFrame(i);
                MethodBase method = frame?.GetMethod();

                if (method == null) continue;

                // 跳过 ThreadAwareTextWriter 和 System.IO 相关的内部方法
                Type declaringType = method.DeclaringType;
                if (declaringType == null) continue;

                string typeName = declaringType.FullName;
                if (typeName != null &&
                    (typeName.Contains(nameof(ThreadAwareTextWriter)) ||
                     typeName.Contains(nameof(ConsoleRedirector)) ||
                     typeName.StartsWith("System.IO") ||
                     typeName.StartsWith("System.Console")))
                {
                    continue;
                }

                // 找到真正的调用者
                string className = declaringType.Name;
                string methodName = method.Name;
                string fileName = frame.GetFileName();
                int lineNumber = frame.GetFileLineNumber();

                if (!string.IsNullOrEmpty(fileName) && lineNumber > 0)
                {
                    string shortFileName = Path.GetFileName(fileName);
                    // return $"{className}.{methodName}({shortFileName}:{lineNumber})";

                    return $"{shortFileName}:{lineNumber}";
                }
                else
                {
                    return $"{className}.{methodName}";
                }
            }
        }
        catch
        {
            // 忽略异常，返回默认值
        }

        return "Unknown";
    }

    /// <summary>
    /// 自定义TextWriter，为每行添加线程名、时间戳和调用位置
    /// </summary>
    private class ThreadAwareTextWriter : TextWriter
    {
        private readonly TextWriter _innerWriter;
        private readonly TextWriter _orgwriter;
        private readonly string _timestampFormat;
        private readonly StringBuilder _lineBuffer = new StringBuilder();

        public ThreadAwareTextWriter(TextWriter innerWriter, TextWriter orgwriter, string timestampFormat)
        {
            _innerWriter = innerWriter;
            _orgwriter = orgwriter;
            _timestampFormat = timestampFormat;
        }

        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value)
        {
            if (value == '\n')
            {
                FlushLine();
            }
            else if (value != '\r')
            {
                _lineBuffer.Append(value);
            }
        }

        public override void Write(string value)
        {
            if (value == null) return;
            
            foreach (char c in value)
            {
                Write(c);
            }
        }

        public override void WriteLine(string value)
        {
            Write(value);
            Write('\n');
        }

        /// <summary>
        /// 刷新当前行并添加格式
        /// </summary>
        private void FlushLine()
        {
            if (_lineBuffer.Length > 0)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                string threadName = GetThreadName(threadId);
                var timestamp = DateTime.Now.ToString(_timestampFormat);
                string callerLocation = GetCallerLocation();
                
                string formattedLine = $"[{timestamp}][TID {threadId}][{threadName}][{callerLocation}]: {_lineBuffer}";
                
                Console.SetOut(_orgwriter);
                Console.WriteLine(formattedLine);
                Console.SetOut(this);
                
                _innerWriter.Write(formattedLine);
                _lineBuffer.Clear();
            }
            
            _innerWriter.Write('\n');
        }

        public override void Flush()
        {
            if (_lineBuffer.Length > 0)
            {
                FlushLine();
            }
            _innerWriter.Flush();
        }

        private string GetThreadName(int threadId)
        {
            if (_threadNames.TryGetValue(threadId, out var name))
            {
                return name;
            }
            
            // 自动为未命名的线程生成名称
            string newName = $"Thread-{threadId}";
            _threadNames.TryAdd(threadId, newName);
            return newName;
        }
    }
}