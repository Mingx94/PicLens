using PicLens.Imaging;
using PicLens.Worker;
try
{
    if (args.Length == 1 && args[0] == "--stall-test") { Thread.Sleep(Timeout.Infinite); return 0; }
    if (args.Length < 2) throw new ArgumentException("無效的 helper 指令。");
    if (args[0] == "trash")
    {
        Recycle.File(args[1]);
    }
    else if (args[0] == "decode" && args.Length >= 4)
    {
        using var image = Codec.Decode(args[1], int.Parse(args[3]));
        Codec.WritePixels(image, args[2]);
        if (args.Length == 5) Codec.Encode(image, args[4], "png");
    }
    else if (args[0] == "convert" && args.Length == 4)
    {
        using var image = Codec.Decode(args[1], 0); Codec.Encode(image, args[2], args[3]);
    }
    else throw new ArgumentException("無效的 helper 指令。");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
