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
        "release" => Release(root, rest),
        "legacy-release" => LegacyRelease(root, rest),
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
        Console.WriteLine($"==> Running tests: {project}");
        RunOrThrow("dotnet", ["test", project, $"-p:RestoreConfigFile={nugetConfig}", "-p:NuGetAudit=false"], root);
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

    Console.WriteLine("==> Running Avalonia headless UI smoke tests");
    RunOrThrow("dotnet", ["test", project, "-c", configuration, $"-p:RestoreConfigFile={nugetConfig}", "-p:NuGetAudit=false"], root);
    return 0;
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
                  --configuration Release
                  --runtime win-x64|linux-x64
                  --platform x64

                Builds the primary Qt portable release. Use legacy-release only
                for temporary Avalonia coexistence verification.
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

    if (!configuration.Equals("Release", StringComparison.OrdinalIgnoreCase))
    {
        throw new ArgumentException("The primary Qt release command only accepts --configuration Release.");
    }
    if (noClean)
    {
        throw new ArgumentException("The primary Qt release is always clean; --no-clean is not supported.");
    }

    if (OperatingSystem.IsWindows())
    {
        if (runtime != "win-x64" || !platform.Equals("x64", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException("Qt Windows release currently supports win-x64 / x64.");
        }
        var script = RequiredFile(root, "qt", "scripts", "build-portable.ps1");
        RunOrThrow("pwsh", ["-NoProfile", "-File", script], root);
        return 0;
    }

    if (OperatingSystem.IsLinux())
    {
        if (runtime != "linux-x64" || !platform.Equals("x64", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException("Qt Linux release currently supports linux-x64 / x64.");
        }
        var script = RequiredFile(root, "qt", "scripts", "build-linux-portable.sh");
        RunOrThrow("bash", [script], root);
        return 0;
    }

    throw new PlatformNotSupportedException("Qt portable release supports Windows x64 and Linux x64 hosts.");
}

static int LegacyRelease(string root, string[] args)
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
                  dotnet run Tasks.cs -- legacy-release [options]

                Options:
                  --configuration Debug|Release
                  --runtime win-x64|win-arm64|win-x86|linux-x64
                  --platform x64|ARM64|x86
                  --no-clean

                Builds the temporary Avalonia portable release for coexistence
                and rollback verification.
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

    Console.WriteLine("==> Publishing framework-dependent output");
    RunOrThrow("dotnet",
    [
        "publish",
        project,
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
        $"/p:RestoreConfigFile={nugetConfig}",
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

    var installerProject = RequiredFile(root, "installer", "PicLens.wixproj");
    var portableScript = RequiredFile(root, "qt", "scripts", "build-portable.ps1");
    var auditScript = RequiredFile(root, "qt", "scripts", "audit-msi.ps1");
    var runtime = "win-x64";
    var portableDir = SafePath(root, "artifacts", "qt-portable", $"PicLens-{runtime}");
    var installerRoot = SafePath(root, "artifacts", "installer");
    var outputName = $"PicLens-{runtime}";
    var msiPath = SafePath(root, "artifacts", "installer", $"{outputName}.msi");
    var cabinetPath = SafePath(root, "artifacts", "installer", "cab1.cab");

    Console.WriteLine("Installer: Windows MSI (.msi)");

    var releaseArgs = new List<string>
    {
        "-NoProfile",
        "-File", portableScript
    };

    var wixArgs = new List<string>
    {
        "build",
        installerProject,
        "--no-incremental",
        "--configuration", "Release",
        $"/p:AppVersion={options.Version}",
        $"/p:PayloadDir={portableDir}",
        "/p:SuppressValidation=true",
        $"/p:OutputPath={installerRoot}{Path.DirectorySeparatorChar}",
        $"/p:OutputName={outputName}"
    };
    var auditArgs = new List<string>
    {
        "-NoProfile",
        "-File", auditScript,
        "-MsiPath", msiPath,
        "-PayloadDirectory", portableDir,
        "-ExpectedVersion", options.Version
    };

    if (options.DryRun)
    {
        if (!options.NoRelease)
        {
            PrintCommand("pwsh", releaseArgs);
        }
        PrintCommand("dotnet", wixArgs);
        PrintCommand("pwsh", auditArgs);
        return 0;
    }

    if (!options.NoRelease)
    {
        Console.WriteLine("==> Building Qt portable release");
        RunOrThrow("pwsh", releaseArgs, root);
    }

    if (!File.Exists(Path.Combine(portableDir, "PicLens.exe")))
    {
        throw new InvalidOperationException($"Portable release was not found: {portableDir}");
    }

    Directory.CreateDirectory(installerRoot);

    if (!options.NoClean)
    {
        DeleteIfExists(msiPath);
        DeleteIfExists(cabinetPath);
    }

    Console.WriteLine("==> Building WiX MSI installer");
    RunOrThrow("dotnet", wixArgs, root);

    if (!File.Exists(msiPath))
    {
        throw new InvalidOperationException($"Installer build completed but MSI file was not found: {msiPath}");
    }

    Console.WriteLine("==> Auditing MSI database and Qt payload");
    RunOrThrow("pwsh", auditArgs, root);

    Console.WriteLine();
    Console.WriteLine("Installer output ready:");
    Console.WriteLine($"  MSI:    {msiPath}");
    Console.WriteLine($"  Bytes:  {new FileInfo(msiPath).Length}");
    Console.WriteLine($"  SHA256: {Sha256(msiPath)}");
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
        "legacy-release",
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

    if (fileName == "dotnet" && File.Exists(Path.Combine(workingDirectory, "NuGet.Config")))
    {
        var appData = Path.Combine(workingDirectory, ".nuget", "appdata");
        var localAppData = Path.Combine(workingDirectory, ".nuget", "localappdata");
        var dotnetHome = Path.Combine(workingDirectory, ".nuget", "dotnet-home");
        Directory.CreateDirectory(appData);
        Directory.CreateDirectory(localAppData);
        Directory.CreateDirectory(dotnetHome);
        startInfo.Environment["APPDATA"] = appData;
        startInfo.Environment["LOCALAPPDATA"] = localAppData;
        startInfo.Environment["DOTNET_CLI_HOME"] = dotnetHome;
        startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
        startInfo.Environment["AVALONIA_TELEMETRY_OPTOUT"] = "1";
        startInfo.Environment["XDG_CONFIG_HOME"] = appData;
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
      release    Build the primary Qt portable release folder
      legacy-release  Publish the temporary Avalonia portable folder
      installer  Build the platform installer
      ui-test    Run Avalonia Headless UI smoke tests
    """);
}

sealed record InstallerOptions
{
    public string Version { get; init; } = "";
    public string RuntimeRequires { get; init; } = "dotnet-runtime-10.0";
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
          --no-clean                    Keep existing legacy Linux portable output where possible
          --no-release                  Reuse an existing portable output
          --runtime-requires PACKAGE    RPM runtime dependency. Default: dotnet-runtime-10.0
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
