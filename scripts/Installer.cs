using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

try
{
    var options = Options.Parse(args);
    if (options.ShowHelp)
    {
        Options.PrintUsage();
        return 0;
    }

    var root = FindRepoRoot();
    Console.WriteLine($"Platform: {RuntimeInformation.OSDescription.Trim()} ({RuntimeInformation.OSArchitecture})");

    if (OperatingSystem.IsWindows())
    {
        return BuildWindows(root, options);
    }

    if (OperatingSystem.IsLinux())
    {
        return BuildFedora(root, options);
    }

    throw new PlatformNotSupportedException("No installer exists for this platform. Supported hosts: Windows and Fedora Linux.");
}
catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or PlatformNotSupportedException or IOException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static int BuildWindows(string root, Options options)
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

    var tasks = RequiredFile(root, "scripts", "Tasks.cs");
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

static int BuildFedora(string root, Options options)
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

    var tasks = RequiredFile(root, "scripts", "Tasks.cs");
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
    Directory.CreateDirectory(Path.Combine(rpmTop, "SRPMS"));
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
    var path = Path.GetFullPath(Path.Combine([root, .. parts]));
    var resolvedRoot = Path.GetFullPath(root);
    var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    if (!path.Equals(resolvedRoot, comparison) && !path.StartsWith(resolvedRoot + Path.DirectorySeparatorChar, comparison))
    {
        throw new InvalidOperationException($"Refusing to operate outside workspace root: {path}");
    }

    return path;
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

static string Sha256(string path)
{
    using var stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

sealed record Options
{
    public string Version { get; init; } = "1.0.0";
    public string RuntimeRequires { get; init; } = "dotnet-runtime-10.0";
    public string? InnoSetupCompiler { get; init; }
    public bool NoClean { get; init; }
    public bool NoRelease { get; init; }
    public bool DryRun { get; init; }
    public bool ShowHelp { get; init; }

    public static Options Parse(string[] args)
    {
        var options = new Options();
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
          dotnet run --file scripts/Installer.cs -- [options]

        Options:
          --version VERSION             Installer version. Default: 1.0.0
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
