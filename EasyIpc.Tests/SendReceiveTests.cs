using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using EasyIpc;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace EasyIpc.Tests
{
    [TestClass]
    [Ignore("Integration")]
    public class SendReceiveTests
    {
        private string _pipeName;
        private CancellationTokenSource _cts;
        private ConnectionFactory<TestMessageType> _connectionFactory;
        private IServer<TestMessageType> _server;
        private IClient<TestMessageType> _client;

        [TestInitialize]
        public async Task TestInit()
        {
            _pipeName = Guid.NewGuid().ToString();
            _cts = new CancellationTokenSource();

            _connectionFactory = new ConnectionFactory<TestMessageType>(new CallbackCollectionFactory(), new LoggerFactory());
            _server = await _connectionFactory.CreateServer(_pipeName);
            _client = await _connectionFactory.CreateClient(".", _pipeName);
        }


        [TestMethod]
        public async Task WaitForConnection_GivenTokenIsCancelled_ReturnsFalse()
        {
            var waitTask = _server.WaitForConnection(_cts.Token);

            await Task.Delay(10);
            _cts.Cancel();
            await Task.Delay(10);

            Assert.IsFalse(waitTask.Result);
        }

        [TestMethod]
        public async Task Send_GivenHappyPath_ReceivesMessages()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            var clientPong = string.Empty;
            var serverPong = string.Empty;

            _client.On(TestMessageType.Ping, (string message) =>
            {
                _client.Send(TestMessageType.Pong, $"{message} Pong");
            });

            _client.On(TestMessageType.Pong, (string message) =>
            {
                clientPong = message;
            });

            _server.On(TestMessageType.Ping, (string message) =>
            {
                _server.Send(TestMessageType.Pong, $"{message} Pong");
            });

            _server.On(TestMessageType.Pong, (string message) =>
            {
                serverPong = message;
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            await _client.Send(TestMessageType.Ping, "Client");
            await _server.Send(TestMessageType.Ping, "Server");

            TaskHelper.DelayUntil(() =>
                !string.IsNullOrWhiteSpace(clientPong) &&
                !string.IsNullOrWhiteSpace(serverPong),
                TimeSpan.FromSeconds(3));

            Assert.AreEqual("Client Pong", clientPong);
            Assert.AreEqual("Server Pong", serverPong);
        }

        [TestMethod]
        public async Task Invoke_GivenHappyPath_ReturnsValue()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            _client.On(TestMessageType.Ping, () =>
            {
                return "Pong from Client";
            });

            _server.On(TestMessageType.Ping, () =>
            {
                return Task.FromResult("Pong from Server");
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            var serverResponse = await _client.Invoke<string>(TestMessageType.Ping);
            var clientResponse = await _server.Invoke<string>(TestMessageType.Ping);

            Assert.AreEqual("Pong from Client", clientResponse.Value);
            Assert.AreEqual("Pong from Server", serverResponse.Value);
        }

        [TestMethod]
        public async Task Send_GivenHappyPath_OkThroughput()
        {
            var connectionFactory = new ConnectionFactory<PerfMessageType>(new CallbackCollectionFactory(), new LoggerFactory());
            var server = await connectionFactory.CreateServer("throughput-test");
            var client = await connectionFactory.CreateClient(".", "throughput-test");

            _ = server.WaitForConnection(CancellationToken.None);
            var result = await client.Connect(1000);

            Assert.IsTrue(result);

            int bytesReceived = 0;

            server.On(PerfMessageType.ScreenCapture, (ScreenCapture screenCapture) =>
            {
                bytesReceived += screenCapture.EncodedImage.Length;
            });

            client.BeginRead(_cts.Token);
            server.BeginRead(_cts.Token);

            using var mrs = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Remotely.Shared.Client.Tests.Resources.ScreenCapture.jpg");
            using var ms = new MemoryStream();
            await mrs.CopyToAsync(ms);
            var imageBytes = ms.ToArray();
            ms.Seek(0, SeekOrigin.Begin);
            var image = (Bitmap)Image.FromStream(ms);
            var screenCapture = new ScreenCapture()
            {
                EncodedImage = imageBytes,
                Height = image.Height,
                Width = image.Width
            };

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 100; i++)
            {
                await client.Send(PerfMessageType.ScreenCapture, screenCapture);
            }
            sw.Stop();

            var mbps = bytesReceived / 1024 / 1024 * 8 / sw.Elapsed.TotalSeconds;

            Console.WriteLine($"Mbps: {mbps}");
            Assert.IsTrue(mbps > 500);
        }

        public enum PerfMessageType
        {
            ScreenCapture
        }

        [DataContract]
        public class ScreenCapture
        {
            [DataMember]
            public byte[] EncodedImage { get; set; } = Array.Empty<byte>();
            [DataMember]
            public int Width { get; set; }
            [DataMember]
            public int Height { get; set; }
        }

    }
}
