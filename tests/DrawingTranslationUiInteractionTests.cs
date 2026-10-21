using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Autodesk.AutoCAD.DatabaseServices;
using StructuralToolbox.DrawingTranslation.Api;
using StructuralToolbox.DrawingTranslation.Commands;
using StructuralToolbox.DrawingTranslation.Core;
using StructuralToolbox.DrawingTranslation.UI;

// 用真实 WinForms 消息循环驱动 API 设置面板和确认窗，后端是本机 HTTP 服务，
// 覆盖必须人工点一遍的路径：测试连接成功/失败、获取模型、自动翻译后确认写入、
// 取消不写入、翻译失败不写入。
internal static class DrawingTranslationUiInteractionTests
{
    private const string Translated1 = "Translated text 1";

    [STAThread]
    private static int Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveCadAssembly;
        try
        {
            CheckTestConnectionSuccess();
            CheckTestConnectionFailure();
            CheckFetchModels();
            CheckReviewDialogAutoTranslateAndConfirm();
            CheckReviewDialogRestoresInternalTokens();
            CheckReviewDialogCancelKeepsDrawing();
            CheckReviewDialogFailureBlocksWrite();
            Console.WriteLine("PASS drawing translation UI interaction tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL drawing translation UI interaction: " +
                exception.GetType().FullName + " " + exception.Message);
            return 1;
        }
    }

    private static Assembly ResolveCadAssembly(object sender,
        ResolveEventArgs eventArgs)
    {
        string name = new AssemblyName(eventArgs.Name).Name;
        if (!String.Equals(name, "acmgd", StringComparison.OrdinalIgnoreCase) &&
            !String.Equals(name, "acdbmgd", StringComparison.OrdinalIgnoreCase) &&
            !String.Equals(name, "accoremgd", StringComparison.OrdinalIgnoreCase))
            return null;
        return Assembly.LoadFrom(Path.Combine(@"D:\cad\AutoCAD 2020",
            name + ".dll"));
    }

    private static void CheckTestConnectionSuccess()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                using (TranslationApiSettingsDialog dialog =
                    new TranslationApiSettingsDialog(store))
                {
                    Label status = Find<Label>(dialog, "ApiTestStatusLabel");
                    Button test = Find<Button>(dialog, "TestApiButton");
                    // 消息循环结束时会关闭窗体，子控件文本随之清空，
                    // 所以断言用的值必须在循环内部先取到局部变量。
                    string observed = null;
                    int requests = -1;
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (ticks == 1)
                            {
                                RaiseClick(test);
                                return false;
                            }
                            if (status.Text.IndexOf("连接成功",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            observed = status.Text;
                            requests = server.ChatRequests;
                            return true;
                        }, 200);
                    driver.Run();
                    Require(observed != null, "测试连接没有在超时前给出结果");
                    Require(observed.StartsWith("连接成功，示例译文：",
                        StringComparison.Ordinal),
                        "测试连接成功后状态栏没有给出示例译文：" + observed);
                    Require(requests == 1,
                        "测试连接应只请求一次，实际 " + requests + " 次");
                    Require(dismisser.Count == 0,
                        "测试连接成功时不应弹出提示框：" + dismisser.Describe());
                }
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    private static void CheckTestConnectionFailure()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            server.FailWith(401);
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                using (TranslationApiSettingsDialog dialog =
                    new TranslationApiSettingsDialog(store))
                {
                    Label status = Find<Label>(dialog, "ApiTestStatusLabel");
                    Button test = Find<Button>(dialog, "TestApiButton");
                    string observed = null;
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (ticks == 1)
                            {
                                RaiseClick(test);
                                return false;
                            }
                            if (dismisser.Count < 1 ||
                                status.Text.IndexOf("连接失败",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            observed = status.Text;
                            return true;
                        }, 200);
                    driver.Run();
                    Require(observed != null, "连接失败时没有在超时前给出结果");
                    Require(observed.StartsWith("连接失败：",
                        StringComparison.Ordinal),
                        "密钥无效时状态栏没有给出失败原因：" + observed);
                    Require(observed.IndexOf("401", StringComparison.Ordinal) >= 0,
                        "失败原因没有带上 HTTP 状态码：" + observed);
                    Require(dismisser.SawCaption("测试连接"),
                        "连接失败时应弹出说明： " + dismisser.Describe());
                }
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    private static void CheckFetchModels()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                using (TranslationApiSettingsDialog dialog =
                    new TranslationApiSettingsDialog(store))
                {
                    Label status = Find<Label>(dialog, "ApiTestStatusLabel");
                    Button models = Find<Button>(dialog, "FetchModelsButton");
                    ComboBox model = Find<ComboBox>(dialog, "ApiModel");
                    model.Items.Clear();
                    model.SelectedIndex = -1;
                    string observed = null;
                    bool restored = false;
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (ticks == 1)
                            {
                                RaiseClick(models);
                                return false;
                            }
                            if (status.Text.IndexOf("已获取",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            observed = status.Text;
                            restored = models.Text == "获取模型" && models.Enabled;
                            return restored && model.Items.Count == 2;
                        }, 200);
                    driver.Run();
                    Require(observed != null, "获取模型没有在超时前给出结果");
                    Require(observed.StartsWith("已获取 2 个模型",
                        StringComparison.Ordinal),
                        "获取模型后状态栏没有给出数量：" + observed);
                    Require(Convert.ToString(model.Items[0]) == "mock-alpha" &&
                        Convert.ToString(model.Items[1]) == "mock-beta" &&
                        model.SelectedIndex == 0,
                        "获取模型没有填充并选中下拉列表");
                    Require(dismisser.Count == 0,
                        "获取模型成功不应重复弹出模型清单：" +
                        dismisser.Describe());
                    Require(restored, "获取模型结束后按钮没有恢复");
                }
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    private static void CheckReviewDialogAutoTranslateAndConfirm()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                IList<TranslationTarget> targets = CreateTargets();
                using (TranslationReviewDialog dialog = new TranslationReviewDialog(
                    targets, 2, store))
                {
                    Label status = Find<Label>(dialog, "TranslationStatus");
                    DataGridView grid = Find<DataGridView>(dialog,
                        "TranslationReviewGrid");
                    Button confirm = Find<Button>(dialog,
                        "ConfirmTranslationButton");
                    string observedTranslation = null;
                    string observedSource = null;
                    int popups = -1;
                    DialogResult result = DialogResult.None;
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (status.Text.IndexOf("已翻译",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            observedTranslation = Convert.ToString(
                                grid.Rows[0].Cells[2].Value);
                            observedSource = Convert.ToString(
                                grid.Rows[0].Cells[1].Value);
                            popups = dismisser.Count;
                            RaiseClick(confirm);
                            result = dialog.DialogResult;
                            return true;
                        }, 300);
                    driver.Run();
                    Require(popups == 0,
                        "自动翻译成功时不应弹出提示框：" + dismisser.Describe());
                    Require(observedTranslation == Translated1,
                        "自动翻译没有把译文填进对照窗：" + observedTranslation);
                    Require(observedSource == targets[0].Item.RawContent,
                        "对照窗原文列不正确：" + observedSource);
                    Require(result == DialogResult.OK,
                        "确认后对照窗没有返回 OK");
                    Require(dialog.Edits != null && dialog.Edits.Count == 1,
                        "确认后没有收集到要写入的译文");
                    Require(dialog.Edits[0].Value == Translated1,
                        "确认后写入内容不是对照窗里的译文：" +
                        dialog.Edits[0].Value);
                }
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    private static void CheckReviewDialogRestoresInternalTokens()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                DrawingTextItem item = DrawingTextItemFactory.Create("2A",
                    DrawingTextKind.DbText, "厚度为3~5mm", false, false);
                string marker = ProtectedText.TokenName(0);
                server.FirstTranslation = "Thickness is 3~" + marker;
                IList<TranslationTarget> targets =
                    new List<TranslationTarget> {
                        new TranslationTarget(ObjectId.Null, -1, -1, item) };
                using (TranslationReviewDialog dialog =
                    new TranslationReviewDialog(targets, 0, store))
                {
                    Label status = Find<Label>(dialog, "TranslationStatus");
                    DataGridView grid = Find<DataGridView>(dialog,
                        "TranslationReviewGrid");
                    string observed = null;
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (status.Text.IndexOf("已翻译",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            observed = Convert.ToString(
                                grid.Rows[0].Cells[2].Value);
                            return true;
                        }, 300);
                    driver.Run();
                    Require(observed == "Thickness is 3~5mm" &&
                        !ProtectedText.ContainsInternalToken(observed),
                        "确认窗泄漏了内部占位符：" + observed);
                    Require(dismisser.Count == 0,
                        "占位符正常还原时不应弹窗：" + dismisser.Describe());
                }
            }
            finally { DeleteFile(path); }
        }
    }

    private static void CheckReviewDialogCancelKeepsDrawing()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                IList<TranslationTarget> targets = CreateTargets();
                using (TranslationReviewDialog dialog = new TranslationReviewDialog(
                    targets, 0, store))
                {
                    Label status = Find<Label>(dialog, "TranslationStatus");
                    string observed = null;
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (status.Text.IndexOf("已翻译",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            observed = status.Text;
                            return true;
                        }, 300);
                    driver.Run();
                    Require(observed != null, "取消前没有在超时内完成自动翻译");
                    Require(dialog.Edits == null,
                        "取消对照窗后不应产生任何写入内容");
                    Require(dialog.DialogResult != DialogResult.OK,
                        "取消对照窗却返回了 OK");
                    Require(dismisser.Count == 0,
                        "取消对照窗时不应弹出提示框：" + dismisser.Describe());
                }
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    private static void CheckReviewDialogFailureBlocksWrite()
    {
        using (MockTranslationServer server = new MockTranslationServer())
        using (MessageDialogDismisser dismisser = new MessageDialogDismisser())
        {
            server.FailWith(500);
            string path = TempSettingsPath();
            try
            {
                TranslationApiSettingsStore store = CreateStore(path, server);
                IList<TranslationTarget> targets = CreateTargets();
                using (TranslationReviewDialog dialog = new TranslationReviewDialog(
                    targets, 0, store))
                {
                    Label status = Find<Label>(dialog, "TranslationStatus");
                    DataGridView grid = Find<DataGridView>(dialog,
                        "TranslationReviewGrid");
                    Button confirm = Find<Button>(dialog,
                        "ConfirmTranslationButton");
                    Button retry = Find<Button>(dialog,
                        "RetryTranslationButton");
                    DialogDriver driver = new DialogDriver(dialog,
                        delegate(int ticks)
                        {
                            if (dismisser.Count < 1 ||
                                status.Text.IndexOf("自动翻译失败",
                                    StringComparison.Ordinal) < 0)
                                return false;
                            Require(status.Text == "自动翻译失败，图纸未修改",
                                "翻译失败时的提示不对：" + status.Text);
                            Require(dialog.Edits == null,
                                "翻译失败后不应准备写入内容");
                            Require(String.IsNullOrEmpty(Convert.ToString(
                                grid.Rows[0].Cells[2].Value)),
                                "翻译失败后对照窗不应显示内部模板");
                            Require(!confirm.Enabled && retry.Visible,
                                "翻译失败后应禁用确认并显示重试");
                            return true;
                        }, 300);
                    driver.Run();
                    Require(dialog.Edits == null,
                        "翻译失败后确认写入却产生了内容");
                    Require(dismisser.SawCaption("自动翻译"),
                        "翻译失败时应弹出说明：" + dismisser.Describe());
                }
            }
            finally
            {
                DeleteFile(path);
            }
        }
    }

    private static TranslationApiSettingsStore CreateStore(string path,
        MockTranslationServer server)
    {
        TranslationApiSettingsStore store =
            new TranslationApiSettingsStore(path);
        store.Save(new TranslationApiSettings {
            Endpoint = server.BaseUrl + "/v1",
            Model = "mock-alpha",
            ApiKey = "ui-test-key-not-a-real-secret",
            TargetLanguage = "English",
            Workflow = TranslationWorkflow.ConfirmBeforeReplace,
            ProxyMode = TranslationProxyMode.Direct,
            ProxyAddress = String.Empty
        });
        return store;
    }

    private static IList<TranslationTarget> CreateTargets()
    {
        DrawingTextItem item = DrawingTextItemFactory.Create("1A",
            DrawingTextKind.DbText, "技术要求", false, false);
        List<TranslationTarget> targets = new List<TranslationTarget>();
        targets.Add(new TranslationTarget(ObjectId.Null, -1, -1, item));
        return targets;
    }

    private static string TempSettingsPath()
    {
        string directory = Path.Combine("work", "tests", "ui-interaction");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory,
            Guid.NewGuid().ToString("N") + ".json");
    }

    private static void DeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static T Find<T>(Form dialog, string name) where T : Control
    {
        Control[] found = dialog.Controls.Find(name, true);
        Require(found.Length == 1, "控件缺失：" + name);
        return (T)found[0];
    }

    private static void RaiseClick(Control control)
    {
        MethodInfo onClick = typeof(Control).GetMethod("OnClick",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(onClick != null, "Control.OnClick was not found");
        onClick.Invoke(control, new object[] { EventArgs.Empty });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    // 在真实消息循环里推进对话框：每 100 毫秒调一次 step，
    // step 返回 true 表示这一轮交互做完，随后关闭窗口结束循环。
    private sealed class DialogDriver
    {
        private readonly Form _form;
        private readonly Func<int, bool> _step;
        private readonly System.Windows.Forms.Timer _timer;
        private readonly int _maximumTicks;
        private int _ticks;
        private Exception _failure;

        internal DialogDriver(Form form, Func<int, bool> step,
            int maximumTicks)
        {
            _form = form;
            _step = step;
            _maximumTicks = maximumTicks;
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 100;
            _timer.Tick += OnTick;
        }

        internal void Run()
        {
            _timer.Start();
            Application.Run(_form);
            _timer.Stop();
            _timer.Dispose();
            if (_failure != null)
                throw new InvalidOperationException(_failure.Message,
                    _failure);
        }

        private void OnTick(object sender, EventArgs args)
        {
            try
            {
                _ticks++;
                if (_step(_ticks))
                {
                    _timer.Stop();
                    _form.Close();
                    return;
                }
                if (_ticks > _maximumTicks)
                    throw new InvalidOperationException(
                        "界面交互在 " + (_maximumTicks / 10) + " 秒内没有完成。");
            }
            catch (Exception exception)
            {
                _failure = exception;
                _timer.Stop();
                _form.Close();
            }
        }
    }

    // 关掉 MessageBox 之类系统对话框，并把标题记下来供断言使用。
    private sealed class MessageDialogDismisser : IDisposable
    {
        private delegate bool EnumWindowsProcedure(IntPtr window,
            IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProcedure callback,
            IntPtr parameter);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window,
            StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window,
            StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window,
            out uint processId);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, uint message,
            IntPtr wParam, IntPtr lParam);

        private const uint CloseMessage = 0x0010;
        private const string DialogClass = "#32770";
        private readonly System.Windows.Forms.Timer _timer;
        private readonly List<string> _dismissed = new List<string>();

        internal MessageDialogDismisser()
        {
            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 100;
            _timer.Tick += delegate { Dismiss(); };
            _timer.Start();
        }

        internal int Count
        {
            get { return _dismissed.Count; }
        }

        internal bool SawCaption(string caption)
        {
            foreach (string item in _dismissed)
            {
                if (String.Equals(item, caption, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal string Describe()
        {
            return _dismissed.Count == 0
                ? "（没有弹出任何对话框）"
                : String.Join("、", _dismissed.ToArray());
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }

        private void Dismiss()
        {
            uint self = (uint)Process.GetCurrentProcess().Id;
            EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                if (!IsWindowVisible(window))
                    return true;
                StringBuilder className = new StringBuilder(64);
                GetClassName(window, className, className.Capacity);
                if (!String.Equals(className.ToString(), DialogClass,
                        StringComparison.Ordinal))
                    return true;
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != self)
                    return true;
                StringBuilder caption = new StringBuilder(256);
                GetWindowText(window, caption, caption.Capacity);
                _dismissed.Add(caption.ToString());
                PostMessage(window, CloseMessage, IntPtr.Zero, IntPtr.Zero);
                return false;
            }, IntPtr.Zero);
        }
    }

    // 进程内的最小 OpenAI 兼容服务：/v1/models 与 /v1/chat/completions。
    private sealed class MockTranslationServer : IDisposable
    {
        private const string NewLine = "\r\n";
        private readonly TcpListener _listener;
        private readonly Thread _worker;
        private volatile bool _running = true;
        private volatile int _failStatus;
        private int _chatRequests;
        private string _lastRequest;
        internal string FirstTranslation = Translated1;

        internal MockTranslationServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _worker = new Thread(Loop);
            _worker.IsBackground = true;
            _worker.Start();
        }

        internal int Port
        {
            get { return ((IPEndPoint)_listener.LocalEndpoint).Port; }
        }

        internal string BaseUrl
        {
            get { return "http://127.0.0.1:" + Port; }
        }

        internal int ChatRequests
        {
            get { return _chatRequests; }
        }

        internal string LastRequest
        {
            get { return _lastRequest; }
        }

        internal void FailWith(int status)
        {
            _failStatus = status;
        }

        public void Dispose()
        {
            _running = false;
            try
            {
                _listener.Stop();
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            if (!_worker.Join(3000))
                throw new InvalidOperationException("本机测试服务没有及时退出。");
        }

        private void Loop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (SocketException) { break; }
                catch (InvalidOperationException) { break; }
                try
                {
                    using (client)
                    using (NetworkStream stream = client.GetStream())
                        Serve(stream);
                }
                catch (IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private void Serve(NetworkStream stream)
        {
            byte[] buffer = new byte[8192];
            MemoryStream received = new MemoryStream();
            byte[] terminator = Encoding.ASCII.GetBytes(NewLine + NewLine);
            int headerEnd = -1;
            while (headerEnd < 0)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    return;
                received.Write(buffer, 0, read);
                headerEnd = IndexOf(received.ToArray(), terminator);
            }
            byte[] all = received.ToArray();
            string header = Encoding.ASCII.GetString(all, 0, headerEnd);
            string[] lines = header.Split(new string[] { NewLine },
                StringSplitOptions.None);
            string path = lines[0].Split(' ')[1];
            int contentLength = 0;
            foreach (string line in lines)
            {
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                if (!String.Equals(line.Substring(0, colon).Trim(),
                        "Content-Length", StringComparison.OrdinalIgnoreCase))
                    continue;
                Int32.TryParse(line.Substring(colon + 1).Trim(),
                    out contentLength);
            }
            List<byte> body = new List<byte>();
            for (int index = headerEnd + terminator.Length;
                index < all.Length; index++)
                body.Add(all[index]);
            while (body.Count < contentLength)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                for (int index = 0; index < read; index++)
                    body.Add(buffer[index]);
            }
            string request = Encoding.UTF8.GetString(body.ToArray());
            int status;
            string response = Build(path, request, out status);
            byte[] payload = Encoding.UTF8.GetBytes(response);
            StringBuilder head = new StringBuilder();
            head.Append("HTTP/1.1 ").Append(status).Append(' ')
                .Append(Reason(status)).Append(NewLine);
            head.Append("Content-Type: application/json; charset=utf-8")
                .Append(NewLine);
            head.Append("Content-Length: ").Append(payload.Length)
                .Append(NewLine);
            head.Append("Connection: close").Append(NewLine).Append(NewLine);
            byte[] headBytes = Encoding.ASCII.GetBytes(head.ToString());
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int start = 0; start <= haystack.Length - needle.Length;
                start++)
            {
                bool match = true;
                for (int index = 0; index < needle.Length; index++)
                {
                    if (haystack[start + index] != needle[index])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                    return start;
            }
            return -1;
        }

        private static string Reason(int status)
        {
            if (status == 200)
                return "OK";
            if (status == 401)
                return "Unauthorized";
            if (status == 404)
                return "Not Found";
            return "Server Error";
        }

        private string Build(string path, string request, out int status)
        {
            status = 200;
            if (path.IndexOf("/models", StringComparison.OrdinalIgnoreCase) >= 0)
                return "{\"object\":\"list\",\"data\":[{\"id\":\"mock-alpha\"}," +
                    "{\"id\":\"mock-beta\"}]}";
            if (path.IndexOf("/chat/completions",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                status = 404;
                return "{\"error\":{\"message\":\"unknown path\"}}";
            }
            Interlocked.Increment(ref _chatRequests);
            _lastRequest = request;
            int failure = _failStatus;
            if (failure == 401)
            {
                status = 401;
                return "{\"error\":{\"message\":\"invalid api key\"}}";
            }
            if (failure != 0)
            {
                status = failure;
                return "{\"error\":{\"message\":\"upstream unavailable\"}}";
            }
            int count = CountTexts(request);
            List<string> translated = new List<string>();
            for (int index = 0; index < count; index++)
                translated.Add(index == 0 ? FirstTranslation
                    : "Translated text " + (index + 1));
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            return serializer.Serialize(new Dictionary<string, object> {
                { "choices", new object[] {
                    new Dictionary<string, object> {
                        { "message", new Dictionary<string, object> {
                            { "content", serializer.Serialize(translated) } } } } } }
            });
        }

        private static int CountTexts(string request)
        {
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> payload =
                    serializer.Deserialize<Dictionary<string, object>>(request);
                object[] messages = (object[])payload["messages"];
                Dictionary<string, object> user =
                    (Dictionary<string, object>)messages[1];
                Dictionary<string, object> content =
                    serializer.Deserialize<Dictionary<string, object>>(
                        Convert.ToString(user["content"]));
                object[] texts = (object[])content["texts"];
                return texts.Length;
            }
            catch (Exception)
            {
                return 1;
            }
        }
    }
}
