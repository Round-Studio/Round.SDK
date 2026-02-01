namespace Round.SDK.Helper;

public class SizeHelper
{
    public static string FormatBytes(double bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        var counter = 0;
        var number = bytes;

        while (number >= 1024 && counter < suffixes.Length - 1)
        {
            number /= 1024;
            counter++;
        }

        return $"{number:F1} {suffixes[counter]}";
    }
}