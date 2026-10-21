using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using StructuralToolbox.DrawingTranslation.Api;

internal static class DrawingTranslationApiTests
{
    private static int Main()
    {
        try
        {
            CheckSettings();
            CheckCacheAndProgress().GetAwaiter().GetResult();
            CheckEndpointResolution();
            CheckProxySettings();
            CheckTranslation().GetAwaiter().GetResult();
            CheckRelayResponseShapes().GetAwaiter().GetResult();
            CheckTransportFallback().GetAwaiter().GetResult();
            CheckModelList().GetAwaiter().GetResult();
            Console.WriteLine("PASS drawing translation API tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL drawing translation API: " +
                exception.ToString());
            return 1;
        }
    }

    private static void CheckEndpointResolution()
    {
        TranslationApiSettings settings = TranslationApiSettings.Default();
        settings.Model = "test-model";
        settings.Endpoint = "https://cdn.coderelay.cn/";
        Require(settings.ChatCompletionsUri().AbsoluteUri ==
            "https://cdn.coderelay.cn/v1/chat/completions",
            "API root was not normalized to chat completions");
        settings.Endpoint = "https://example.com/v1/";
        Require(settings.ChatCompletionsUri().AbsoluteUri ==
            "https://example.com/v1/chat/completions",
            "API v1 base was not normalized");
        settings.Endpoint = "https://example.com/v1/chat/completions";
        Require(settings.ChatCompletionsUri().AbsoluteUri == settings.Endpoint,
            "complete API endpoint was changed");
    }

    private static void CheckSettings()
    {
        string path = Path.Combine(Path.GetTempPath(),
            "STPV-api-test-" + Guid.NewGuid().ToString("N"), "settings.json");
        try
        {
            TranslationApiSettings settings = TranslationApiSettings.Default();
            settings.Model = "test-model";
            settings.ApiKey = "test-secret-do-not-persist-plain";
            settings.Workflow = TranslationWorkflow.DirectReplace;
            TranslationApiSettingsStore store =
                new TranslationApiSettingsStore(path);
            store.Save(settings);
            string content = File.ReadAllText(path);
            Require(!content.Contains(settings.ApiKey),
                "API key was stored as plaintext");
            TranslationApiSettings loaded = store.Load();
            Require(loaded.ApiKey == settings.ApiKey &&
                loaded.Workflow == TranslationWorkflow.DirectReplace &&
                loaded.Model == settings.Model,
                "API settings roundtrip failed");
            loaded.Glossary = "屋顶=Roof\n支架=Support";
            loaded.EnableCache = true;
            loaded.CacheDays = 14;
            store.Save(loaded);
            TranslationApiSettings advanced = store.Load();
            Require(advanced.Glossary.Contains("屋顶=Roof") &&
                advanced.EnableCache && advanced.CacheDays == 14,
                "glossary and cache settings roundtrip failed");
            loaded.Workflow = TranslationWorkflow.ConfirmBeforeReplace;
            store.Save(loaded);
            Require(store.Load().Workflow ==
                TranslationWorkflow.ConfirmBeforeReplace,
                "API workflow update failed");
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(path)))
                Directory.Delete(Path.GetDirectoryName(path), true);
        }
    }

    private static async Task CheckCacheAndProgress()
    {
        TranslationApiSettings settings = TranslationApiSettings.Default();
        settings.Model = "test-model";
        settings.Glossary = "屋顶=Roof";
        settings.ApiKey = "fake-key";
        List<TranslationProgress> progress = new List<TranslationProgress>();
        using (HttpClient client = new HttpClient(new FakeHandler(
            ResponseFor("[\"Roof\"]"))))
        {
            OpenAiCompatibleTranslator translator =
                new OpenAiCompatibleTranslator(client);
            IList<string> result = await translator.TranslateAsync(settings,
                new List<string> { "屋顶" }, CancellationToken.None,
                delegate(TranslationProgress item) { progress.Add(item); });
            Require(result.Count == 1 && result[0] == "Roof" &&
                progress.Count >= 2 && progress[progress.Count - 1].Completed == 1,
                "translation progress callback did not report completion");
            Require(client != null, "translator test client was not created");
        }
        string path = Path.Combine(Path.GetTempPath(),
            "STPV-cache-test-" + Guid.NewGuid().ToString("N"), "cache.json");
        try
        {
            TranslationCache cache = new TranslationCache(path);
            cache.Put(settings, "屋顶", "Roof");
            string value;
            Require(cache.TryGet(settings, "屋顶", out value) && value == "Roof",
                "translation cache did not roundtrip");
            cache.Clear();
            Require(!cache.TryGet(settings, "屋顶", out value),
                "translation cache clear did not remove the entry");
        }
        finally
        {
            string directory = Path.GetDirectoryName(path);
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static void CheckProxySettings()
    {
        Require(TranslationApiSettings.Default().ProxyMode ==
            TranslationProxyMode.Direct,
            "default proxy mode must be direct");
        string directory = Path.Combine(Path.GetTempPath(),
            "STPV-proxy-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(directory, "settings.json");
        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "{\"Endpoint\":\"https://cdn.coderelay.cn/\"," +
                "\"Model\":\"gpt-5.6-terra\",\"TargetLanguage\":\"English\"," +
                "\"Workflow\":\"DirectReplace\"}", Encoding.UTF8);
            TranslationApiSettingsStore store =
                new TranslationApiSettingsStore(path);
            TranslationApiSettings legacy = store.Load();
            Require(legacy.ProxyMode == TranslationProxyMode.Direct &&
                legacy.Workflow == TranslationWorkflow.DirectReplace,
                "legacy settings without proxy mode must load as direct");
            legacy.ApiKey = "fake-key";
            legacy.ProxyMode = TranslationProxyMode.Custom;
            legacy.ProxyAddress = "127.0.0.1:7897";
            bool rejected = false;
            try { store.Save(legacy); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "custom proxy without scheme was accepted");
            legacy.ProxyAddress = "http://127.0.0.1:7897";
            store.Save(legacy);
            TranslationApiSettings reloaded = store.Load();
            Require(reloaded.ProxyMode == TranslationProxyMode.Custom &&
                reloaded.ProxyAddress == "http://127.0.0.1:7897",
                "proxy settings roundtrip failed");
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    private static async Task CheckTransportFallback()
    {
        TranslationApiSettings settings = TranslationApiSettings.Default();
        settings.Model = "test-model";
        settings.ApiKey = "fake-key";
        settings.ProxyMode = TranslationProxyMode.System;
        int fallbackCalls = 0;
        using (HttpClient client = new HttpClient(new FailingHandler()))
        {
            OpenAiCompatibleTranslator translator =
                new OpenAiCompatibleTranslator(client, delegate {
                    fallbackCalls++;
                    return new HttpClient(new FakeHandler(
                        ResponseFor("[\"Roof\"]")));
                });
            IList<string> result = await translator.TranslateAsync(settings,
                new List<string> { "屋顶" }, CancellationToken.None);
            Require(result.Count == 1 && result[0] == "Roof" &&
                fallbackCalls == 1,
                "transport fallback did not recover a dead proxy");
        }
        using (HttpClient client = new HttpClient(new FailingHandler()))
        {
            bool explained = false;
            try
            {
                await new OpenAiCompatibleTranslator(client, delegate {
                    return new HttpClient(new FailingHandler());
                }).TranslateAsync(settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                explained = exception.Message.Contains("无法连接翻译服务") &&
                    exception.Message.Contains("身份验证失败") &&
                    exception.Message.Contains("图纸未修改");
            }
            Require(explained, "transport failure was not explained");
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            ResponseFor("[\"Roof\"]"))))
        {
            bool mapped = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            }
            catch (Exception exception) { mapped = exception.Message.Length > 0; }
            Require(!mapped, "reachable client must not fall back");
        }
    }
    private static async Task CheckModelList()
    {
        TranslationApiSettings settings = TranslationApiSettings.Default();
        settings.Endpoint = "https://gateway.example.com/";
        settings.Model = "gpt-5.6-terra";
        settings.ApiKey = "fake-key";
        FakeHandler handler = new FakeHandler(
            "{\"data\":[{\"id\":\"gpt-image-2\"}," +
            "{\"id\":\"gpt-image-2-2k\"}]}");
        using (HttpClient client = new HttpClient(handler))
        {
            IList<string> models = await new OpenAiCompatibleTranslator(client)
                .ListModelsAsync(settings, CancellationToken.None);
            Require(models.Count == 2 && models[0] == "gpt-image-2" &&
                models[1] == "gpt-image-2-2k",
                "model list was not parsed");
            Require(handler.Method == "GET" &&
                handler.Authorization == "Bearer fake-key",
                "model list request was malformed");
        }
        using (HttpClient client = new HttpClient(new FailingHandler()))
        {
            int fallbackCalls = 0;
            IList<string> models = await new OpenAiCompatibleTranslator(client,
                delegate {
                    fallbackCalls++;
                    return new HttpClient(new FakeHandler(
                        "{\"data\":[{\"id\":\"gpt-5.6-terra\"}]}"));
                }).ListModelsAsync(settings, CancellationToken.None);
            Require(models.Count == 1 && models[0] == "gpt-5.6-terra" &&
                fallbackCalls == 1,
                "model list did not use the fallback transport");
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            "<html><body>gateway</body></html>", "text/html")))
        {
            bool explained = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).ListModelsAsync(
                    settings, CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                explained = exception.Message.Contains("模型列表不是有效 JSON");
            }
            Require(explained, "model list failure was not explained");
        }
    }
    private static async Task CheckTranslation()
    {
        TranslationApiSettings settings = TranslationApiSettings.Default();
        settings.Model = "test-model";
        settings.ApiKey = "fake-key";
        string marker = "__STPV_TOKEN_0000__";
        FakeHandler handler = new FakeHandler(ResponseFor("[\"Roof " +
            marker + "\"]"));
        using (HttpClient client = new HttpClient(handler))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client).
                TranslateAsync(settings, new List<string> { "屋顶 " + marker },
                    CancellationToken.None);
            Require(result.Count == 1 && result[0] == "Roof " + marker,
                "translated text or marker changed");
            Require(handler.Method == "POST" &&
                handler.Authorization == "Bearer fake-key" &&
                handler.Body.Contains("test-model") &&
                handler.Body.Contains(marker),
                "API request was malformed");
        }
        int formatIndex = 0;
        foreach (string content in new [] {
            "Roof " + marker,
            "\"Roof " + marker + "\"",
            "```json\n[\"Roof " + marker + "\"]\n```",
            "{\"translation\":\"Roof " + marker + "\"}"
        })
        {
            formatIndex++;
            using (HttpClient client = new HttpClient(new FakeHandler(
                ResponseFor(content))))
            {
                IList<string> result;
                try
                {
                    result = await new OpenAiCompatibleTranslator(client).
                        TranslateAsync(settings,
                            new List<string> { "屋顶 " + marker },
                            CancellationToken.None);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        "single response format " + formatIndex + ": " +
                        exception.Message);
                }
                Require(result.Count == 1 && result[0] == "Roof " + marker,
                    "single translation response was not parsed");
            }
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            "{\"success\":true,\"data\":" + ResponseFor("[\"Roof\"]") + "}")))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client).
                TranslateAsync(settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            Require(result.Count == 1 && result[0] == "Roof",
                "wrapped gateway response was not parsed");
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            WrappedPartsResponse())))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client).
                TranslateAsync(settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            Require(result.Count == 1 && result[0] == "Roof",
                "content parts response was not parsed");
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            ResponseFor("{\"translations\":[\"Roof\",\"Rail\"]}"))))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client).
                TranslateAsync(settings, new List<string> { "屋顶", "导轨" },
                    CancellationToken.None);
            Require(result.Count == 2 && result[1] == "Rail",
                "batch translation response was not parsed");
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            ResponseFor("[invalid"))))
        {
            bool rejected = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    settings, new List<string> { "one" },
                    CancellationToken.None);
            }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "malformed single result was accepted");
        }
        foreach (string content in new [] { "[]", "Roof", "[invalid" })
        {
            using (HttpClient client = new HttpClient(new FakeHandler(
                ResponseFor(content))))
            {
                bool rejected = false;
                try
                {
                    await new OpenAiCompatibleTranslator(client).TranslateAsync(
                        settings, new List<string> { "one", "two" },
                        CancellationToken.None);
                }
                catch (InvalidOperationException) { rejected = true; }
                Require(rejected, "ambiguous batch translation was accepted");
            }
        }
        using (HttpClient client = new HttpClient(new FakeHandler(
            "<html>dashboard</html>", "text/html")))
        {
            bool rejected = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                rejected = exception.Message.Contains("非 JSON");
            }
            Require(rejected, "HTML API response was not rejected clearly");
        }
        TranslationApiSettings gateway = TranslationApiSettings.Default();
        gateway.Endpoint = "https://cdn.coderelay.cn/";
        gateway.Model = "unavailable-text-model";
        gateway.ApiKey = "fake-key";
        ModelFailureHandler modelHandler = new ModelFailureHandler();
        using (HttpClient client = new HttpClient(modelHandler))
        {
            bool explained = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    gateway, new List<string> { "屋顶" },
                    CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                explained = exception.Message.Contains("不包含") &&
                    exception.Message.Contains("gpt-image-2") &&
                    exception.Message.Contains("Codex-image") &&
                    exception.Message.Contains("没有文本模型");
            }
            Require(explained && modelHandler.PostCount == 1 &&
                modelHandler.GetCount == 1,
                "unavailable gateway model was not diagnosed");
        }
    }

    // 中转网关的响应形态不稳定：顶层可能是 JSON 数组、可能夹带前后缀文本、
    // 也可能被截断或返回网页。这里覆盖用户实际遇到的这几种情况。
    private static async Task CheckRelayResponseShapes()
    {
        TranslationApiSettings settings = TranslationApiSettings.Default();
        settings.Model = "test-model";
        settings.ApiKey = "fake-key";

        using (HttpClient client = new HttpClient(new FakeHandler(
            "[" + ResponseFor("[\"Roof\"]") + "]")))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client)
                .TranslateAsync(settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            Require(result.Count == 1 && result[0] == "Roof",
                "top level JSON array response was not parsed");
        }

        using (HttpClient client = new HttpClient(new FakeHandler(
            "gateway notice\n" + ResponseFor("[\"Roof\"]") + "\nend of stream")))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client)
                .TranslateAsync(settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            Require(result.Count == 1 && result[0] == "Roof",
                "response with surrounding text was not parsed");
        }

        using (HttpClient client = new HttpClient(new FakeHandler(String.Empty)))
        {
            bool explained = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                explained = exception.Message.Contains("翻译服务响应无法使用") &&
                    exception.Message.Contains("HTTP 200") &&
                    exception.Message.Contains("响应长度") &&
                    exception.Message.Contains("图纸未修改");
            }
            Require(explained, "empty response was not explained clearly");
        }

        using (HttpClient client = new HttpClient(new FakeHandler(
            "<html><body>gateway error page</body></html>", "text/html")))
        {
            bool explained = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    settings, new List<string> { "屋顶" },
                    CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                explained = exception.Message.Contains("HTTP 200") &&
                    exception.Message.Contains("text/html") &&
                    exception.Message.Contains("响应长度") &&
                    exception.Message.Contains("gateway error page");
            }
            Require(explained, "HTML response was not explained clearly");
        }

        List<string> many = new List<string>();
        for (int index = 0; index < 45; index++)
            many.Add("屋顶" + index);

        BatchHandler split = new BatchHandler(0);
        using (HttpClient client = new HttpClient(split))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client)
                .TranslateAsync(settings, many, CancellationToken.None);
            Require(result.Count == 45 && result[44] == "EN 屋顶44",
                "large batch translation was not merged");
            Require(split.Calls == 2 && split.BatchSizes.Count == 2 &&
                split.BatchSizes[0] == 40 && split.BatchSizes[1] == 5,
                "large batch was not split at the request limit");
        }

        BatchHandler truncated = new BatchHandler(2);
        using (HttpClient client = new HttpClient(truncated))
        {
            IList<string> result = await new OpenAiCompatibleTranslator(client)
                .TranslateAsync(settings, many, CancellationToken.None);
            Require(result.Count == 45 && result[0] == "EN 屋顶0",
                "truncated gateway response was not recovered by splitting");
            Require(truncated.Calls == 5,
                "truncated response recovery made " + truncated.Calls +
                " requests");
        }

        FakeHandler wrong = new FakeHandler(ResponseFor("[]"));
        using (HttpClient client = new HttpClient(wrong))
        {
            bool rejected = false;
            try
            {
                await new OpenAiCompatibleTranslator(client).TranslateAsync(
                    settings, many, CancellationToken.None);
            }
            catch (InvalidOperationException exception)
            {
                rejected = exception.Message.Contains("数量不匹配");
            }
            Require(rejected && wrong.Calls == 1,
                "semantic batch failure was retried " + wrong.Calls + " times");
        }
    }

    private static string ResponseFor(string content)
    {
        return new JavaScriptSerializer().Serialize(new {
            choices = new[] { new { message = new { content = content } } }
        });
    }

    private static string WrappedPartsResponse()
    {
        return new JavaScriptSerializer().Serialize(new {
            data = new {
                choices = new[] { new { message = new {
                    content = new object[] {
                        new { type = "text", text = "[\"Roof\"]" } } } } }
            },
            success = true
        });
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly string _response;
        private readonly string _contentType;
        internal int Calls;
        internal string Method;
        internal string Authorization;
        internal string Body;

        internal FakeHandler(string response,
            string contentType = "application/json")
        {
            _response = response;
            _contentType = contentType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Method = request.Method.Method;
            Authorization = request.Headers.Authorization.ToString();
            Body = request.Content == null ? String.Empty
                : await request.Content.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(_response, Encoding.UTF8,
                    _contentType)
            };
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellation)
        {
            throw new HttpRequestException("发送请求时出错。",
                new IOException("由于远程方已关闭传输流，身份验证失败。"));
        }
    }

    private sealed class ModelFailureHandler : HttpMessageHandler
    {
        internal int PostCount;
        internal int GetCount;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellation)
        {
            if (request.Method == HttpMethod.Post)
            {
                PostCount++;
                Require(request.RequestUri.AbsolutePath ==
                    "/v1/chat/completions", "gateway root was posted directly");
                return Task.FromResult(new HttpResponseMessage(
                    HttpStatusCode.ServiceUnavailable) {
                    Content = new StringContent(
                        "{\"error\":{\"code\":\"model_not_found\",\"message\":" +
                        "\"No available channel for model under group Codex-image\"}}",
                        Encoding.UTF8, "application/json")
                });
            }
            GetCount++;
            Require(request.RequestUri.AbsolutePath == "/v1/models" &&
                request.Headers.Authorization.ToString() == "Bearer fake-key",
                "model discovery request was malformed");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    "{\"data\":[{\"id\":\"gpt-image-2\"}]}",
                    Encoding.UTF8, "application/json")
            });
        }
    }

    // 按请求里的文字条数生成响应，用于验证分批请求和截断恢复。
    private sealed class BatchHandler : HttpMessageHandler
    {
        private readonly int _failureCount;

        internal BatchHandler(int failureCount)
        {
            _failureCount = failureCount;
        }

        internal int Calls;
        internal List<int> BatchSizes = new List<int>();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellation)
        {
            // 失败诊断会用同一个密钥读取一次模型列表，别当成翻译请求。
            if (request.Method != HttpMethod.Post)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(
                        "{\"data\":[{\"id\":\"test-model\"}]}",
                        Encoding.UTF8, "application/json")
                };
            }
            Calls++;
            string body = await request.Content.ReadAsStringAsync();
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IDictionary outer = (IDictionary)serializer.DeserializeObject(body);
            IList messages = (IList)outer["messages"];
            IDictionary payload = (IDictionary)serializer.DeserializeObject(
                (string)((IDictionary)messages[1])["content"]);
            IList texts = (IList)payload["texts"];
            BatchSizes.Add(texts.Count);
            if (Calls <= _failureCount)
            {
                // 模拟中转站把响应截断成半截 JSON。
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(
                        "{\"choices\":[{\"message\":{\"content\":\"[[\\\"",
                        Encoding.UTF8, "application/json")
                };
            }
            List<string> translated = new List<string>();
            foreach (object text in texts)
                translated.Add("EN " + text);
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(
                    ResponseFor(serializer.Serialize(translated)),
                    Encoding.UTF8, "application/json")
            };
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

