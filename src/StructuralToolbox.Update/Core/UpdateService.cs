using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace StructuralToolbox.Update
{
    public sealed class UpdateService
    {
        public const int ManifestTimeoutMilliseconds = 8000;
        public const int PackageTimeoutMilliseconds = 120000;
        public const long MaximumPackageSizeBytes = 512L * 1024L * 1024L;
        private const int MaximumManifestBytes = 262144;
        private readonly Func<Uri, CancellationToken, Stream> _open;
        private readonly int _manifestTimeout;
        private readonly int _packageTimeout;

        public UpdateService() : this(OpenSource,
            ManifestTimeoutMilliseconds, PackageTimeoutMilliseconds) { }

        internal UpdateService(Func<Uri, CancellationToken, Stream> open,
            int manifestTimeout, int packageTimeout)
        {
            _open = open;
            _manifestTimeout = manifestTimeout;
            _packageTimeout = packageTimeout;
        }

        public static Version DetectInstalledVersion()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (assembly.GetName().Name == "StructuralToolbox")
                    return assembly.GetName().Version;
            return typeof(UpdateService).Assembly.GetName().Version;
        }

        public UpdateManifest FetchManifest(Uri sourceUri)
        {
            return FetchManifest(new[] { sourceUri }, CancellationToken.None, null);
        }

        public UpdateManifest FetchManifest(IList<Uri> sources,
            CancellationToken cancellation, Action<string> status)
        {
            if (sources == null || sources.Count == 0 || sources.Count > 5)
                throw new UpdateException("更新服务数量无效。");
            // The first reachable source is authoritative, including rollbacks.
            // A stale fallback must never override a reachable primary manifest.
            for (int i = 0; i < sources.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                Report(status, i == 0 ? "正在检查更新…" : "正在尝试备用更新服务…");
                using (CancellationTokenSource attempt =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    attempt.CancelAfter(_manifestTimeout);
                    try
                    {
                        RequireSource(sources[i]);
                        using (Stream input = _open(sources[i], attempt.Token))
                        using (MemoryStream buffer = new MemoryStream())
                        {
                            CopyBounded(input, buffer, MaximumManifestBytes,
                                attempt.Token, null, -1);
                            UpdateManifest manifest = UpdateManifestParser.Parse(
                                Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF'));
                            if (sources[i].Scheme == "https")
                                foreach (Uri url in PackageSources(manifest))
                                    if (url.Scheme != "https")
                                        throw new UpdateException("在线清单包含非 HTTPS 下载地址。");
                            attempt.Token.ThrowIfCancellationRequested();
                            return manifest;
                        }
                    }
                    catch (Exception error)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (!Recoverable(error)) throw;
                    }
                }
            }
            throw new UpdateException("更新服务暂时无法连接，请稍后重试。");
        }

        public string DownloadInstaller(UpdateManifest manifest,
            Action<long, long> progress, string targetDirectoryOverride)
        {
            return DownloadInstaller(manifest, progress, targetDirectoryOverride,
                CancellationToken.None, null);
        }

        public string DownloadInstaller(UpdateManifest manifest,
            Action<long, long> progress, string targetDirectoryOverride,
            CancellationToken cancellation, Action<string> status)
        {
            if (manifest == null) throw new ArgumentNullException("manifest");
            IList<Uri> sources = PackageSources(manifest);
            if (manifest.SizeBytes > MaximumPackageSizeBytes)
                throw new UpdateException("安装包超过 512 MB 上限。");
            cancellation.ThrowIfCancellationRequested();
            string directory = String.IsNullOrWhiteSpace(targetDirectoryOverride)
                ? Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "StructuralToolbox", "update")
                : targetDirectoryOverride;
            Directory.CreateDirectory(directory);
            string finalPath = Path.Combine(directory,
                "FourJ_PV_Toolbox_Setup_v" + manifest.Version.ToString(3) + ".exe");
            if (File.Exists(finalPath))
            {
                try { VerifyDownloadedFile(manifest, finalPath); return finalPath; }
                catch (UpdateException) { }
            }
            string partialPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".part";
            for (int i = 0; i < sources.Count; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                Report(status, i == 0 ? "正在下载安装包…" : "正在切换备用下载线路…");
                using (CancellationTokenSource attempt =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                {
                    attempt.CancelAfter(_packageTimeout);
                    try
                    {
                        if (progress != null) progress(0, manifest.SizeBytes);
                        using (Stream input = _open(sources[i], attempt.Token))
                        using (Stream output = File.Create(partialPath))
                            CopyBounded(input, output,
                                manifest.SizeBytes >= 0 ? manifest.SizeBytes : MaximumPackageSizeBytes,
                                attempt.Token, progress, manifest.SizeBytes);
                        VerifyDownloadedFile(manifest, partialPath);
                        attempt.Token.ThrowIfCancellationRequested();
                    }
                    catch (Exception error)
                    {
                        TryDeleteFile(partialPath);
                        cancellation.ThrowIfCancellationRequested();
                        if (!Recoverable(error)) throw;
                        continue;
                    }
                    // A disk commit error is not a network failure. Preserve any old file.
                    try
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (File.Exists(finalPath)) File.Replace(partialPath, finalPath, null);
                        else File.Move(partialPath, finalPath);
                        return finalPath;
                    }
                    finally { TryDeleteFile(partialPath); }
                }
            }
            throw new UpdateException("所有下载线路均未能取得校验通过的安装包，请稍后重试。");
        }

        private static IList<Uri> PackageSources(UpdateManifest manifest)
        {
            List<Uri> result = new List<Uri>();
            RequireSource(manifest.PackageUrl);
            result.Add(manifest.PackageUrl);
            foreach (Uri url in manifest.PackageUrls)
            {
                RequireSource(url);
                bool duplicate = false;
                foreach (Uri existing in result)
                    if (existing.AbsoluteUri == url.AbsoluteUri) duplicate = true;
                if (!duplicate) result.Add(url);
            }
            if (result.Count > 5) throw new UpdateException("下载线路数量超过上限。");
            return result;
        }

        private static void RequireSource(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri ||
                (uri.Scheme != "https" && uri.Scheme != "file") ||
                !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Fragment))
                throw new UpdateException("更新地址无效，在线更新必须使用 HTTPS。");
        }

        private static Stream OpenSource(Uri uri, CancellationToken cancellation)
        {
            RequireSource(uri);
            cancellation.ThrowIfCancellationRequested();
            if (uri.Scheme == "file") return File.OpenRead(uri.LocalPath);
            // Check each hop before requesting it; never follow an HTTPS-to-HTTP redirect.
            for (int hop = 0; hop < 6; hop++)
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                request.Timeout = ManifestTimeoutMilliseconds;
                request.ReadWriteTimeout = ManifestTimeoutMilliseconds;
                request.AllowAutoRedirect = false;
                request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
                request.UserAgent = "FourJTooLs-Updater/" +
                    typeof(UpdateService).Assembly.GetName().Version.ToString(4);
                CancellationTokenRegistration registration = cancellation.Register(request.Abort);
                HttpWebResponse response = null;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    int code = (int)response.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                    {
                        string location = response.Headers["Location"];
                        Uri next;
                        if (String.IsNullOrEmpty(location) ||
                            !Uri.TryCreate(uri, location, out next) || next.Scheme != "https")
                            throw new UpdateException("已拒绝非 HTTPS 重定向。");
                        RequireSource(next);
                        uri = next;
                        response.Dispose();
                        registration.Dispose();
                        continue;
                    }
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new UpdateException("服务器未返回完整安装资源。");
                    return new OwnedResponseStream(response, registration);
                }
                catch (WebException error)
                {
                    if (error.Response != null) error.Response.Dispose();
                    if (response != null) response.Dispose();
                    registration.Dispose();
                    throw;
                }
                catch
                {
                    if (response != null) response.Dispose();
                    registration.Dispose();
                    throw;
                }
            }
            throw new UpdateException("更新服务重定向次数过多。");
        }

        private static void CopyBounded(Stream input, Stream output, long limit,
            CancellationToken cancellation, Action<long, long> progress, long total)
        {
            byte[] buffer = new byte[81920];
            long copied = 0;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                int read = input.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                copied += read;
                if (copied > limit) throw new UpdateException("下载内容超过清单大小或允许上限。");
                output.Write(buffer, 0, read);
                if (progress != null) progress(copied, total);
            }
            cancellation.ThrowIfCancellationRequested();
        }

        private static bool Recoverable(Exception error)
        {
            return error is UpdateException || error is WebException ||
                error is IOException || error is OperationCanceledException;
        }
        private static void Report(Action<string> status, string text)
        {
            if (status != null) status(text);
        }
        private static void VerifyDownloadedFile(UpdateManifest manifest, string path)
        {
            if (manifest.SizeBytes >= 0 && new FileInfo(path).Length != manifest.SizeBytes)
                throw new UpdateException("下载文件大小与清单不符。");
            using (SHA256 hash = SHA256.Create())
            using (Stream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                if (!String.Equals(actual, manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new UpdateException("SHA-256 校验失败，安装包不可使用。");
            }
        }
        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private sealed class OwnedResponseStream : Stream
        {
            private readonly WebResponse _response;
            private readonly Stream _stream;
            private readonly CancellationTokenRegistration _registration;
            public OwnedResponseStream(WebResponse response, CancellationTokenRegistration registration)
            { _response = response; _stream = response.GetResponseStream(); _registration = registration; }
            public override bool CanRead { get { return _stream.CanRead; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count) { return _stream.Read(buffer, offset, count); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing)
            {
                if (disposing) { _registration.Dispose(); _stream.Dispose(); _response.Dispose(); }
                base.Dispose(disposing);
            }
        }
    }
}
