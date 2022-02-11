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
using System.Security.Cryptography;

namespace EasyIpc.Tests
{
    [TestClass]
    public class E2ETests
    {
        private string _pipeName;
        private CancellationTokenSource _cts;
        private IConnectionFactory _connectionFactory;
        private IServer _server;
        private IClient _client;

        [TestInitialize]
        public async Task TestInit()
        {
            _pipeName = Guid.NewGuid().ToString();
            _cts = new CancellationTokenSource();

            _connectionFactory = Bootstrapper.DefaultFactory;
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
        public async Task Send_GivenIdealScenario_ReceivesMessages()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            var pongFromServer = string.Empty;
            var pongFromClient = string.Empty;

            _client.On((Ping ping) =>
            {
                _client.Send(new Pong("Pong from client"));
            });

            _client.On((Pong pong) =>
            {
                pongFromServer = pong.Message;
            });

            _server.On((Ping ping) =>
            {
                _server.Send(new Pong("Pong from server"));
            });

            _server.On((Pong pong) =>
            {
                pongFromClient = pong.Message;
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            await _client.Send(new Ping());
            await _server.Send(new Ping());

            TaskHelper.DelayUntil(() =>
                !string.IsNullOrWhiteSpace(pongFromServer) &&
                !string.IsNullOrWhiteSpace(pongFromClient),
                TimeSpan.FromSeconds(1));

            Assert.AreEqual("Pong from server", pongFromServer);
            Assert.AreEqual("Pong from client", pongFromClient);
        }

        [TestMethod]
        public async Task RemoveAll_GivenValidType_RemovesAll()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            var count = 0;

            _server.On((Ping ping) =>
            {
                count++;
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            await _client.Send(new Ping());

            TaskHelper.DelayUntil(() =>
                count > 0,
                TimeSpan.FromSeconds(1));

            Assert.AreEqual(1, count);

            _server.Off<Ping>();

            await _client.Send(new Ping());
            await Task.Delay(1_000);

            Assert.AreEqual(1, count);
        }

        [TestMethod]
        public async Task RemoveAll_GivenInvalidType_RemovesNone()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            var count = 0;

            _server.On((Ping ping) =>
            {
                count++;
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            await _client.Send(new Ping());

            TaskHelper.DelayUntil(() =>
                count > 0,
                TimeSpan.FromSeconds(1));

            Assert.AreEqual(1, count);

            _server.Off<Pong>();

            await _client.Send(new Ping());
            TaskHelper.DelayUntil(() =>
              count > 1,
              TimeSpan.FromSeconds(1));

            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public async Task RemoveAll_GivenValidToken_RemovesOne()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            var count = 0;

            var token1 = _server.On((Ping ping) =>
            {
                count++;
            });
            var token2 = _server.On((Ping ping) =>
            {
                count++;
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            await _client.Send(new Ping());

            TaskHelper.DelayUntil(() =>
                count > 1,
                TimeSpan.FromSeconds(1));

            Assert.AreEqual(2, count);

            _server.Off<Ping>(token1);

            await _client.Send(new Ping());
            TaskHelper.DelayUntil(() =>
              count > 2,
              TimeSpan.FromSeconds(1));

            Assert.AreEqual(3, count);
        }

        [TestMethod]
        public async Task Invoke_GivenIdealScenario_ReturnsValue()
        {
            _ = _server.WaitForConnection(_cts.Token);
            var result = await _client.Connect(1000);

            Assert.IsTrue(result);

            _client.On((Ping pong) =>
            {
                return new Pong($"Pong from Client: {pong.Message}");
            });

            _server.On((Ping pong) =>
            {
                return Task.FromResult(new Pong($"Pong from Server: {pong.Message}"));
            });

            _client.BeginRead(_cts.Token);
            _server.BeginRead(_cts.Token);

            var serverResponse = await _client.Invoke<Ping, Pong>(new Ping("Client Ping"), 1000);
            var clientResponse = await _server.Invoke<Ping, Pong>(new Ping("Server Ping"), 1000);

            Assert.AreEqual("Pong from Client: Server Ping", clientResponse.Value.Message);
            Assert.AreEqual("Pong from Server: Client Ping", serverResponse.Value.Message);
        }

        [TestMethod]
        public async Task Send_GivenIdealScenario_OkThroughput()
        {
            var connectionFactory = new ConnectionFactory(new CallbackStoreFactory(), new LoggerFactory());
            var server = await connectionFactory.CreateServer("throughput-test");
            var client = await connectionFactory.CreateClient(".", "throughput-test");

            _ = server.WaitForConnection(CancellationToken.None);
            var result = await client.Connect(1000);

            Assert.IsTrue(result);

            int bytesReceived = 0;

            server.On((TestImage image) =>
            {
                bytesReceived += image.EncodedImage.Length;
            });

            client.BeginRead(_cts.Token);
            server.BeginRead(_cts.Token);

            var buffer = RandomNumberGenerator.GetBytes(2_097_152);

            var testImage = new TestImage()
            {
                EncodedImage = buffer,
                Height = 1080,
                Width = 1920
            };

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 100; i++)
            {
                await client.Send(testImage);
            }
            sw.Stop();

            var mbps = bytesReceived / 1024 / 1024 * 8 / sw.Elapsed.TotalSeconds;

            Console.WriteLine($"Mbps: {mbps}");
            Assert.IsTrue(mbps > 500);
        }

        [DataContract]
        public class Ping
        {
            public Ping() { }

            public Ping(string message)
            {
                Message = message;
            }

            [DataMember]
            public string Message { get; set; }
        }

        [DataContract]
        public class Pong
        {
            public Pong() { }

            public Pong(string message)
            {
                Message = message;
            }


            [DataMember]
            public string Message { get; set; }
        }


        [DataContract]
        public class TestImage
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
