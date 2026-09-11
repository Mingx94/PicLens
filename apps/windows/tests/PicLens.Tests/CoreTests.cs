using System.Text.Json;
using PicLens.Core;
using PicLens.Services;
namespace PicLens.Tests;

public sealed class CoreTests
{
    [Fact]
    public void SharedSettingsFixtures()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixture.Repo, "test-data/windows-native-cases.json")));
        foreach (var row in json.RootElement.GetProperty("settings").EnumerateArray()) {
            var settings = JsonSerializer.Deserialize<Settings>(row.GetProperty("input").GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            Assert.Equal(row.GetProperty("expectedSize").GetInt32(), settings.Normalize().ThumbnailSize);
        }
    }
    [Fact]
    public void SharedNaturalSortFixtures()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixture.Repo, "test-data/windows-native-cases.json")));
        foreach (var row in json.RootElement.GetProperty("naturalSort").EnumerateArray())
        {
            var input = row.GetProperty("input").EnumerateArray().Select(v => v.GetString()!).ToArray();
            var expected = row.GetProperty("expected").EnumerateArray().Select(v => v.GetString()).ToArray();
            Assert.Equal(expected, input.OrderBy(v => v, NaturalComparer.Instance));
        }
    }
    [Fact]
    public void SelectionPreservesOrderAndStableRange()
    {
        var s = new Selection(); string[] visible = ["a", "b", "c", "d"];
        s.Select("c", visible, false, false); s.Select("a", visible, true, false);
        Assert.Equal(["c", "a"], s.Ordered);
        s.Select("d", visible, false, true); Assert.Equal(visible, s.Ordered);
        s.Select("b", visible, true, false); Assert.Equal(["a", "c", "d"], s.Ordered);
        s.Clear(); Assert.Empty(s.Ordered); Assert.Null(s.Anchor);
    }
    [Fact]
    public void FoldersStayFirstInDescendingMode()
    {
        var files = new[] { new LibraryEntry("a.jpg", "a.jpg", false, 0, 1), new LibraryEntry("z", "z", true, 0, 0) };
        Assert.True(LibraryRules.Sort(files, new() { Direction = 1 }, true)[0].IsFolder);
    }
    [Fact]
    public void HistoryTruncatesForwardBranch()
    {
        var history = new FolderHistory(); history.Visit("a"); history.Visit("b"); history.Visit("c");
        Assert.Equal("b", history.Back()); history.Visit("d"); Assert.False(history.CanForward); Assert.Equal("b", history.Back());
    }
    [Fact]
    public void ZoomKeepsPointerAnchor()
    {
        var next = Zoom.Fit.At(60, 30, 1);
        Assert.Equal(60, (60 - next.X) / next.Scale, 6); Assert.Equal(30, (30 - next.Y) / next.Scale, 6);
        Assert.Equal(8, new Zoom(8, 0, 0).At(0, 0, 1).Scale);
    }
    [Theory]
    [InlineData("CON")]
    [InlineData("com1")]
    [InlineData("bad/name")]
    [InlineData("trailing.")]
    [InlineData("  ")]
    public void RejectsUnsafeNames(string name) => Assert.Throws<ArgumentException>(() => FilePlans.ValidateRename(@"C:\pics\a.png", name));
    [Fact]
    public void SettingsRoundTripAndRecovery()
    {
        using var f = new Fixture(); var profile = new Profile(f.Root);
        File.WriteAllText(profile.SettingsPath, """{"lastFolderPath":"D:\\Pictures","sort":{"key":1,"direction":99},"thumbnailSize":170,"windowWidth":600,"windowHeight":400,"unknown":true}""");
        var settings = profile.Load(); Assert.Equal(180, settings.ThumbnailSize); Assert.Equal(0, settings.Sort.Direction);
        Assert.Equal(800u, settings.WindowWidth); profile.Save(settings);
        Assert.Equal(settings, profile.Load());
        File.WriteAllText(profile.SettingsPath, "{bad");
        Assert.Equal(160, profile.Load().ThumbnailSize);
        Assert.Single(Directory.GetFiles(f.Root, "*.corrupt.*"));
        profile.Save(new()); Assert.Equal(160, profile.Load().ThumbnailSize);
    }
    [Fact]
    public void RenamePlansAvoidOtherExtensions()
    {
        using var f = new Fixture(); string source = f.File("one.jpg"), target = f.File("base.png"), existing = f.File("base-01.webp");
        var plan = FilePlans.DropRename([source], target, [source, target, existing]);
        Assert.EndsWith("base-02.jpg", plan[0].Target); Assert.True(plan[0].CheckStem);
    }
    [Fact]
    public void ConversionAndCleanupRules()
    {
        using var f = new Fixture();
        var files = new[] { f.File("a.png"), f.File("a.jpg"), f.File("a.webp"), f.File("b.gif") };
        var entries = files.Select(p => new LibraryEntry(p, Path.GetFileName(p), false, 0, 1, p.EndsWith(".gif"))).ToList();
        Assert.Equal(3, FilePlans.Convert(entries, OperationKind.Webp).Count(x => x.Skip != null));
        var cleanup = FilePlans.Cleanup(entries); Assert.Single(cleanup); Assert.Equal(files[0], cleanup[0].Source);
    }
}
