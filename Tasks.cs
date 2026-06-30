using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

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
        "installer" => Installer(root, rest),
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
        Console.WriteLine("Usage: dotnet run Tasks.cs -- test");
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
                Console.WriteLine("Usage: dotnet run Tasks.cs -- ui-test [--configuration Debug|Release]");
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
                  dotnet run Tasks.cs -- run [project] [--skip-run] [--detach] [MSBuild args]
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
                  dotnet run Tasks.cs -- release [options]

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

static int Installer(string root, string[] args)
{
    var options = InstallerOptions.Parse(args);
    if (options.ShowHelp)
    {
        InstallerOptions.PrintUsage();
        return 0;
    }

    options = options with { Version = ResolvePackageVersion(root, options.Version) };
    Console.WriteLine($"Platform: {RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})");
    Console.WriteLine($"Package version: {options.Version}");

    if (OperatingSystem.IsWindows())
    {
        return BuildWindowsInstaller(root, options);
    }

    if (OperatingSystem.IsLinux())
    {
        return BuildFedoraInstaller(root, options);
    }

    throw new PlatformNotSupportedException("No installer exists for this platform. Supported hosts: Windows and Fedora Linux.");
}

static string ResolvePackageVersion(string root, string versionOverride)
{
    var version = string.IsNullOrWhiteSpace(versionOverride)
        ? File.ReadAllText(RequiredFile(root, "VERSION")).Trim()
        : versionOverride.Trim();

    if (string.IsNullOrWhiteSpace(version))
    {
        throw new ArgumentException("Package version must not be empty.");
    }

    return version;
}

static int BuildWindowsInstaller(string root, InstallerOptions options)
{
    if (!Regex.IsMatch(options.Version, @"^\d+\.\d+\.\d+(\.\d+)?$"))
    {
        throw new ArgumentException($"Installer version must be three or four dot-separated numbers: {options.Version}");
    }

    Console.WriteLine("Installer: Windows setup (.exe)");

    var iscc = FindInnoSetupCompiler(options.InnoSetupCompiler);
    if (iscc is null)
    {
        Console.Error.WriteLine("Inno Setup 6 not found. Install: winget install --id JRSoftware.InnoSetup -e");
        return 1;
    }

    var tasks = RequiredFile(root, "Tasks.cs");
    var installerScript = RequiredFile(root, "installer", "PicLens.iss");
    var runtime = "win-x64";
    var platform = "x64";
    var portableDir = SafePath(root, "artifacts", "portable", $"PicLens-{runtime}");
    var installerRoot = SafePath(root, "artifacts", "installer");
    var stageRoot = SafePath(root, "artifacts", "installer", "setup-stage");
    var stageDir = SafePath(root, "artifacts", "installer", "setup-stage", $"PicLens-{runtime}");
    var outputBaseName = $"PicLens-{runtime}-Setup";
    var setupPath = SafePath(root, "artifacts", "installer", $"{outputBaseName}.exe");

    var releaseArgs = new List<string>
    {
        "run", "--file", tasks, "--",
        "release",
        "--configuration", "Release",
        "--runtime", runtime,
        "--platform", platform
    };
    if (options.NoClean) releaseArgs.Add("--no-clean");

    var isccArgs = new[]
    {
        $"/DAppVersion={options.Version}",
        $"/DRootDir={root}",
        $"/DPayloadDir={stageDir}",
        $"/DOutputDir={installerRoot}",
        $"/DOutputBaseFilename={outputBaseName}",
        installerScript
    };

    if (options.DryRun)
    {
        PrintCommand("dotnet", releaseArgs);
        PrintCommand(iscc, isccArgs);
        return 0;
    }

    Console.WriteLine("==> Building portable release");
    RunOrThrow("dotnet", releaseArgs, root);

    if (!File.Exists(Path.Combine(portableDir, "PicLens.exe")))
    {
        throw new InvalidOperationException($"Portable release was not found: {portableDir}");
    }

    Directory.CreateDirectory(installerRoot);

    if (!options.NoClean)
    {
        DeleteIfExists(stageRoot);
        DeleteIfExists(setupPath);
    }

    CopyDirectory(portableDir, stageDir);
    foreach (var pdb in Directory.EnumerateFiles(stageDir, "*.pdb", SearchOption.AllDirectories))
    {
        File.Delete(pdb);
    }

    Console.WriteLine("==> Building Inno Setup installer");
    RunOrThrow(iscc, isccArgs, root);

    if (!File.Exists(setupPath))
    {
        throw new InvalidOperationException($"Installer build completed but setup file was not found: {setupPath}");
    }

    Console.WriteLine();
    Console.WriteLine("Installer output ready:");
    Console.WriteLine($"  Setup:  {setupPath}");
    Console.WriteLine($"  Bytes:  {new FileInfo(setupPath).Length}");
    Console.WriteLine($"  SHA256: {Sha256(setupPath)}");
    return 0;
}

static int BuildFedoraInstaller(string root, InstallerOptions options)
{
    if (!Regex.IsMatch(options.Version, @"^[0-9A-Za-z._+~]+$"))
    {
        throw new ArgumentException($"RPM version contains unsupported characters: {options.Version}");
    }

    Console.WriteLine("Installer: Fedora RPM (.rpm)");

    if (FindCommand("rpmbuild") is null)
    {
        Console.Error.WriteLine("rpmbuild not found. Install: sudo dnf install rpm-build");
        return 1;
    }

    var tasks = RequiredFile(root, "Tasks.cs");
    RequiredFile(root, "PicLens", "Assets", "Square150x150Logo.scale-200.png");
    RequiredFile(root, "PicLens", "Assets", "Square44x44Logo.targetsize-48_altform-lightunplated.png");

    var runtime = "linux-x64";
    var platform = "x64";
    var payloadDir = SafePath(root, "artifacts", "portable", $"PicLens-{runtime}");
    var rpmTop = SafePath(root, "artifacts", "rpm");
    var rpmOutputDir = SafePath(root, "artifacts", "installer");
    var specFile = SafePath(root, "artifacts", "rpm", "SPECS", "piclens.spec");

    var releaseArgs = new List<string>
    {
        "run", "--file", tasks, "--",
        "release",
        "--configuration", "Release",
        "--runtime", runtime,
        "--platform", platform
    };
    if (options.NoClean) releaseArgs.Add("--no-clean");

    var rpmArgs = new[]
    {
        "-bb", specFile,
        "--target", "x86_64",
        "--define", $"_topdir {rpmTop}",
        "--define", $"piclens_version {options.Version}",
        "--define", $"piclens_payload_dir {payloadDir}",
        "--define", $"piclens_root_dir {root}",
        "--define", $"piclens_runtime_requires {options.RuntimeRequires}"
    };

    if (options.DryRun)
    {
        if (!options.NoRelease)
        {
            PrintCommand("dotnet", releaseArgs);
        }

        PrintCommand("rpmbuild", rpmArgs);
        return 0;
    }

    if (!options.NoRelease)
    {
        Console.WriteLine("==> Building portable release");
        RunOrThrow("dotnet", releaseArgs, root);
    }

    if (!File.Exists(Path.Combine(payloadDir, "PicLens")))
    {
        throw new InvalidOperationException($"Portable release was not found: {payloadDir}");
    }

    DeleteIfExists(rpmTop);
    Directory.CreateDirectory(Path.Combine(rpmTop, "BUILD"));
    Directory.CreateDirectory(Path.Combine(rpmTop, "BUILDROOT"));
    Directory.CreateDirectory(Path.Combine(rpmTop, "RPMS"));
    Directory.CreateDirectory(Path.Combine(rpmTop, "SOURCES"));
    Directory.CreateDirectory(Path.Combine(rpmTop, "SPECS"));
    Directory.CreateDirectory(rpmOutputDir);

    File.WriteAllText(specFile, """
    %global debug_package %{nil}

    Name: piclens
    Version: %{piclens_version}
    Release: 1%{?dist}
    Summary: PicLens image organizer and viewer
    License: LicenseRef-PicLens
    Requires: %{piclens_runtime_requires}
    Requires: hicolor-icon-theme

    %description
    PicLens is a Windows and Linux Avalonia image organizer and viewer.

    %prep

    %build

    %install
    rm -rf %{buildroot}
    mkdir -p %{buildroot}%{_libdir}/piclens
    cp -a "%{piclens_payload_dir}/." %{buildroot}%{_libdir}/piclens/
    chmod 0755 %{buildroot}%{_libdir}/piclens/PicLens

    mkdir -p %{buildroot}%{_bindir}
    ln -s %{_libdir}/piclens/PicLens %{buildroot}%{_bindir}/piclens

    install -Dm0644 "%{piclens_root_dir}/PicLens/Assets/Square44x44Logo.targetsize-48_altform-lightunplated.png" \
        %{buildroot}%{_datadir}/icons/hicolor/48x48/apps/piclens.png
    install -Dm0644 "%{piclens_root_dir}/PicLens/Assets/Square150x150Logo.scale-200.png" \
        %{buildroot}%{_datadir}/icons/hicolor/300x300/apps/piclens.png

    mkdir -p %{buildroot}%{_datadir}/applications
    cat > %{buildroot}%{_datadir}/applications/piclens.desktop <<'DESKTOP'
    [Desktop Entry]
    Type=Application
    Name=PicLens
    Comment=Image organizer and viewer
    Exec=piclens
    Icon=piclens
    Terminal=false
    Categories=Graphics;Photography;
    StartupNotify=true
    DESKTOP

    %files
    %{_bindir}/piclens
    %{_libdir}/piclens/
    %{_datadir}/applications/piclens.desktop
    %{_datadir}/icons/hicolor/48x48/apps/piclens.png
    %{_datadir}/icons/hicolor/300x300/apps/piclens.png
    """);

    Console.WriteLine("==> Building Fedora RPM");
    RunOrThrow("rpmbuild", rpmArgs, root);

    var rpmFile = Directory.EnumerateFiles(Path.Combine(rpmTop, "RPMS", "x86_64"), $"piclens-{options.Version}-*.x86_64.rpm")
        .OrderBy(path => path, StringComparer.Ordinal)
        .LastOrDefault();

    if (rpmFile is null)
    {
        throw new InvalidOperationException("RPM build completed but package was not found.");
    }

    var outputFile = Path.Combine(rpmOutputDir, $"PicLens-{options.Version}-fedora-x86_64.rpm");
    File.Copy(rpmFile, outputFile, overwrite: true);

    Console.WriteLine();
    Console.WriteLine("Fedora RPM ready:");
    Console.WriteLine($"  File:   {outputFile}");
    Console.WriteLine($"  Install: sudo dnf install {outputFile}");
    Console.WriteLine($"  SHA256: {Sha256(outputFile)}");
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
            File.Exists(Path.Combine(dir.FullName, "PicLens.slnx")))
        {
            return dir.FullName;
        }

        dir = dir.Parent;
    }

    throw new InvalidOperationException("Run this from the PicLens repository.");
}

static string? FindInnoSetupCompiler(string? path)
{
    if (!string.IsNullOrWhiteSpace(path))
    {
        var resolved = Path.GetFullPath(path);
        if (!File.Exists(resolved))
        {
            throw new InvalidOperationException($"Inno Setup compiler was not found: {resolved}");
        }

        return resolved;
    }

    var command = FindCommand("iscc.exe");
    if (command is not null)
    {
        return command;
    }

    var candidates = new[]
    {
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Inno Setup 6", "ISCC.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Inno Setup 6", "ISCC.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Inno Setup 6", "ISCC.exe")
    };

    return candidates.FirstOrDefault(File.Exists);
}

static string? FindCommand(string command)
{
    var path = Environment.GetEnvironmentVariable("PATH");
    if (string.IsNullOrWhiteSpace(path)) return null;

    var extensions = OperatingSystem.IsWindows()
        ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';')
        : [string.Empty];

    foreach (var directory in path.Split(Path.PathSeparator))
    {
        if (string.IsNullOrWhiteSpace(directory)) continue;

        foreach (var extension in extensions)
        {
            var candidate = Path.Combine(directory, command.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? command : command + extension);
            if (File.Exists(candidate)) return candidate;
        }
    }

    return null;
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

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
    {
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    }

    foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static void DeleteIfExists(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
    else if (File.Exists(path))
    {
        File.Delete(path);
    }
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

static void PrintCommand(string fileName, IReadOnlyList<string> arguments)
{
    Console.WriteLine($"  {fileName} {string.Join(' ', arguments.Select(Quote))}");
}

static string Quote(string value)
{
    return value.Contains(' ') ? $"\"{value}\"" : value;
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
      dotnet run Tasks.cs -- <command> [options]

    Commands:
      test       Restore and run Core, Infrastructure, and ViewModel tests
      run        Build and optionally launch the app
      release    Publish the portable release folder
      installer  Build the platform installer
      ui-test    Run Avalonia Headless UI smoke tests
    """);
}

sealed record InstallerOptions
{
    public string Version { get; init; } = "";
    public string RuntimeRequires { get; init; } = "dotnet-runtime-10.0";
    public string? InnoSetupCompiler { get; init; }
    public bool NoClean { get; init; }
    public bool NoRelease { get; init; }
    public bool DryRun { get; init; }
    public bool ShowHelp { get; init; }

    public static InstallerOptions Parse(string[] args)
    {
        var options = new InstallerOptions();
        for (var i = 0; i < args.Length; i++)
        {
            options = args[i] switch
            {
                "-h" or "--help" => options with { ShowHelp = true },
                "--no-clean" => options with { NoClean = true },
                "--no-release" => options with { NoRelease = true },
                "--dry-run" => options with { DryRun = true },
                "--version" => options with { Version = ReadValue(args, ref i, args[i]) },
                "--runtime-requires" => options with { RuntimeRequires = ReadValue(args, ref i, args[i]) },
                "--inno-setup-compiler" => options with { InnoSetupCompiler = ReadValue(args, ref i, args[i]) },
                _ => throw new ArgumentException($"Unknown option: {args[i]}")
            };
        }

        return options;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
        Usage:
          dotnet run Tasks.cs -- installer [options]

        Options:
          --version VERSION             Override package version from VERSION
          --no-clean                    Keep existing portable output where possible
          --no-release                  Reuse existing portable output for RPM builds
          --runtime-requires PACKAGE    RPM runtime dependency. Default: dotnet-runtime-10.0
          --inno-setup-compiler PATH    Windows ISCC.exe path
          --dry-run                     Print commands without running them
        """);
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
}
