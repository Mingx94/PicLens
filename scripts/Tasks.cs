using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

try
{
    var root = FindRepoRoot();
    if (args.Length == 0 || args[0] is "-h" or "--help")
    {
        PrintUsage();
        return 0;
    }

    var command = args[0].ToLowerInvariant();
    var rest = args.Skip(1).ToArray();
    return command switch
    {
        "test" => Test(root, rest),
        "run" => BuildAndRun(root, rest),
        "release" => Release(root, rest),
        "ui-test" => UiTest(root, rest),
        _ => Fail($"Unknown command: {args[0]}")
    };
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or PlatformNotSupportedException or IOException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int Test(string root, string[] args)
{
    if (HasHelp(args))
    {
        Console.WriteLine("Usage: dotnet run --file scripts/Tasks.cs -- test");
        return 0;
    }

    RequireNoArgs(args);

    var nugetConfig = RequiredFile(root, "NuGet.Config");
    var projects = new[]
    {
        RequiredFile(root, "tests", "PicLens.Core.Tests", "PicLens.Core.Tests.csproj"),
        RequiredFile(root, "tests", "PicLens.Infrastructure.Tests", "PicLens.Infrastructure.Tests.csproj"),
        RequiredFile(root, "tests", "PicLens.ViewModels.Tests", "PicLens.ViewModels.Tests.csproj")
    };

    foreach (var project in projects)
    {
        Console.WriteLine($"==> Restoring test project: {project}");
        RunOrThrow("dotnet", ["restore", project, "--configfile", nugetConfig], root);
    }

    foreach (var project in projects)
    {
        Console.WriteLine($"==> Running tests: {project}");
        RunOrThrow("dotnet", ["test", project, "--no-restore"], root);
    }

    return 0;
}

static int UiTest(string root, string[] args)
{
    var configuration = "Debug";
    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-h":
            case "--help":
                Console.WriteLine("Usage: dotnet run --file scripts/Tasks.cs -- ui-test [--configuration Debug|Release]");
                return 0;
            case "--configuration":
                configuration = ReadValue(args, ref i, args[i]);
                break;
            default:
                throw new ArgumentException($"Unknown option: {args[i]}");
        }
    }

    RequireConfiguration(configuration);

    var nugetConfig = RequiredFile(root, "NuGet.Config");
    var project = RequiredFile(root, "tests", "PicLens.Ui.Tests", "PicLens.Ui.Tests.csproj");

    Console.WriteLine("==> Restoring Avalonia headless UI test project");
    RunOrThrow("dotnet", ["restore", project, "--configfile", nugetConfig], root);

    Console.WriteLine("==> Running Avalonia headless UI smoke tests");
    RunOrThrow("dotnet", ["test", project, "--no-restore", "-c", configuration], root);
    return 0;
}

static int BuildAndRun(string root, string[] args)
{
    var project = "PicLens/PicLens.csproj";
    var skipRun = false;
    var detach = false;
    var extraArgs = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "-h":
            case "--help":
                Console.WriteLine("""
                Usage:
                  dotnet run --file scripts/Tasks.cs -- run [project] [--skip-run] [--detach] [MSBuild args]
                """);
                return 0;
            case "--skip-run":
            case "-SkipRun":
                skipRun = true;
                break;
            case "--detach":
            case "-Detach":
                detach = true;
                break;
            default:
                if (project == "PicLens/PicLens.csproj" && (arg.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || !LooksLikeOption(arg)))
                {
                    project = arg;
                }
                else
                {
                    extraArgs.Add(arg);
                }

                break;
        }
    }

    var projectPath = FullPath(root, project);
    if (!File.Exists(projectPath))
    {
        throw new InvalidOperationException($"Project file not found: {projectPath}");
    }

    var projectDir = Path.GetDirectoryName(projectPath)!;
    var projectName = Path.GetFileNameWithoutExtension(projectPath);
    var platform = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "ARM64" : "x64";
    var configuration = "Debug";

    foreach (var arg in extraArgs)
    {
        if (TryReadProperty(arg, "Platform", out var value))
        {
            platform = value;
        }
        else if (TryReadProperty(arg, "Configuration", out value))
        {
            configuration = value;
        }
    }

    Console.WriteLine($"==> Building {projectName} ({configuration}|{platform})");
    RunOrThrow("dotnet",
    [
        "build",
        projectPath,
        "/restore",
        $"-p:Platform={platform}",
        $"-p:Configuration={configuration}",
        "-p:NuGetAudit=false",
        .. extraArgs
    ], root);

    Console.WriteLine("BUILD SUCCEEDED");

    if (skipRun)
    {
        Console.WriteLine("==> Skipping run (--skip-run)");
        return 0;
    }

    var binDir = Path.Combine(projectDir, "bin", platform, configuration);
    if (!Directory.Exists(binDir))
    {
        throw new InvalidOperationException($"Build completed but output folder was not found: {binDir}");
    }

    var exe = Directory.EnumerateFiles(binDir, "*", SearchOption.AllDirectories)
        .Where(path => Path.GetFileName(path) == projectName || Path.GetFileName(path) == $"{projectName}.exe")
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault();

    if (exe is not null)
    {
        Console.WriteLine($"==> Launching {exe}");
        return detach ? StartDetached(exe, [], Path.GetDirectoryName(exe)!) : Run(exe, [], Path.GetDirectoryName(exe)!);
    }

    var dll = Directory.EnumerateFiles(binDir, $"{projectName}.dll", SearchOption.AllDirectories)
        .OrderByDescending(File.GetLastWriteTimeUtc)
        .FirstOrDefault();

    if (dll is null)
    {
        throw new InvalidOperationException($"Build completed but {projectName} executable was not found under {binDir}.");
    }

    Console.WriteLine($"==> Launching {dll}");
    return detach ? StartDetached("dotnet", [dll], Path.GetDirectoryName(dll)!) : Run("dotnet", [dll], Path.GetDirectoryName(dll)!);
}

static int Release(string root, string[] args)
{
    var target = DefaultReleaseTarget();
    var configuration = "Release";
    var runtime = target.RuntimeIdentifier;
    var platform = target.Platform;
    var noClean = false;

    for (var i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "-h":
            case "--help":
                Console.WriteLine("""
                Usage:
                  dotnet run --file scripts/Tasks.cs -- release [options]

                Options:
                  --configuration Debug|Release
                  --runtime win-x64|win-arm64|win-x86|linux-x64
                  --platform x64|ARM64|x86
                  --no-clean
                """);
                return 0;
            case "--configuration":
                configuration = ReadValue(args, ref i, args[i]);
                break;
            case "--runtime":
                runtime = ReadValue(args, ref i, args[i]);
                break;
            case "--platform":
                platform = ReadValue(args, ref i, args[i]);
                break;
            case "--no-clean":
                noClean = true;
                break;
            default:
                throw new ArgumentException($"Unknown option: {args[i]}");
        }
    }

    RequireConfiguration(configuration);
    RequireRuntimePlatform(runtime, platform);

    var project = RequiredFile(root, "PicLens", "PicLens.csproj");
    var nugetConfig = RequiredFile(root, "NuGet.Config");
    var outputRoot = SafePath(root, "artifacts", "portable");
    var outputDir = SafePath(root, "artifacts", "portable", $"PicLens-{runtime}");
    var exePath = Path.Combine(outputDir, OperatingSystem.IsWindows() || runtime.StartsWith("win-", StringComparison.Ordinal) ? "PicLens.exe" : "PicLens");
    var readyToRun = configuration == "Debug" ? "false" : "true";

    Directory.CreateDirectory(outputRoot);
    if (!noClean && Directory.Exists(outputDir))
    {
        Directory.Delete(outputDir, recursive: true);
    }

    Console.WriteLine($"==> Restoring app for {runtime}");
    RunOrThrow("dotnet",
    [
        "restore",
        project,
        "--configfile",
        nugetConfig,
        "-r",
        runtime,
        $"/p:Configuration={configuration}",
        $"/p:Platform={platform}",
        $"/p:PublishReadyToRun={readyToRun}",
        "/p:SelfContained=false"
    ], root);

    Console.WriteLine("==> Publishing framework-dependent output");
    RunOrThrow("dotnet",
    [
        "publish",
        project,
        "--no-restore",
        "-c",
        configuration,
        "-r",
        runtime,
        "--self-contained",
        "false",
        $"/p:Platform={platform}",
        "/p:PublishSelfContained=false",
        "/p:PublishSingleFile=false",
        $"/p:PublishReadyToRun={readyToRun}",
        "/p:PublishTrimmed=false",
        "/p:SelfContained=false",
        "/p:DebugType=None",
        "/p:DebugSymbols=false",
        "-o",
        outputDir
    ], root);

    if (!File.Exists(exePath))
    {
        throw new InvalidOperationException($"Publish completed but PicLens executable was not found at: {exePath}");
    }

    var files = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories).ToArray();
    var totalBytes = files.Sum(path => new FileInfo(path).Length);

    Console.WriteLine();
    Console.WriteLine("Release output ready:");
    Console.WriteLine($"  Folder: {outputDir}");
    Console.WriteLine($"  Exe:    {exePath}");
    Console.WriteLine($"  Files:  {files.Length}");
    Console.WriteLine($"  Bytes:  {totalBytes}");
    Console.WriteLine($"  SHA256: {Sha256(exePath)}");
    return 0;
}

static (string RuntimeIdentifier, string Platform) DefaultReleaseTarget()
{
    if (OperatingSystem.IsWindows())
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => ("win-arm64", "ARM64"),
            Architecture.X86 => ("win-x86", "x86"),
            _ => ("win-x64", "x64")
        };
    }

    if (OperatingSystem.IsLinux())
    {
        return ("linux-x64", "x64");
    }

    throw new PlatformNotSupportedException("Portable release supports Windows and Linux hosts.");
}

static void RequireRuntimePlatform(string runtime, string platform)
{
    var expected = runtime switch
    {
        "win-x64" => "x64",
        "win-arm64" => "ARM64",
        "win-x86" => "x86",
        "linux-x64" => "x64",
        _ => throw new ArgumentException($"Unsupported runtime: {runtime}")
    };

    if (!string.Equals(platform, expected, StringComparison.Ordinal))
    {
        throw new ArgumentException($"Runtime {runtime} requires --platform {expected}.");
    }
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "PicLens", "PicLens.csproj")) &&
            Directory.Exists(Path.Combine(dir.FullName, "scripts")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Run this from the PicLens repository.");
}

static string RequiredFile(string root, params string[] parts)
{
    var path = SafePath(root, parts);
    if (!File.Exists(path))
    {
        throw new InvalidOperationException($"Required file not found: {path}");
    }

    return path;
}

static string SafePath(string root, params string[] parts)
{
    var path = FullPath(root, Path.Combine(parts));
    var resolvedRoot = Path.GetFullPath(root);
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    if (!path.Equals(resolvedRoot, comparison) && !path.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, comparison))
    {
        throw new InvalidOperationException($"Refusing to operate outside workspace root: {path}");
    }

    return path;
}

static string FullPath(string root, string path)
{
    return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
}

static int Run(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    using var process = Process.Start(StartInfo(fileName, arguments, workingDirectory))
        ?? throw new InvalidOperationException($"Failed to start: {fileName}");
    process.WaitForExit();
    return process.ExitCode;
}

static void RunOrThrow(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    var exitCode = Run(fileName, arguments, workingDirectory);
    if (exitCode != 0)
    {
        throw new InvalidOperationException($"{fileName} failed with exit code {exitCode}.");
    }
}

static int StartDetached(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    _ = Process.Start(StartInfo(fileName, arguments, workingDirectory))
        ?? throw new InvalidOperationException($"Failed to start: {fileName}");
    return 0;
}

static ProcessStartInfo StartInfo(string fileName, IReadOnlyList<string> arguments, string workingDirectory)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = fileName,
        WorkingDirectory = workingDirectory,
        RedirectStandardOutput = false,
        RedirectStandardError = false,
        UseShellExecute = false
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    return startInfo;
}

static bool HasHelp(string[] args)
{
    return args is ["-h" or "--help"];
}

static void RequireNoArgs(string[] args)
{
    if (args.Length > 0)
    {
        throw new ArgumentException($"Unknown option: {args[0]}");
    }
}

static void RequireConfiguration(string configuration)
{
    if (configuration is not ("Debug" or "Release"))
    {
        throw new ArgumentException("--configuration must be Debug or Release.");
    }
}

static string ReadValue(string[] args, ref int index, string option)
{
    if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
    {
        throw new ArgumentException($"{option} requires a value.");
    }

    index++;
    return args[index];
}

static bool LooksLikeOption(string value)
{
    return value.StartsWith('-') || (value.StartsWith('/') && !value.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase));
}

static bool TryReadProperty(string arg, string name, out string value)
{
    var prefixes = new[] { $"-p:{name}=", $"/p:{name}=" };
    foreach (var prefix in prefixes)
    {
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            value = arg[prefix.Length..];
            return true;
        }
    }

    value = "";
    return false;
}

static string Sha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
    Usage:
      dotnet run --file scripts/Tasks.cs -- <command> [options]

    Commands:
      test       Restore and run Core, Infrastructure, and ViewModel tests
      run        Build and optionally launch the app
      release    Publish the portable release folder
      ui-test    Run Avalonia Headless UI smoke tests
    """);
}
