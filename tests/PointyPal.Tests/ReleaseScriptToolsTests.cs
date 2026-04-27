using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace PointyPal.Tests;

public class ReleaseScriptToolsTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _repoRoot;

    public ReleaseScriptToolsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _repoRoot = FindRepoRoot();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void GetPointyPalSha256_ReturnsExpectedHash()
    {
        string file = Path.Combine(_tempDir, "sample.txt");
        File.WriteAllText(file, "hello", Encoding.UTF8);

        string actual = RunPowerShell($". {PsQuote(ReleaseToolsPath)}; Get-PointyPalSha256 -Path {PsQuote(file)}").Trim();

        using var sha = SHA256.Create();
        string expected = Convert.ToHexString(sha.ComputeHash(File.ReadAllBytes(file))).ToLowerInvariant();
        actual.Should().Be(expected);
    }

    [Fact]
    public void CreateReleaseManifest_WritesMetadataAndHashesForTempArtifacts()
    {
        string portableDir = Path.Combine(_tempDir, "PointyPal-portable");
        Directory.CreateDirectory(Path.Combine(portableDir, "docs"));
        File.WriteAllText(Path.Combine(portableDir, "PointyPal.exe"), "fake exe");
        File.WriteAllText(Path.Combine(portableDir, "PointyPal.dll"), "fake dll");
        File.WriteAllText(Path.Combine(portableDir, "config.example.json"), "{}");
        File.WriteAllText(Path.Combine(portableDir, "NOTICE.txt"), "notice");
        File.WriteAllText(Path.Combine(portableDir, "README-FIRST-RUN.md"), "readme");
        File.WriteAllText(Path.Combine(portableDir, "docs", "local-release.md"), "docs");

        RunPowerShell(
            $"& {PsQuote(Path.Combine(_repoRoot, "scripts", "create-release-manifest.ps1"))} " +
            $"-PortableDir {PsQuote(portableDir)} -RepoRoot {PsQuote(_repoRoot)} -BuildDate '2026-05-02' -RuntimeTarget 'win-x64'");

        string manifestPath = Path.Combine(portableDir, "release-manifest.json");
        string checksumsPath = Path.Combine(portableDir, "checksums.txt");

        File.Exists(manifestPath).Should().BeTrue();
        File.Exists(checksumsPath).Should().BeTrue();

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        root.GetProperty("appName").GetString().Should().Be("PointyPal");
        root.GetProperty("version").GetString().Should().Be("0.21.0");
        root.GetProperty("releaseLabel").GetString().Should().Be("private-rc.1");
        root.GetProperty("buildChannel").GetString().Should().Be("private-rc");
        root.GetProperty("buildDate").GetString().Should().Be("2026-05-02");
        root.GetProperty("runtimeTarget").GetString().Should().Be("win-x64");
        root.GetProperty("selfContained").GetBoolean().Should().BeFalse();
        root.GetProperty("docsIncluded").GetBoolean().Should().BeTrue();

        var signing = root.GetProperty("signing");
        signing.GetProperty("portableExeSigned").GetBoolean().Should().BeFalse();
        signing.GetProperty("portableExeSignatureStatus").GetString().Should().NotBeNullOrWhiteSpace();
        signing.GetProperty("portableExeSigner").GetString().Should().NotBeNull();
        signing.GetProperty("installerSigned").GetBoolean().Should().BeFalse();
        signing.GetProperty("installerSignatureStatus").GetString().Should().NotBeNullOrWhiteSpace();
        signing.GetProperty("installerSigner").GetString().Should().NotBeNull();
        signing.GetProperty("timestamped").GetBoolean().Should().BeFalse();
        signing.GetProperty("verifiedAt").GetString().Should().NotBeNullOrWhiteSpace();

        var exeEntry = root.GetProperty("files").EnumerateArray()
            .Single(f => f.GetProperty("path").GetString() == "PointyPal.exe");
        exeEntry.GetProperty("sha256").GetString().Should().NotBeNullOrWhiteSpace();
        File.ReadAllText(checksumsPath).Should().Contain("PointyPal.exe");
    }

    [Fact]
    public void PackageFileFilter_ExcludesLocalConfigLogsDebugSecretsAndBuildLeftovers()
    {
        string output = RunPowerShell($@"
. {PsQuote(ReleaseToolsPath)}
$paths = @('PointyPal.exe','docs/local-release.md','config.json','logs/app.log','debug/latest.json','history/session.json','usage/stats.json','.env','secrets/key.txt','certs/test.pfx','signing/key.pem','PointyPal.pdb','bin/x.dll','obj/x.dll','local.p12','public.cer','private.pvk','publisher.spc')
foreach ($p in $paths) {{ ""$p=$((Test-PointyPalPackageFileAllowed -RelativePath $p))"" }}
");

        var results = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('='))
            .ToDictionary(parts => parts[0], parts => bool.Parse(parts[1]));

        results["PointyPal.exe"].Should().BeTrue();
        results["docs/local-release.md"].Should().BeTrue();
        results["config.json"].Should().BeFalse();
        results["logs/app.log"].Should().BeFalse();
        results["debug/latest.json"].Should().BeFalse();
        results["history/session.json"].Should().BeFalse();
        results["usage/stats.json"].Should().BeFalse();
        results[".env"].Should().BeFalse();
        results["secrets/key.txt"].Should().BeFalse();
        results["certs/test.pfx"].Should().BeFalse();
        results["signing/key.pem"].Should().BeFalse();
        results["PointyPal.pdb"].Should().BeFalse();
        results["bin/x.dll"].Should().BeFalse();
        results["obj/x.dll"].Should().BeFalse();
        results["local.p12"].Should().BeFalse();
        results["public.cer"].Should().BeFalse();
        results["private.pvk"].Should().BeFalse();
        results["publisher.spc"].Should().BeFalse();
    }

    [Fact]
    public void InstallerFileName_UsesBuildMetadataVersionAndChannel()
    {
        string output = RunPowerShell($@"
. {PsQuote(ReleaseToolsPath)}
$metadata = Get-PointyPalReleaseMetadata -RepoRoot {PsQuote(_repoRoot)}
Get-PointyPalInstallerFileName -Metadata $metadata -RuntimeTarget 'win-x64'
").Trim();

        output.Should().Be("PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe");
    }

    [Fact]
    public void CreateInstallerManifest_WritesUnsignedInstallerMetadataAndHash()
    {
        string installerDir = Path.Combine(_tempDir, "installer");
        Directory.CreateDirectory(installerDir);
        string installerPath = Path.Combine(installerDir, "PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe");
        File.WriteAllBytes(installerPath, new byte[] { 0x4D, 0x5A, 0x00, 0x01 });

        RunPowerShell(
            $"& {PsQuote(Path.Combine(_repoRoot, "scripts", "create-installer-manifest.ps1"))} " +
            $"-InstallerPath {PsQuote(installerPath)} -RepoRoot {PsQuote(_repoRoot)} -BuildDate '2026-05-02' -RuntimeTarget 'win-x64'");

        string manifestPath = Path.Combine(installerDir, "installer-manifest.json");
        string checksumsPath = Path.Combine(installerDir, "installer-checksums.txt");

        File.Exists(manifestPath).Should().BeTrue();
        File.Exists(checksumsPath).Should().BeTrue();

        using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var root = doc.RootElement;

        root.GetProperty("appName").GetString().Should().Be("PointyPal");
        root.GetProperty("version").GetString().Should().Be("0.21.0");
        root.GetProperty("releaseLabel").GetString().Should().Be("private-rc.1");
        root.GetProperty("buildChannel").GetString().Should().Be("private-rc");
        root.GetProperty("runtimeTarget").GetString().Should().Be("win-x64");

        var installer = root.GetProperty("installer");
        installer.GetProperty("filename").GetString().Should().Be("PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe");
        installer.GetProperty("sizeBytes").GetInt64().Should().Be(4);
        installer.GetProperty("sha256").GetString().Should().NotBeNullOrWhiteSpace();
        installer.GetProperty("signed").GetBoolean().Should().BeFalse();
        installer.GetProperty("signatureStatus").GetString().Should().NotBeNullOrWhiteSpace();
        installer.GetProperty("signer").GetString().Should().NotBeNull();
        installer.GetProperty("timestamped").GetBoolean().Should().BeFalse();
        installer.GetProperty("signatureVerifiedAt").GetString().Should().NotBeNullOrWhiteSpace();
        File.ReadAllText(checksumsPath).Should().Contain("PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe");
    }

    [Fact]
    public void ValidateInstaller_AcceptsExplicitUnsignedInstallerWithoutInstallTest()
    {
        string installerDir = Path.Combine(_tempDir, "installer-validation");
        Directory.CreateDirectory(installerDir);
        string installerPath = Path.Combine(installerDir, "PointyPal-v0.21.0-private-rc.1-win-x64-setup.exe");
        File.WriteAllBytes(installerPath, new byte[] { 0x4D, 0x5A, 0x00, 0x01 });

        RunPowerShell(
            $"& {PsQuote(Path.Combine(_repoRoot, "scripts", "create-installer-manifest.ps1"))} " +
            $"-InstallerPath {PsQuote(installerPath)} -RepoRoot {PsQuote(_repoRoot)} -BuildDate '2026-05-02' -RuntimeTarget 'win-x64'");

        string output = RunPowerShell(
            $"& {PsQuote(Path.Combine(_repoRoot, "scripts", "validate-installer.ps1"))} " +
            $"-InstallerPath {PsQuote(installerPath)} -ManifestPath {PsQuote(Path.Combine(installerDir, "installer-manifest.json"))} -RuntimeTarget 'win-x64' -MaxSizeMB 1");

        output.Should().Contain("Installer validation SUCCESSFUL.");
        output.Should().Contain("Installer execution skipped.");
    }

    [Fact]
    public void InstallerExcludePatterns_CoverLocalConfigLogsDebugHistoryAndSecrets()
    {
        string output = RunPowerShell($@"
. {PsQuote(ReleaseToolsPath)}
Get-PointyPalInstallerExcludePatterns
");

        var patterns = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        patterns.Should().Contain("config.json");
        patterns.Should().Contain("logs\\*");
        patterns.Should().Contain("debug\\*");
        patterns.Should().Contain("history\\*");
        patterns.Should().Contain("usage\\*");
        patterns.Should().Contain("secrets\\*");
        patterns.Should().Contain(".env");
        patterns.Should().Contain("*.key");
        patterns.Should().Contain("*.pem");
        patterns.Should().Contain("*.pfx");
        patterns.Should().Contain("*.p12");
        patterns.Should().Contain("*.cer");
        patterns.Should().Contain("*.pvk");
        patterns.Should().Contain("*.spc");
        patterns.Should().Contain("certs\\*");
        patterns.Should().Contain("signing\\*");
        patterns.Should().Contain("*.pdb");

        File.ReadAllText(Path.Combine(_repoRoot, "installer", "PointyPal.iss"))
            .Should()
            .Contain("Excludes: \"{#InstallerExcludes}\"");
    }

    [Fact]
    public void ReleaseCheck_ExposesInstallerAndSigningSwitches()
    {
        string scriptPath = Path.Combine(_repoRoot, "scripts", "release-check.ps1");
        string output = RunPowerShell($@"
$command = Get-Command {PsQuote(scriptPath)}
$command.Parameters.ContainsKey('IncludeInstaller')
$command.Parameters.ContainsKey('Sign')
$command.Parameters.ContainsKey('VerifySignatures')
$command.Parameters.ContainsKey('RequireSigned')
").Trim();

        output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(bool.Parse)
            .Should()
            .OnlyContain(value => value);
    }

    [Fact]
    public void SigningScripts_ExposeExpectedParameters()
    {
        string signScriptPath = Path.Combine(_repoRoot, "scripts", "sign-artifacts.ps1");
        string verifyScriptPath = Path.Combine(_repoRoot, "scripts", "verify-signatures.ps1");
        string certScriptPath = Path.Combine(_repoRoot, "scripts", "create-local-test-cert.ps1");

        string output = RunPowerShell($@"
$sign = Get-Command {PsQuote(signScriptPath)}
$verify = Get-Command {PsQuote(verifyScriptPath)}
$cert = Get-Command {PsQuote(certScriptPath)}
$names = @(
    'CertPath',
    'CertPassword',
    'CertThumbprint',
    'TimestampUrl',
    'PortablePath',
    'InstallerPath',
    'SkipIfNoCertificate',
    'WhatIf'
)
foreach ($name in $names) {{ ""sign:$name=$($sign.Parameters.ContainsKey($name))"" }}
foreach ($name in @('PortablePath','InstallerPath','RequireSigned')) {{ ""verify:$name=$($verify.Parameters.ContainsKey($name))"" }}
foreach ($name in @('ExportPfx','OutputPath')) {{ ""cert:$name=$($cert.Parameters.ContainsKey($name))"" }}
");

        output.Should().NotContain("False");
    }

    [Fact]
    public void GitIgnore_ExcludesSigningPrivateMaterial()
    {
        string text = File.ReadAllText(Path.Combine(_repoRoot, ".gitignore"));

        text.Should().Contain("*.pfx");
        text.Should().Contain("*.p12");
        text.Should().Contain("*.cer");
        text.Should().Contain("*.pvk");
        text.Should().Contain("*.spc");
        text.Should().Contain("*.key");
        text.Should().Contain("*.pem");
        text.Should().Contain("certs/");
        text.Should().Contain("signing/");
        text.Should().Contain("secrets/");
    }

    [Fact]
    public void SecretScanner_FindsObviousLiveSecretsButIgnoresPlaceholders()
    {
        string portableDir = Path.Combine(_tempDir, "portable");
        Directory.CreateDirectory(portableDir);
        File.WriteAllText(Path.Combine(portableDir, "config.example.json"), "{\"WorkerClientKey\":\"YOUR_POINTYPAL_CLIENT_KEY\"}");
        File.WriteAllText(Path.Combine(portableDir, "leak.txt"), "api_key=sk-1234567890abcdef1234567890abcdef");

        string output = RunPowerShell($@"
. {PsQuote(ReleaseToolsPath)}
Find-PointyPalObviousSecrets -PortableDir {PsQuote(portableDir)} | ForEach-Object {{ $_.Path }}
");

        output.Should().Contain("leak.txt");
        output.Should().NotContain("config.example.json");
    }

    private string ReleaseToolsPath => Path.Combine(_repoRoot, "scripts", "release-tools.ps1");

    private static string PsQuote(string value)
    {
        return "'" + value.Replace("'", "''") + "'";
    }

    private static string RunPowerShell(string command)
    {
        var psi = new ProcessStartInfo("powershell")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-Command");
        psi.ArgumentList.Add(command);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"PowerShell failed with exit code {process.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }

        return stdout;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "PointyPal.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
