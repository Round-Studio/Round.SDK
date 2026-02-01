namespace Round.SDK.Helper;

public class UnixTimeConverter
{
    /// <summary>
    ///     将Unix时间戳（秒）转换为DateTime
    /// </summary>
    /// <param name="unixTimeStamp">Unix时间戳（秒）</param>
    /// <returns>对应的DateTime</returns>
    public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        // Unix时间戳的起点：1970年1月1日 00:00:00 UTC
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        // 将秒转换为TimeSpan并添加到起点
        dateTime = dateTime.AddSeconds(unixTimeStamp);

        // 转换为本地时间（可选）
        return dateTime.ToLocalTime();
    }

    /// <summary>
    ///     将DateTime转换为Unix时间戳（秒）
    /// </summary>
    public static long DateTimeToUnixTimeStamp(DateTime dateTime)
    {
        // 转换为UTC时间
        var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        var diff = dateTime.ToUniversalTime() - origin;

        return (long)diff.TotalSeconds;
    }
}