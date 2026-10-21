using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace StructuralToolbox.DrawingTranslation.Api
{
    internal enum TranslationWorkflow
    {
        ConfirmBeforeReplace,
        DirectReplace
    }

    internal enum TranslationProxyMode
    {
        Direct,
        System,
        Custom
    }

    internal sealed class TranslationApiSettings
    {
        internal string Endpoint { get; set; }
        internal string Model { get; set; }
        internal string TargetLanguage { get; set; }
        internal string ApiKey { get; set; }
        internal TranslationWorkflow Workflow { get; set; }
        internal TranslationProxyMode ProxyMode { get; set; }
        internal string ProxyAddress { get; set; }
        internal string Glossary { get; set; }
        internal bool EnableCache { get; set; }
        internal int CacheDays { get; set; }

        internal static TranslationApiSettings Default()
        {
            return new TranslationApiSettings {
                Endpoint = "https://api.openai.com/v1/chat/completions",
                TargetLanguage = "English", Model = String.Empty,
                ApiKey = String.Empty,
                Workflow = TranslationWorkflow.ConfirmBeforeReplace,
                ProxyMode = TranslationProxyMode.Direct,
                ProxyAddress = String.Empty,
                Glossary = String.Empty,
                EnableCache = false,
                CacheDays = 30
            };
        }

        internal void Validate()
        {
            ValidateConnection();
            if (String.IsNullOrWhiteSpace(Model))
                throw new InvalidOperationException("请先获取并选择模型。");
            if (String.IsNullOrWhiteSpace(TargetLanguage))
                throw new InvalidOperationException("请填写目标语言。");
            // 旧版配置对象没有该字段，按默认 30 天兼容迁移。
            if (CacheDays <= 0)
                CacheDays = 30;
            if (CacheDays > 365)
                throw new InvalidOperationException("译文缓存有效期须为 1-365 天。");
        }

        internal void ValidateConnection()
        {
            Uri address;
            if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out address) ||
                (address.Scheme != Uri.UriSchemeHttps &&
                 !(address.Scheme == Uri.UriSchemeHttp && address.IsLoopback)))
                throw new InvalidOperationException(
                    "API 地址须为 HTTPS；本机服务可使用 HTTP。请填写完整的 chat/completions 地址。");
            if (ProxyMode == TranslationProxyMode.Custom)
            {
                Uri proxy;
                if (!Uri.TryCreate((ProxyAddress ?? String.Empty).Trim(),
                        UriKind.Absolute, out proxy) ||
                    (proxy.Scheme != Uri.UriSchemeHttp &&
                     proxy.Scheme != Uri.UriSchemeHttps))
                    throw new InvalidOperationException(
                        "自定义代理须填写 http:// 或 https:// 开头的地址，例如 http://127.0.0.1:7897。");
            }
        }

        internal Uri ChatCompletionsUri()
        {
            ValidateConnection();
            Uri address = new Uri(Endpoint.Trim());
            string path = address.AbsolutePath.TrimEnd('/');
            if (path.Length == 0)
                path = "/v1";
            if (path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                UriBuilder builder = new UriBuilder(address);
                builder.Path = path + "/chat/completions";
                return builder.Uri;
            }
            return address;
        }
    }

    internal static class TranslationHttp
    {
        internal static void EnsureModernTls()
        {
            try {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            } catch (NotSupportedException) { }
        }

        internal static HttpClient CreateClient(TranslationApiSettings settings,
            TimeSpan timeout)
        {
            return CreateClient(settings, timeout, settings.ProxyMode);
        }

        internal static HttpClient CreateClient(TranslationApiSettings settings,
            TimeSpan timeout, TranslationProxyMode mode)
        {
            EnsureModernTls();
            HttpClient client = new HttpClient(CreateHandler(settings, mode));
            client.Timeout = timeout;
            return client;
        }

        private static HttpClientHandler CreateHandler(
            TranslationApiSettings settings, TranslationProxyMode mode)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip |
                DecompressionMethods.Deflate;
            if (mode == TranslationProxyMode.Direct)
            {
                handler.UseProxy = false;
                return handler;
            }
            if (mode == TranslationProxyMode.Custom)
            {
                handler.UseProxy = true;
                handler.Proxy = new WebProxy(settings.ProxyAddress.Trim());
                return handler;
            }
            handler.UseProxy = true;
            return handler;
        }
    }

    internal sealed class TranslationApiSettingsStore
    {
        private readonly string _path;

        internal TranslationApiSettingsStore(string path)
        {
            _path = path;
        }

        internal static TranslationApiSettingsStore ForCurrentUser()
        {
            return new TranslationApiSettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FourSeasonsPVToolbox", "DrawingTranslationApi.json"));
        }

        internal TranslationApiSettings Load()
        {
            if (!File.Exists(_path))
                return TranslationApiSettings.Default();
            StoredSettings stored = new JavaScriptSerializer().Deserialize<StoredSettings>(
                File.ReadAllText(_path, Encoding.UTF8));
            if (stored == null)
                throw new InvalidOperationException("API 配置文件无效。");
            TranslationApiSettings settings = TranslationApiSettings.Default();
            settings.Endpoint = stored.Endpoint ?? settings.Endpoint;
            settings.Model = stored.Model ?? String.Empty;
            settings.TargetLanguage = stored.TargetLanguage ?? settings.TargetLanguage;
            TranslationWorkflow workflow;
            if (Enum.TryParse(stored.Workflow, out workflow) &&
                Enum.IsDefined(typeof(TranslationWorkflow), workflow))
                settings.Workflow = workflow;
            TranslationProxyMode proxyMode;
            if (Enum.TryParse(stored.ProxyMode, out proxyMode) &&
                Enum.IsDefined(typeof(TranslationProxyMode), proxyMode))
                settings.ProxyMode = proxyMode;
            settings.ProxyAddress = stored.ProxyAddress ?? String.Empty;
            settings.Glossary = stored.Glossary ?? String.Empty;
            settings.EnableCache = stored.EnableCache;
            if (stored.CacheDays > 0)
                settings.CacheDays = stored.CacheDays;
            if (!String.IsNullOrEmpty(stored.ProtectedKey))
            {
                byte[] encrypted = Convert.FromBase64String(stored.ProtectedKey);
                byte[] plain = ProtectedData.Unprotect(encrypted, null,
                    DataProtectionScope.CurrentUser);
                settings.ApiKey = Encoding.UTF8.GetString(plain);
                Array.Clear(plain, 0, plain.Length);
            }
            return settings;
        }

        internal void Save(TranslationApiSettings settings)
        {
            settings.Validate();
            string protectedKey = String.Empty;
            if (!String.IsNullOrEmpty(settings.ApiKey))
            {
                byte[] plain = Encoding.UTF8.GetBytes(settings.ApiKey);
                try {
                    protectedKey = Convert.ToBase64String(ProtectedData.Protect(
                        plain, null, DataProtectionScope.CurrentUser));
                } finally {
                    Array.Clear(plain, 0, plain.Length);
                }
            }
            StoredSettings stored = new StoredSettings {
                Endpoint = settings.Endpoint.Trim(), Model = settings.Model.Trim(),
                TargetLanguage = settings.TargetLanguage.Trim(),
                ProtectedKey = protectedKey,
                Workflow = settings.Workflow.ToString(),
                ProxyMode = settings.ProxyMode.ToString(),
                ProxyAddress = (settings.ProxyAddress ?? String.Empty).Trim(),
                Glossary = settings.Glossary ?? String.Empty,
                EnableCache = settings.EnableCache,
                CacheDays = settings.CacheDays
            };
            string directory = Path.GetDirectoryName(_path);
            Directory.CreateDirectory(directory);
            string temporary = _path + ".tmp";
            File.WriteAllText(temporary,
                new JavaScriptSerializer().Serialize(stored), Encoding.UTF8);
            if (File.Exists(_path))
                File.Replace(temporary, _path, null);
            else
                File.Move(temporary, _path);
        }

        public sealed class StoredSettings
        {
            public string Endpoint { get; set; }
            public string Model { get; set; }
            public string TargetLanguage { get; set; }
            public string ProtectedKey { get; set; }
            public string Workflow { get; set; }
            public string ProxyMode { get; set; }
            public string ProxyAddress { get; set; }
            public string Glossary { get; set; }
            public bool EnableCache { get; set; }
            public int CacheDays { get; set; }
        }
    }

    internal sealed class OpenAiCompatibleTranslator : IDisposable
    {
        private readonly HttpClient _client;
        private readonly bool _ownsClient;
        private readonly Func<HttpClient> _fallbackClientFactory;
        private readonly string _fallbackDescription;

        internal OpenAiCompatibleTranslator(HttpClient client)
            : this(client, false, null, String.Empty) { }

        internal OpenAiCompatibleTranslator(HttpClient client,
            Func<HttpClient> fallbackClientFactory)
            : this(client, false, fallbackClientFactory, "备用连接") { }

        private OpenAiCompatibleTranslator(HttpClient client, bool ownsClient,
            Func<HttpClient> fallbackClientFactory, string fallbackDescription)
        {
            _client = client;
            _ownsClient = ownsClient;
            _fallbackClientFactory = fallbackClientFactory;
            _fallbackDescription = fallbackDescription;
        }

        internal static OpenAiCompatibleTranslator Create(
            TranslationApiSettings settings, TimeSpan timeout)
        {
            TranslationProxyMode fallbackMode =
                settings.ProxyMode == TranslationProxyMode.Direct
                    ? TranslationProxyMode.System : TranslationProxyMode.Direct;
            HttpClient client = TranslationHttp.CreateClient(settings, timeout);
            Func<HttpClient> fallback = delegate {
                return TranslationHttp.CreateClient(settings, timeout,
                    fallbackMode);
            };
            return new OpenAiCompatibleTranslator(client, true, fallback,
                fallbackMode == TranslationProxyMode.System
                    ? "系统代理" : "直连");
        }

        public void Dispose()
        {
            if (_ownsClient)
                _client.Dispose();
        }

        private const int MaximumTextsPerRequest = 40;
        private const int MaximumSplitAttempts = 32;
        private const string SystemPrompt =
            "Detect each source language and translate every text to the requested target language. " +
            "Return only a JSON array of translated strings in the same order and count. " +
            "Preserve every __STPV_TOKEN_0000__-style marker exactly, in its original order, " +
            "without translating or duplicating it. Preserve numbers and engineering meaning.";
        private int _splitAttempts;

        // 大图一次选中很多文字时，单个请求既容易被网关截断，也会让一次偶发
        // 失败拖垮整批；这里按上限分批，并在响应不可用时拆半重试。
        internal async Task<IList<string>> TranslateAsync(
            TranslationApiSettings settings, IList<string> texts,
            CancellationToken cancellation)
        {
            return await TranslateAsync(settings, texts, cancellation, null)
                .ConfigureAwait(false);
        }

        internal async Task<IList<string>> TranslateAsync(
            TranslationApiSettings settings, IList<string> texts,
            CancellationToken cancellation, Action<TranslationProgress> progress)
        {
            settings.Validate();
            if (texts == null || texts.Count == 0)
                throw new InvalidOperationException("没有需要翻译的文字。");
            TranslationHttp.EnsureModernTls();
            _splitAttempts = 0;
            List<string> result = new List<string>(texts.Count);
            for (int empty = 0; empty < texts.Count; empty++)
                result.Add(String.Empty);
            TranslationCache cache = settings.EnableCache
                ? TranslationCache.ForCurrentUser() : null;
            int completed = 0;
            int requested = 0;
            int cached = 0;
            if (progress != null)
                progress(new TranslationProgress(0, texts.Count, 0, 0,
                    "准备翻译"));
            int index = 0;
            while (index < texts.Count)
            {
                cancellation.ThrowIfCancellationRequested();
                List<int> positions = new List<int>(MaximumTextsPerRequest);
                List<string> chunk = new List<string>(MaximumTextsPerRequest);
                while (index < texts.Count && positions.Count <
                    MaximumTextsPerRequest)
                {
                    string cachedText;
                    if (cache != null && cache.TryGet(settings,
                        texts[index], out cachedText))
                    {
                        result[index] = cachedText;
                        cached++;
                        completed++;
                    }
                    else
                    {
                        positions.Add(index);
                        chunk.Add(texts[index]);
                    }
                    index++;
                }
                if (positions.Count == 0)
                {
                    if (progress != null)
                        progress(new TranslationProgress(completed,
                            texts.Count, requested, cached, "缓存命中"));
                    continue;
                }
                int size = chunk.Count;
                cancellation.ThrowIfCancellationRequested();
                if (progress != null)
                    progress(new TranslationProgress(completed, texts.Count,
                        requested, cached, "请求翻译"));
                IList<string> translated = await TranslateChunkAsync(settings,
                    chunk, cancellation, true).ConfigureAwait(false);
                if (translated == null || translated.Count != size)
                    throw new InvalidOperationException(
                        "翻译服务返回的条目数量与请求不一致，图纸未修改。");
                requested += size;
                for (int offset = 0; offset < translated.Count; offset++)
                {
                    int position = positions[offset];
                    result[position] = translated[offset];
                    if (cache != null)
                        cache.Put(settings, texts[position], translated[offset]);
                    completed++;
                }
                if (progress != null)
                    progress(new TranslationProgress(completed, texts.Count,
                        requested, cached, "完成一批"));
            }
            return result;
        }

        private async Task<IList<string>> TranslateChunkAsync(
            TranslationApiSettings settings, IList<string> texts,
            CancellationToken cancellation, bool allowRepeat)
        {
            // C# 5 不允许在 catch 里 await，这里先记录失败再统一处理。
            TranslationResponseException problem = null;
            try
            {
                return await TranslateOnceAsync(settings, texts, cancellation)
                    .ConfigureAwait(false);
            }
            catch (TranslationResponseException failure)
            {
                problem = failure;
            }
            // 网关偶发返回空响应或半截响应时先原样重试一次，
            // 仍然失败就拆成两半分别请求，避免整批作废。
            if (allowRepeat)
                return await TranslateChunkAsync(settings, texts, cancellation,
                    false).ConfigureAwait(false);
            if (texts.Count == 1 || _splitAttempts >= MaximumSplitAttempts)
                throw new InvalidOperationException(problem.Message);
            _splitAttempts++;
            int half = texts.Count / 2;
            List<string> left = new List<string>(half);
            List<string> right = new List<string>(texts.Count - half);
            for (int position = 0; position < texts.Count; position++)
            {
                if (position < half)
                    left.Add(texts[position]);
                else
                    right.Add(texts[position]);
            }
            IList<string> first = await TranslateChunkAsync(settings, left,
                cancellation, true).ConfigureAwait(false);
            IList<string> second = await TranslateChunkAsync(settings, right,
                cancellation, true).ConfigureAwait(false);
            List<string> merged = new List<string>(first.Count + second.Count);
            merged.AddRange(first);
            merged.AddRange(second);
            return merged;
        }
        private async Task<IList<string>> TranslateOnceAsync(
            TranslationApiSettings settings, IList<string> texts,
            CancellationToken cancellation)
        {
            JavaScriptSerializer serializer = CreateSerializer();
            string requestBody = BuildRequestBody(serializer, settings, texts);
            Uri uri = settings.ChatCompletionsUri();
            ChatResponse response = null;
            HttpRequestException transportFailure = null;
            try
            {
                response = await SendAsync(_client, uri, requestBody,
                    settings.ApiKey, cancellation).ConfigureAwait(false);
            }
            catch (HttpRequestException failure)
            {
                transportFailure = failure;
            }
            catch (OperationCanceledException)
            {
                if (cancellation.IsCancellationRequested)
                    throw;
                throw new InvalidOperationException(
                    "等待翻译服务响应超时，图纸未修改。可稍后重试或改用响应更快的模型。");
            }
            HttpClient activeClient = _client;
            if (transportFailure != null)
            {
                TransportAttempt attempt;
                try
                {
                    attempt = await RetryWithoutCurrentTransportAsync(settings,
                        uri, requestBody, transportFailure, cancellation).
                        ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellation.IsCancellationRequested)
                        throw;
                    throw new InvalidOperationException(
                        "等待翻译服务响应超时，图纸未修改。可稍后重试或改用响应更快的模型。");
                }
                response = attempt.Response;
                if (attempt.Client != null)
                    activeClient = attempt.Client;
            }
            try
            {
                if (!response.IsSuccess)
                {
                    string server = DescribeErrorPayload(response.Body);
                    IList<string> available = null;
                    int status = (int)response.StatusCode;
                    if (status == 400 || status == 404 || status == 503)
                        available = await GetAvailableModelsAsync(activeClient,
                            settings, cancellation).ConfigureAwait(false);
                    throw new InvalidOperationException(BuildStatusMessage(
                        settings, status, server, available));
                }
                string problem;
                string content = ReadContent(response, serializer, out problem);
                if (content == null)
                {
                    string hint = await DescribeKeyStatusAsync(activeClient,
                        settings, cancellation).ConfigureAwait(false);
                    throw new TranslationResponseException(
                        BuildResponseMessage(response, problem, hint));
                }
                if (String.IsNullOrWhiteSpace(content))
                    throw new TranslationResponseException(
                        BuildResponseMessage(response,
                            "响应里没有可用的文字结果", String.Empty));
                return ParseTranslations(content, texts.Count, serializer);
            }
            finally
            {
                if (!Object.ReferenceEquals(activeClient, _client))
                    activeClient.Dispose();
            }
        }

        private static string BuildRequestBody(JavaScriptSerializer serializer,
            TranslationApiSettings settings, IList<string> texts)
        {
            string glossary = settings.Glossary == null ? String.Empty :
                settings.Glossary.Trim();
            string prompt = SystemPrompt;
            if (glossary.Length > 0)
                prompt += " Apply these glossary mappings exactly when the source " +
                    "contains the left term (one mapping per line, source=target):\n" +
                    glossary;
            return serializer.Serialize(new {
                model = settings.Model.Trim(),
                stream = false,
                messages = new object[] {
                    new { role = "system", content = prompt },
                    new { role = "user", content = serializer.Serialize(new {
                        target_language = settings.TargetLanguage.Trim(),
                        texts = texts
                    }) }
                }
            });
        }
        private async Task<TransportAttempt> RetryWithoutCurrentTransportAsync(
            TranslationApiSettings settings, Uri uri, string requestBody,
            HttpRequestException failure, CancellationToken cancellation)
        {
            if (_fallbackClientFactory == null)
                throw BuildTransportFailure(settings, failure, false);
            HttpClient fallback;
            try { fallback = _fallbackClientFactory(); }
            catch (Exception) { throw BuildTransportFailure(settings, failure, false); }
            if (fallback == null)
                throw BuildTransportFailure(settings, failure, false);
            try
            {
                TransportAttempt attempt = new TransportAttempt();
                attempt.Client = fallback;
                attempt.Response = await SendAsync(fallback, uri, requestBody,
                    settings.ApiKey, cancellation).ConfigureAwait(false);
                return attempt;
            }
            catch (HttpRequestException) { }
            fallback.Dispose();
            throw BuildTransportFailure(settings, failure, true);
        }

        private static async Task<ChatResponse> SendAsync(HttpClient client,
            Uri uri, string requestBody, string apiKey,
            CancellationToken cancellation)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Post, uri))
            {
                request.Content = new StringContent(requestBody, Encoding.UTF8,
                    "application/json");
                if (!String.IsNullOrEmpty(apiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue(
                        "Bearer", apiKey);
                using (HttpResponseMessage response = await client.SendAsync(
                    request, cancellation).ConfigureAwait(false))
                {
                    ChatResponse result = new ChatResponse();
                    result.StatusCode = response.StatusCode;
                    result.MediaType = response.Content.Headers.ContentType == null
                        ? String.Empty
                        : response.Content.Headers.ContentType.MediaType;
                    result.Body = await response.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                    return result;
                }
            }
        }

        private InvalidOperationException BuildTransportFailure(
            TranslationApiSettings settings, Exception failure, bool retried)
        {
            string detail = DescribeTransportFailure(failure);
            string advice = settings.ProxyMode == TranslationProxyMode.Direct
                ? "当前为直连。若本机需要经代理访问该接口，请在 API 设置中改用系统代理或自定义代理。"
                : "当前使用" + ModeName(settings.ProxyMode) +
                  "；已自动改用" + _fallbackDescription + "重试仍失败。若本机代理软件已关闭或异常，请在 API 设置中改为直连。";
            if (!retried && _fallbackClientFactory == null)
                advice = "请检查网络、接口地址和代理设置。";
            return new InvalidOperationException(
                "无法连接翻译服务：" + detail + advice + " 图纸未修改。");
        }

        private static string ModeName(TranslationProxyMode mode)
        {
            if (mode == TranslationProxyMode.System)
                return "系统代理";
            if (mode == TranslationProxyMode.Custom)
                return "自定义代理";
            return "直连";
        }

        private static string DescribeTransportFailure(Exception failure)
        {
            string outer = failure.Message;
            Exception inner = failure;
            string deepest = outer;
            while (inner != null)
            {
                if (!String.IsNullOrWhiteSpace(inner.Message))
                    deepest = inner.Message.Trim();
                inner = inner.InnerException;
            }
            string detail = outer;
            if (!String.Equals(deepest, outer, StringComparison.Ordinal))
                detail = outer + "（" + deepest + "）";
            if (!detail.Trim().EndsWith("。", StringComparison.Ordinal))
                detail = detail.Trim() + "。";
            return detail;
        }

        private static string BuildStatusMessage(TranslationApiSettings settings,
            int status, string server, IList<string> available)
        {
            StringBuilder message = new StringBuilder();
            message.Append("翻译服务返回 HTTP ").Append(status);
            if (!String.IsNullOrWhiteSpace(server))
                message.Append("：").Append(server);
            message.Append("。");
            if (available != null && available.Count > 0 &&
                !ContainsModel(available, settings.Model))
            {
                message.Append("当前密钥可用的模型为 ")
                    .Append(String.Join("、", FirstModels(available)))
                    .Append("，不包含 ").Append(settings.Model.Trim())
                    .Append("。");
                if (ContainsImageOnly(available))
                    message.Append("该密钥下没有文本模型，无法用于文字翻译，请在 API 设置中改用具备文本模型权限的密钥或服务。");
                else
                    message.Append("请在 API 设置中改用可用的文本模型或密钥。");
            }
            else
                message.Append("请检查接口地址、模型名称和密钥。");
            message.Append(" 图纸未修改。");
            return message.ToString();
        }

        private static bool ContainsImageOnly(IList<string> models)
        {
            foreach (string model in models)
                if (model.IndexOf("image", StringComparison.OrdinalIgnoreCase) < 0)
                    return false;
            return true;
        }

        private static string DescribeErrorPayload(string body)
        {
            if (String.IsNullOrWhiteSpace(body))
                return String.Empty;
            string value = body.Trim();
            if (value.StartsWith("<", StringComparison.Ordinal))
                return "接口返回了网页内容";
            if (value[0] != '{' && value[0] != '[')
                return Truncate(value, 120);
            try
            {
                IDictionary root = CreateSerializer().
                    DeserializeObject(value) as IDictionary;
                if (root == null)
                    return String.Empty;
                object error = root["error"];
                IDictionary details = error as IDictionary;
                if (details != null)
                {
                    string message = details["message"] as string;
                    if (!String.IsNullOrWhiteSpace(message))
                        return Truncate(message.Trim(), 200);
                }
                string single = error as string;
                if (!String.IsNullOrWhiteSpace(single))
                    return Truncate(single.Trim(), 200);
                string fallback = root["message"] as string;
                if (!String.IsNullOrWhiteSpace(fallback))
                    return Truncate(fallback.Trim(), 200);
                return String.Empty;
            }
            catch (ArgumentException) { return String.Empty; }
        }

        // 部分中转网关会把标准 chat.completion 再包一层，例如
        // {"data":{"choices":[...]},"success":true}。
        private static IList ResolveChoices(IDictionary root)
        {
            if (root == null)
                return null;
            IList choices = NotEmpty(root["choices"] as IList);
            if (choices != null)
                return choices;
            IDictionary payload = root["data"] as IDictionary;
            if (payload != null)
            {
                choices = NotEmpty(payload["choices"] as IList);
                if (choices != null)
                    return choices;
            }
            IList payloads = root["data"] as IList;
            if (payloads != null)
            {
                foreach (object item in payloads)
                {
                    IDictionary entry = item as IDictionary;
                    if (entry == null)
                        continue;
                    choices = NotEmpty(entry["choices"] as IList);
                    if (choices != null)
                        return choices;
                }
            }
            return null;
        }

        private static IList NotEmpty(IList list)
        {
            return list == null || list.Count == 0 ? null : list;
        }

        private static string Truncate(string value, int maximum)
        {
            if (value.Length <= maximum)
                return value;
            return value.Substring(0, maximum) + "...";
        }

        private async Task<IList<string>> GetAvailableModelsAsync(
            HttpClient client, TranslationApiSettings settings,
            CancellationToken cancellation)
        {
            try
            {
                return await RequestModelsAsync(client, settings, cancellation)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (cancellation.IsCancellationRequested)
                    throw;
                return null;
            }
            catch (HttpRequestException) { return null; }
            catch (InvalidOperationException) { return null; }
            catch (ArgumentException) { return null; }
        }

        internal async Task<IList<string>> ListModelsAsync(
            TranslationApiSettings settings, CancellationToken cancellation)
        {
            settings.ValidateConnection();
            TranslationHttp.EnsureModernTls();
            HttpRequestException primaryFailure = null;
            try
            {
                return await RequestModelsAsync(_client, settings, cancellation)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException failure)
            {
                primaryFailure = failure;
            }
            if (_fallbackClientFactory == null)
                throw BuildTransportFailure(settings, primaryFailure, false);
            HttpClient fallback = null;
            try { fallback = _fallbackClientFactory(); }
            catch (Exception) { fallback = null; }
            if (fallback == null)
                throw BuildTransportFailure(settings, primaryFailure, false);
            try
            {
                return await RequestModelsAsync(fallback, settings, cancellation)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                throw BuildTransportFailure(settings, primaryFailure, true);
            }
            finally { fallback.Dispose(); }
        }

        private static async Task<IList<string>> RequestModelsAsync(
            HttpClient client, TranslationApiSettings settings,
            CancellationToken cancellation)
        {
            Uri target = ModelsUri(settings);
            using (HttpRequestMessage request = new HttpRequestMessage(
                HttpMethod.Get, target))
            {
                if (!String.IsNullOrEmpty(settings.ApiKey))
                    request.Headers.Authorization = new AuthenticationHeaderValue(
                        "Bearer", settings.ApiKey);
                using (HttpResponseMessage response = await client.SendAsync(
                    request, cancellation).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync()
                        .ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException(String.Format(
                            "获取模型列表返回 HTTP {0}：{1}", (int)response.StatusCode,
                            DescribeErrorPayload(body)));
                    IList<string> models;
                    try { models = ExtractModels(body); }
                    catch (ArgumentException)
                    {
                        throw new InvalidOperationException(
                            "模型列表不是有效 JSON：" + Truncate(body.Trim(), 120));
                    }
                    if (models == null || models.Count == 0)
                        throw new InvalidOperationException(
                            "该地址没有返回可用的模型列表。");
                    return models;
                }
            }
        }

        private static Uri ModelsUri(TranslationApiSettings settings)
        {
            Uri endpoint = settings.ChatCompletionsUri();
            const string suffix = "/chat/completions";
            UriBuilder builder = new UriBuilder(endpoint);
            string path = endpoint.AbsolutePath;
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                builder.Path = path.Substring(0,
                    path.Length - suffix.Length) + "/models";
            else if (path.Length == 0 || path == "/")
                builder.Path = "/v1/models";
            else
                builder.Path = path.TrimEnd('/') + "/models";
            return builder.Uri;
        }

        private static IList<string> ExtractModels(string body)
        {
            if (String.IsNullOrWhiteSpace(body))
                return null;
            IDictionary root = CreateSerializer().
                DeserializeObject(body) as IDictionary;
            IList data = root == null ? null : root["data"] as IList;
            if (data == null)
                return null;
            List<string> models = new List<string>();
            foreach (object item in data)
            {
                IDictionary entry = item as IDictionary;
                string id = entry == null ? null : entry["id"] as string;
                if (!String.IsNullOrWhiteSpace(id))
                    models.Add(id);
            }
            return models;
        }

        private static bool ContainsModel(IList<string> models, string model)
        {
            foreach (string candidate in models)
                if (String.Equals(candidate, model,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string[] FirstModels(IList<string> models)
        {
            int count = Math.Min(3, models.Count);
            string[] first = new string[count];
            for (int index = 0; index < count; index++)
                first[index] = models[index];
            return first;
        }

        private static IList<string> ParseTranslations(string content, int expected,
            JavaScriptSerializer serializer)
        {
            string value = content.Trim();
            if (value.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLine = value.IndexOf('\n');
                int closing = value.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine < 0 || closing <= firstLine ||
                    value.Substring(closing + 3).Trim().Length != 0)
                    throw new TranslationContentException(
                        "翻译结果格式无效，图纸未修改。");
                value = value.Substring(firstLine + 1,
                    closing - firstLine - 1).Trim();
            }

            if (value.Length == 0)
                throw new TranslationContentException("翻译结果为空，图纸未修改。");
            char first = value[0];
            if (first != '[' && first != '{' && first != '"')
            {
                if (expected == 1)
                    return new List<string> { value };
                throw new TranslationContentException(
                    "批量翻译结果必须是 JSON 数组，图纸未修改。");
            }

            object parsed;
            try
            {
                parsed = first == '"'
                    ? ((IList)serializer.DeserializeObject("[" + value + "]"))[0]
                    : serializer.DeserializeObject(value);
            }
            catch (ArgumentException)
            {
                throw new TranslationResponseException(
                    "翻译结果不是有效 JSON，图纸未修改。");
            }
            IDictionary map = parsed as IDictionary;
            if (map != null)
                parsed = map["translations"] ?? map["translation"] ?? map["text"];
            string single = parsed as string;
            if (single != null)
            {
                if (expected != 1 || String.IsNullOrWhiteSpace(single))
                    throw new TranslationContentException(
                        "翻译结果数量不匹配或为空，图纸未修改。");
                return new List<string> { single };
            }
            IList translated = parsed as IList;
            if (translated == null || translated.Count != expected)
                throw new TranslationContentException(
                    "翻译结果数量不匹配，图纸未修改。");
            List<string> result = new List<string>();
            foreach (object item in translated)
            {
                string text = item as string;
                if (String.IsNullOrWhiteSpace(text))
                    throw new TranslationContentException(
                        "翻译结果包含空文字，图纸未修改。");
                result.Add(text);
            }
            return result;
        }

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            // 大图批量的响应可能超过默认 2 MB 解析上限，超限会抛
            // ArgumentException 并被误报成“不是有效 JSON”。
            serializer.MaxJsonLength = Int32.MaxValue;
            return serializer;
        }

        private static string BuildResponseMessage(ChatResponse response,
            string problem, string hint)
        {
            StringBuilder message = new StringBuilder();
            message.Append("翻译服务响应无法使用：").Append(problem).Append("。HTTP ")
                .Append((int)response.StatusCode);
            if (!String.IsNullOrEmpty(response.MediaType))
                message.Append("，Content-Type ").Append(response.MediaType);
            string body = response.Body ?? String.Empty;
            message.Append("，响应长度 ").Append(body.Length).Append(" 字符。");
            string snippet = Snippet(body, 180);
            if (snippet.Length > 0)
                message.Append(" 响应开头：").Append(snippet);
            string server = DescribeErrorPayload(body);
            if (!String.IsNullOrWhiteSpace(server))
                message.Append(" 服务端说明：").Append(server);
            if (!String.IsNullOrWhiteSpace(hint))
                message.Append(" ").Append(hint);
            message.Append(" 图纸未修改。");
            return message.ToString();
        }

        // 响应内容已经无法解析时，用同一个密钥读一次模型列表，
        // 便于区分“密钥或模型不对”和“网关本次响应异常”。
        private async Task<string> DescribeKeyStatusAsync(HttpClient client,
            TranslationApiSettings settings, CancellationToken cancellation)
        {
            IList<string> models;
            try
            {
                models = await RequestModelsAsync(client, settings, cancellation)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (cancellation.IsCancellationRequested)
                    throw;
                return String.Empty;
            }
            catch (HttpRequestException) { return String.Empty; }
            catch (InvalidOperationException) { return String.Empty; }
            catch (ArgumentException) { return String.Empty; }
            if (models == null || models.Count == 0)
                return String.Empty;
            if (ContainsModel(models, settings.Model))
                return "同一密钥的模型列表可正常读取且包含当前模型，通常属于中转网关的临时响应，可直接重试。";
            return "同一密钥下可用的模型为 " +
                String.Join("、", FirstModels(models)) + "，不包含 " +
                settings.Model.Trim() + "，请在 API 设置中改用可用的文本模型。";
        }

        // 中转网关响应形态不统一：可能包一层 data、顶层是数组、结果放在
        // SSE 分片里或夹带前后缀文本。这里从严格到宽松依次尝试提取 content，
        // 提取结果仍由 ParseTranslations 严格校验数量和格式。
        private static string ReadContent(ChatResponse response,
            JavaScriptSerializer serializer, out string problem)
        {
            problem = String.Empty;
            string body = StripBom(response.Body).Trim();
            if (body.Length == 0)
            {
                problem = "接口返回了空内容，可能是中转网关超时或提前断开";
                return null;
            }
            if (body[0] == '<' || response.MediaType.IndexOf("html",
                StringComparison.OrdinalIgnoreCase) >= 0)
            {
                problem = "接口返回的是网页或其他非 JSON 内容，" +
                    "请确认填写的是服务地址或完整的 /v1/chat/completions 地址";
                return null;
            }
            if (IsEventStream(response, body))
                return ReadEventStream(body, serializer, out problem);
            object parsed;
            if (!TryParseJson(body, serializer, out parsed))
            {
                string candidate = SliceJson(body);
                if (candidate == null ||
                    !TryParseJson(candidate, serializer, out parsed))
                {
                    problem = "响应内容不是有效 JSON";
                    return null;
                }
            }
            return ExtractContent(parsed, out problem);
        }

        private static bool IsEventStream(ChatResponse response, string body)
        {
            if (response.MediaType.IndexOf("event-stream",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            return body.StartsWith("data:", StringComparison.Ordinal) ||
                body.StartsWith("event:", StringComparison.Ordinal);
        }

        private static string ReadEventStream(string body,
            JavaScriptSerializer serializer, out string problem)
        {
            problem = String.Empty;
            StringBuilder builder = new StringBuilder();
            foreach (string line in body.Split('\n'))
            {
                string value = line.Trim();
                if (!value.StartsWith("data:", StringComparison.Ordinal))
                    continue;
                string payload = value.Substring("data:".Length).Trim();
                if (payload.Length == 0 ||
                    String.Equals(payload, "[DONE]", StringComparison.Ordinal))
                    continue;
                object item;
                if (!TryParseJson(payload, serializer, out item))
                    continue;
                string piece = ExtractContent(item, out problem);
                if (piece != null)
                    builder.Append(piece);
            }
            if (builder.Length > 0)
                return builder.ToString();
            problem = "流式响应中没有找到可用的文字结果";
            return null;
        }

        private static string ExtractContent(object parsed, out string problem)
        {
            problem = String.Empty;
            string content;
            if (TryFindContent(parsed, 0, out content))
                return content;
            problem = "响应里没有 choices 或可用的文字内容";
            return null;
        }

        private static bool TryFindContent(object parsed, int depth,
            out string content)
        {
            content = null;
            if (parsed == null || depth > 6)
                return false;
            IDictionary map = parsed as IDictionary;
            if (map != null)
            {
                IList choices = ResolveChoices(map);
                if (choices != null)
                {
                    foreach (object item in choices)
                    {
                        IDictionary choice = item as IDictionary;
                        if (choice == null)
                            continue;
                        content = ContentOf(choice["message"] as IDictionary);
                        if (String.IsNullOrWhiteSpace(content))
                            content = ContentOf(choice["delta"] as IDictionary);
                        if (String.IsNullOrWhiteSpace(content))
                            content = choice["text"] as string;
                        if (!String.IsNullOrWhiteSpace(content))
                            return true;
                    }
                }
                content = DirectContentOf(map);
                if (!String.IsNullOrWhiteSpace(content))
                    return true;
                return TryFindContent(map["data"], depth + 1, out content);
            }
            IList list = parsed as IList;
            if (list != null)
            {
                foreach (object item in list)
                    if (TryFindContent(item, depth + 1, out content))
                        return true;
            }
            return false;
        }

        private static string DirectContentOf(IDictionary map)
        {
            string content = ContentOf(map);
            if (!String.IsNullOrWhiteSpace(content))
                return content;
            string[] keys = new string[] { "output_text", "translation" };
            foreach (string key in keys)
            {
                string value = map[key] as string;
                if (!String.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        // content 可能是字符串，也可能是 [{ "text": "..." }] 形式的分片。
        private static string ContentOf(IDictionary message)
        {
            if (message == null)
                return null;
            string content = message["content"] as string;
            if (!String.IsNullOrEmpty(content))
                return content;
            IList parts = message["content"] as IList;
            if (parts == null)
                return null;
            StringBuilder builder = new StringBuilder();
            foreach (object part in parts)
            {
                IDictionary map = part as IDictionary;
                string text = map == null ? part as string
                    : map["text"] as string;
                if (!String.IsNullOrEmpty(text))
                    builder.Append(text);
            }
            return builder.Length == 0 ? null : builder.ToString();
        }

        private static bool TryParseJson(string value,
            JavaScriptSerializer serializer, out object parsed)
        {
            parsed = null;
            if (String.IsNullOrWhiteSpace(value))
                return false;
            try
            {
                parsed = serializer.DeserializeObject(value);
                return true;
            }
            catch (ArgumentException) { return false; }
            catch (InvalidOperationException) { return false; }
        }

        // 有些网关会在 JSON 前后插入提示文本，截取第一个完整括号区间再试。
        private static string SliceJson(string body)
        {
            int start = -1;
            for (int index = 0; index < body.Length; index++)
            {
                if (body[index] == '{' || body[index] == '[')
                {
                    start = index;
                    break;
                }
            }
            if (start < 0)
                return null;
            char closing = body[start] == '{' ? '}' : ']';
            int end = body.LastIndexOf(closing);
            if (end <= start)
                return null;
            return body.Substring(start, end - start + 1);
        }

        private static string StripBom(string value)
        {
            if (String.IsNullOrEmpty(value))
                return String.Empty;
            if (value[0] == '\uFEFF')
                return value.Substring(1);
            return value;
        }

        // 错误提示要放进 AutoCAD 消息框，必须去掉换行并限制长度。
        private static string Snippet(string body, int maximum)
        {
            if (String.IsNullOrEmpty(body))
                return String.Empty;
            StringBuilder text = new StringBuilder();
            foreach (char value in body)
            {
                if (text.Length >= maximum)
                {
                    text.Append("...");
                    break;
                }
                if (value == '\r' || value == '\n' || value == '\t')
                    text.Append(' ');
                else if (value < ' ')
                    text.Append('.');
                else
                    text.Append(value);
            }
            return text.ToString().Trim();
        }

        // 数量、结构这类语义错误不能用拆批掩盖，必须原样报给用户，
        // 否则错误结果会被当成网关抖动重试后写进图纸。
        private sealed class TranslationContentException : InvalidOperationException
        {
            internal TranslationContentException(string message) : base(message) { }
        }

        /// <summary>响应本身可用但内容无法解析时使用，允许上层拆分重试。</summary>
        private sealed class TranslationResponseException : InvalidOperationException
        {
            internal TranslationResponseException(string message) : base(message) { }
        }
        private sealed class TransportAttempt
        {
            internal ChatResponse Response { get; set; }
            internal HttpClient Client { get; set; }
        }

        private sealed class ChatResponse
        {
            internal HttpStatusCode StatusCode { get; set; }
            internal string MediaType { get; set; }
            internal string Body { get; set; }

            internal bool IsSuccess
            {
                get { return (int)StatusCode >= 200 && (int)StatusCode < 300; }
            }
        }
    }
}



