namespace PicLens.App;

public sealed record LaunchOptions(string? Folder = null, string? DataRoot = null, int? SmokeMs = null, string? Viewer = null,
    string? Metrics = null, string? Screenshot = null, bool Exercise = false, bool Dark = false, int Width = 1600, int Height = 1000, bool Components = false)
{
    public static LaunchOptions Parse(string[] args)
    {
        var result = new LaunchOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Value() { if (++i >= args.Length) throw new ArgumentException("參數缺少值。"); return args[i]; }
            result = args[i] switch
            {
                "--folder" => result with { Folder = Path.GetFullPath(Value()) },
                "--data-root" => result with { DataRoot = Value() },
                "--smoke-ms" => result with { SmokeMs = Positive(Value()) },
                "--viewer" => result with { Viewer = Path.GetFullPath(Value()) },
                "--metrics" => result with { Metrics = Path.GetFullPath(Value()) },
                "--screenshot" => result with { Screenshot = Path.GetFullPath(Value()) },
                "--exercise" => result with { Exercise = true },
                "--dark" => result with { Dark = true },
                "--components" => result with { Components = true },
                "--width" => result with { Width = Math.Max(800, Positive(Value())) },
                "--height" => result with { Height = Math.Max(600, Positive(Value())) },
                _ => throw new ArgumentException("未知參數：" + args[i])
            };
        }
        return result;
    }
    static int Positive(string value) => int.TryParse(value, out int number) && number > 0 ? number : throw new ArgumentException("參數必須是正整數。");
}
