using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using StructuralToolbox.Update;

internal static class UpdateCoreTests
{
    private static int _failures;

    private static void Main()
    {
        TestVersionComparison();
        TestManifestValidation();
        TestLocalDownloadAndHashVerification();
        TestManifestFallbackAndCancellation();
        TestDownloadFallbackAndCancellation();
        if (_failures != 0)
        {
            Console.Error.WriteLine("Update core failures: " + _failures);
            Environment.Exit(1);
        }
        Console.WriteLine("UPDATE_CORE_TESTS=PASS");
    }

    private static void TestVersionComparison()
    {
        Assert(VersionComparer.Compare(new Version(0, 4, 1),
            new Version(0, 4, 0, 9)) > 0, "new patch is newer");
        Assert(VersionComparer.Compare(new Version(0, 4, 0),
            new Version(0, 4, 0, 0)) == 0, "missing revision is zero");
        Assert(!VersionComparer.IsNewerThan(new Version(0, 3, 9),
            new Version(0, 4, 0)), "older release is not newer");
    }

    private static void TestManifestValidation()
    {
        string hash = new string('a', 64);
        UpdateManifest parsed = UpdateManifestParser.Parse(
            "{\"schemaVersion\":1," +
            "\"product\":\"FourJTooLs.StructuralToolbox\"," +
            "\"channel\":\"stable\",\"version\":\"0.4.1\"," +
            "\"url\":\"https://downloads.example.test/toolbox.exe\"," +
            "\"sha256\":\"" + hash + "\",\"sizeBytes\":123," +
            "\"date\":\"2026-08-27\",\"notes\":[\"安全更新\"]}");
        Assert(parsed.Version == new Version(0, 4, 1),
            "manifest version parsed");
        Assert(parsed.SizeBytes == 123, "manifest size parsed");
        Assert(parsed.PackageUrls.Count == 1, "primary package URL is recorded");
        UpdateManifest mirrored = UpdateManifestParser.Parse(
            "{\"schemaVersion\":1,\"product\":\"FourJTooLs.StructuralToolbox\"," +
            "\"channel\":\"stable\",\"version\":\"0.4.1\",\"url\":" +
            "\"https://mirror.example.test/toolbox.exe\",\"mirrors\":[" +
            "\"https://github.com/example/toolbox.exe\"],\"sha256\":\"" + hash + "\"}");
        Assert(mirrored.PackageUrls.Count == 2, "mirror package URL is recorded");
        ExpectUpdateException(delegate
        {
            UpdateManifestParser.Parse(
                "{\"schemaVersion\":1," +
                "\"product\":\"FourJTooLs.StructuralToolbox\"," +
                "\"channel\":\"stable\",\"version\":\"0.4.1\",\"url\":" +
                "\"http://downloads.example.test/toolbox.exe\"," +
                "\"sha256\":\"" + hash + "\"}");
        }, "plain HTTP package rejected");
        ExpectUpdateException(delegate
        {
            UpdateManifestParser.Parse(
                "{\"schemaVersion\":1," +
                "\"product\":\"FourJTooLs.StructuralToolbox\"," +
                "\"channel\":\"stable\",\"version\":\"0.4.1\",\"url\":" +
                "\"https://downloads.example.test/toolbox.exe\"," +
                "\"sha256\":\"short\"}");
        }, "invalid hash rejected");
    }

    private static void TestLocalDownloadAndHashVerification()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "FourJTooLs-update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string packagePath = Path.Combine(root, "setup.exe");
            File.WriteAllBytes(packagePath,
                Encoding.UTF8.GetBytes("verified installer fixture"));
            string hash = ComputeSha256(packagePath);
            UpdateManifest manifest = UpdateManifestParser.Parse(
                "{\"schemaVersion\":1," +
                "\"product\":\"FourJTooLs.StructuralToolbox\"," +
                "\"channel\":\"stable\",\"version\":\"0.4.1\",\"url\":" +
                Quote(new Uri(packagePath).AbsoluteUri) + ",\"sha256\":" +
                Quote(hash) + ",\"sizeBytes\":" +
                new FileInfo(packagePath).Length + "}");
            string downloadDirectory = Path.Combine(root, "download");
            string downloaded = new UpdateService().DownloadInstaller(
                manifest, null, downloadDirectory);
            Assert(File.Exists(downloaded), "verified installer downloaded");
            Assert(ComputeSha256(downloaded) == hash,
                "downloaded installer hash matches");

            manifest.Sha256 = new string('0', 64);
            ExpectUpdateException(delegate
            {
                new UpdateService().DownloadInstaller(manifest, null,
                    Path.Combine(root, "bad-download"));
            }, "tampered package rejected");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }


    private static string ManifestJson(string url, string hash, long size)
    {
        return "{\"schemaVersion\":1,\"product\":\"FourJTooLs.StructuralToolbox\"," +
            "\"channel\":\"stable\",\"version\":\"1.0.3\",\"url\":" + Quote(url) +
            ",\"sha256\":" + Quote(hash) + ",\"sizeBytes\":" + size + "}";
    }

    private static void TestManifestFallbackAndCancellation()
    {
        Uri first = new Uri("https://primary.example.test/stable.json");
        Uri second = new Uri("https://fallback.example.test/stable.json");
        string json = ManifestJson("https://primary.example.test/setup.exe", new string('a', 64), 20);
        int calls = 0;
        UpdateService service = new UpdateService(delegate(Uri uri, CancellationToken token)
        {
            calls++;
            if (uri == first) throw new WebException("unavailable");
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }, 100, 100);
        Assert(service.FetchManifest(new[] { first, second }, CancellationToken.None, null)
            .Version == new Version(1, 0, 3) && calls == 2, "failed primary falls back");
        calls = 0;
        service = new UpdateService(delegate(Uri uri, CancellationToken token)
        {
            calls++;
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }, 100, 100);
        service.FetchManifest(new[] { first, second }, CancellationToken.None, null);
        Assert(calls == 1, "reachable primary remains authoritative for rollbacks");
        service = new UpdateService(delegate(Uri uri, CancellationToken token)
        {
            if (uri == first)
            {
                token.WaitHandle.WaitOne(2000);
                token.ThrowIfCancellationRequested();
                throw new Exception("attempt deadline did not fire");
            }
            return new MemoryStream(Encoding.UTF8.GetBytes(json));
        }, 40, 100);
        Assert(service.FetchManifest(new[] { first, second }, CancellationToken.None, null) != null,
            "stalled manifest reaches fallback after total deadline");
        using (CancellationTokenSource cancel = new CancellationTokenSource())
        {
            calls = 0;
            service = new UpdateService(delegate(Uri uri, CancellationToken token)
            {
                calls++;
                cancel.Cancel();
                token.ThrowIfCancellationRequested();
                throw new Exception("cancellation ignored");
            }, 100, 100);
            try
            {
                service.FetchManifest(new[] { first, second }, cancel.Token, null);
                Assert(false, "check cancellation is reported");
            }
            catch (OperationCanceledException) { Assert(calls == 1, "cancel skips fallback"); }
        }
        service = new UpdateService(delegate(Uri uri, CancellationToken token)
        {
            return new MemoryStream(new byte[262145]);
        }, 100, 100);
        ExpectUpdateException(delegate { service.FetchManifest(first); }, "oversize manifest rejected");
        string localJson = ManifestJson("file:///C:/private.exe", new string('a', 64), 20);
        service = new UpdateService(delegate(Uri uri, CancellationToken token)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(localJson));
        }, 100, 100);
        ExpectUpdateException(delegate { service.FetchManifest(first); }, "online manifest cannot request a local file");
        string badMirror = json.TrimEnd('}') + ",\"mirrors\":[\"http://unsafe.test/setup.exe\"]}";
        ExpectUpdateException(delegate { UpdateManifestParser.Parse(badMirror); }, "HTTP mirror rejected");
        Assert(VersionComparer.Compare(new Version(1, 0, 3), new Version(1, 0, 3, 0)) == 0,
            "three and four part installed versions are equivalent");
    }

    private static void TestDownloadFallbackAndCancellation()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "update-fallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            byte[] good = Encoding.UTF8.GetBytes("complete installer fixture");
            string fixture = Path.Combine(root, "fixture.exe");
            File.WriteAllBytes(fixture, good);
            string json = ManifestJson("https://primary.example.test/setup.exe", ComputeSha256(fixture), good.Length);
            UpdateManifest manifest = UpdateManifestParser.Parse(json.TrimEnd('}') +
                ",\"mirrors\":[\"https://fallback.example.test/setup.exe\"]}");
            int calls = 0;
            UpdateService service = new UpdateService(delegate(Uri uri, CancellationToken token)
            {
                calls++;
                return new MemoryStream(uri.Host.StartsWith("primary") ? new byte[good.Length] : good);
            }, 100, 100);
            string path = service.DownloadInstaller(manifest, null, Path.Combine(root, "ok"));
            Assert(calls == 2 && ComputeSha256(path) == manifest.Sha256, "corrupt primary uses verified fallback");
            service.DownloadInstaller(manifest, null, Path.Combine(root, "ok"));
            Assert(calls == 2, "already verified installer is reused without network");
            Assert(Directory.GetFiles(Path.GetDirectoryName(path), "*.part").Length == 0, "fallback cleans temporary files");

            string cancelDir = Path.Combine(root, "cancel");
            Directory.CreateDirectory(cancelDir);
            string old = Path.Combine(cancelDir, "FourJ_PV_Toolbox_Setup_v1.0.3.exe");
            File.WriteAllText(old, "existing file");
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                service = new UpdateService(delegate(Uri uri, CancellationToken token)
                {
                    return new MemoryStream(good);
                }, 100, 100);
                try
                {
                    service.DownloadInstaller(manifest, delegate(long copied, long total)
                    { if (copied > 0) cancel.Cancel(); }, cancelDir, cancel.Token, null);
                    Assert(false, "cancelled download must not commit");
                }
                catch (OperationCanceledException) { }
            }
            Assert(File.ReadAllText(old) == "existing file", "cancel preserves previous file");
            Assert(Directory.GetFiles(cancelDir, "*.part").Length == 0, "cancel cleans partial file");
            service = new UpdateService(delegate(Uri uri, CancellationToken token)
            { throw new WebException("offline"); }, 100, 100);
            ExpectUpdateException(delegate { service.DownloadInstaller(manifest, null, cancelDir); },
                "all unavailable downloads fail");
            Assert(File.ReadAllText(old) == "existing file", "failed downloads preserve previous file");
            service = new UpdateService(delegate(Uri uri, CancellationToken token)
            {
                if (uri.Host.StartsWith("primary"))
                {
                    token.WaitHandle.WaitOne(2000);
                    token.ThrowIfCancellationRequested();
                    throw new Exception("download deadline did not fire");
                }
                return new MemoryStream(good);
            }, 100, 40);
            Assert(File.Exists(service.DownloadInstaller(manifest, null, Path.Combine(root, "timeout"))),
                "stalled package uses fallback after deadline");
        }
        finally { Directory.Delete(root, true); }
    }
    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string ComputeSha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (Stream stream = File.OpenRead(path))
        {
            StringBuilder result = new StringBuilder();
            foreach (byte value in sha.ComputeHash(stream))
                result.Append(value.ToString("x2"));
            return result.ToString();
        }
    }

    private static void ExpectUpdateException(Action action, string name)
    {
        try
        {
            action();
            Assert(false, name);
        }
        catch (UpdateException)
        {
            Assert(true, name);
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (condition)
            return;
        _failures++;
        Console.Error.WriteLine("FAIL: " + name);
    }
}
