using System.Runtime.InteropServices;

namespace Round.SDK.Helper.IO;

public class FileCheck
{
    public static bool IsFileLocked(string filePath)
    {
        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                // 如果能成功打开并关闭，说明文件没有被占用
                stream.Close();
                return false;
            }
        }
        catch (IOException ex)
        {
            // 检查特定的错误码
            var errorCode = Marshal.GetHRForException(ex) & 0xFFFF;

            // ERROR_SHARING_VIOLATION (32): 文件被另一个进程使用
            // ERROR_LOCK_VIOLATION (33): 文件的一部分被锁定
            if (errorCode == 32 || errorCode == 33) return true;

            // 其他IOException也可能是文件被占用
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}