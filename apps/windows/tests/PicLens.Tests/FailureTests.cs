using System.Security.AccessControl;
using System.Security.Principal;
using PicLens.Core;
using PicLens.Services;
namespace PicLens.Tests;

public sealed class FailureTests
{
    sealed class InlineProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
    static byte[] Hash(string path) => System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));

    [Fact]
    public async Task DeniedReadPermissionPreservesSourceAndContinuesConversion()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker);
        string denied = f.Image("denied.png"), good = f.Image("good.png");
        byte[] before = Hash(denied), goodBefore = Hash(good);
        var plans = FilePlans.Convert(new[] { denied, good }.Select(p => new LibraryEntry(p, Path.GetFileName(p), false, 0, 0)), OperationKind.Jpeg);
        var info = new FileInfo(denied); var originalAcl = info.GetAccessControl();
        var restrictedAcl = info.GetAccessControl();
        restrictedAcl.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny));
        try
        {
            info.SetAccessControl(restrictedAcl);
            Assert.Throws<UnauthorizedAccessException>(() => File.ReadAllBytes(denied));
            var result = await new FileOperations(profile, pool).ExecuteAsync(plans, null, default);
            Assert.Equal(1, result.Failed); Assert.Equal(1, result.Succeeded);
            Assert.False(File.Exists(plans[0].Target)); Assert.True(File.Exists(plans[1].Target));
            Assert.Empty(Directory.GetFiles(f.Root, ".piclens-*.tmp"));
            Assert.Equal(0, pool.ActiveCount);
        }
        finally
        {
            restrictedAcl.SetSecurityDescriptorSddlForm(originalAcl.GetSecurityDescriptorSddlForm(AccessControlSections.Access), AccessControlSections.Access);
            info.SetAccessControl(restrictedAcl);
        }
        Assert.Equal(before, Hash(denied)); Assert.Equal(goodBefore, Hash(good));
    }

    [Fact]
    public async Task PartialCancellationPreservesUnstartedSourcesAndCompletedResult()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker);
        var paths = Enumerable.Range(0, 3).Select(i => f.Image($"{i}.png")).ToArray();
        var hashes = paths.Select(Hash).ToArray();
        var plans = paths.Select(p => new FilePlan(p, Path.ChangeExtension(p, ".renamed.png"), OperationKind.Rename, FileStamp.Read(p))).ToArray();
        using var cancel = new CancellationTokenSource(); int reports = 0;
        var progress = new InlineProgress(_ => { if (++reports == 2) cancel.Cancel(); });
        var result = await new FileOperations(profile, pool).ExecuteAsync(plans, progress, cancel.Token);
        Assert.Equal(1, result.Succeeded); Assert.Equal(2, result.Canceled); Assert.Equal(0, result.Failed);
        Assert.False(File.Exists(paths[0])); Assert.Equal(hashes[0], Hash(plans[0].Target!));
        for (int i = 1; i < paths.Length; i++) { Assert.Equal(hashes[i], Hash(paths[i])); Assert.False(File.Exists(plans[i].Target)); }
        Assert.Empty(Directory.GetFiles(f.Root, ".piclens-*.tmp"));
    }

    [Fact]
    public async Task LockedSourceFailsWithoutStoppingIndependentRename()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker);
        string lockedPath = f.Image("locked.png"), good = f.Image("good.png");
        byte[] lockedHash = Hash(lockedPath), goodHash = Hash(good);
        var plans = new[] { lockedPath, good }.Select(p => new FilePlan(p, Path.ChangeExtension(p, ".renamed.png"), OperationKind.Rename, FileStamp.Read(p))).ToArray();
        using (var held = new FileStream(lockedPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var result = await new FileOperations(profile, pool).ExecuteAsync(plans, null, default);
            Assert.Equal(1, result.Failed); Assert.Equal(1, result.Succeeded);
        }
        Assert.Equal(lockedHash, Hash(lockedPath)); Assert.False(File.Exists(plans[0].Target));
        Assert.Equal(goodHash, Hash(plans[1].Target!)); Assert.False(File.Exists(good));
    }

    [Fact]
    public void ShellPermanentDeletionIsVetoedBeforeExecution()
    {
        var assembly = System.Reflection.Assembly.LoadFrom(Path.ChangeExtension(Fixture.Worker, ".dll"));
        var type = assembly.GetType("PicLens.Worker.RecycleSink", true)!;
        var sink = Activator.CreateInstance(type)!;
        var beforeDelete = type.GetMethod("PreDeleteItem")!;
        Assert.Equal(0, (int)beforeDelete.Invoke(sink, [0x80u, null])!);
        Assert.True((int)beforeDelete.Invoke(sink, [0u, null])! < 0);
        Assert.True((bool)type.GetProperty("Denied")!.GetValue(sink)!);
    }
    [Fact]
    public async Task MissingSourceDuringPlanningDoesNotAbortBatch()
    {
        using var f = new Fixture(); string source = f.Image("good.png");
        var plans = FilePlans.Convert([new(Path.Combine(f.Root, "missing.png"), "missing.png", false, 0, 0), new(source, "good.png", false, 0, 0)], OperationKind.Jpeg);
        var profile = new Profile(Path.Combine(f.Root, "profile")); await using var pool = new WorkerPool(profile, Fixture.Worker);
        var result = await new FileOperations(profile, pool).ExecuteAsync(plans, null, default);
        Assert.Equal(1, result.Failed); Assert.Equal(1, result.Succeeded); Assert.True(File.Exists(source));
        Assert.Equal(240, (new Settings { ThumbnailSize = int.MaxValue }).Normalize().ThumbnailSize);
    }
    [Theory]
    [InlineData(49, false)]
    [InlineData(50, true)]
    public void ConversionConfirmationBoundary(int count, bool expected) =>
        Assert.Equal(expected, FilePlans.RequiresConversionConfirmation(count));

    [Fact]
    public void LockedSettingsCannotBeReplaced()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        profile.Save(new Settings { LastFolderPath = "preserve" });
        string before = File.ReadAllText(profile.SettingsPath);
        using (var locked = new FileStream(profile.SettingsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            profile.Load();
            Assert.Throws<IOException>(() => profile.Save(new Settings()));
        }
        Assert.Equal(before, File.ReadAllText(profile.SettingsPath));
    }

    [Fact]
    public async Task CorruptWarmCacheIsRebuiltWithoutSourceChanges()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); await using var images = new ImageService(profile, pool);
        var path = f.Image("source.png"); byte[] before = File.ReadAllBytes(path);
        await images.LoadAsync(path, 32, default);
        string cache = Assert.Single(Directory.GetFiles(profile.Cache, "*.png"));
        File.WriteAllText(cache, "broken");
        Assert.Equal(32, (await images.LoadAsync(path, 32, default)).Width);
        Assert.Equal(before, File.ReadAllBytes(path)); Assert.Empty(Directory.GetFiles(profile.Temporary));
    }

    [Fact]
    public async Task EightWorkerLimitAndShutdownDrainQueuedJobs()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        var pool = new WorkerPool(profile, Fixture.Worker);
        var jobs = Enumerable.Range(0, 280).Select(_ => pool.RunAsync(["--stall-test"], default)).ToArray();
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (pool.ActiveCount < 8 && DateTime.UtcNow < deadline) await Task.Delay(10);
            Assert.Equal(8, pool.ActiveCount);
        }
        finally { await pool.DisposeAsync(); }
        foreach (var job in jobs) await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await job);
        Assert.Equal(0, pool.ActiveCount);
    }

    [Fact]
    public async Task PartialBatchContinuesAfterMissingSourceAndStemRace()
    {
        using var f = new Fixture(); var profile = new Profile(Path.Combine(f.Root, "profile"));
        await using var pool = new WorkerPool(profile, Fixture.Worker); var files = new FileOperations(profile, pool);
        string missing = f.File("gone.jpg"), source = f.File("a.jpg"), good = f.File("b.jpg");
        var plans = new List<FilePlan>
        {
            new(missing, Path.Combine(f.Root, "gone-new.jpg"), OperationKind.Rename, FileStamp.Read(missing)),
            new(source, Path.Combine(f.Root, "target-01.jpg"), OperationKind.Rename, FileStamp.Read(source), CheckStem: true),
            new(good, Path.Combine(f.Root, "done.jpg"), OperationKind.Rename, FileStamp.Read(good))
        };
        File.Delete(missing); f.File("target-01.webp", "keep");
        var result = await files.ExecuteAsync(plans, null, default);
        Assert.Equal(1, result.Failed); Assert.Equal(1, result.Skipped); Assert.Equal(1, result.Succeeded);
        Assert.True(File.Exists(source)); Assert.Equal("keep", File.ReadAllText(Path.Combine(f.Root, "target-01.webp")));
        Assert.False(File.Exists(good)); Assert.True(File.Exists(plans[2].Target));
    }
}
