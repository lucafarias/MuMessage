using Moq; // Richiede pacchetto NuGet Moq
using MuLog;
using System.Runtime.CompilerServices;

namespace MuMessage.Tests
{
    public class MessageEngineTests
    {
        // Definiamo i messaggi di test all'interno della classe
        public class TestMessage { public int Id { get; set; } }
        public class AnotherMessage { public string Content { get; set; } = ""; }

        [Fact]
        public async Task DeliverMessage_OnException_ShouldLogErrorMessageInEnglish()
        {
            // Arrange
            var mockLog = new Mock<ILogManager>();
            var engine = new MessageEngine(mockLog.Object);
            var tcs = new TaskCompletionSource();

            engine.Register<TestMessage>(this, _ =>
            {
                try
                {
                    throw new Exception("Test exception");
                }
                finally
                {
                    tcs.SetResult();
                }
            });

            // Act
            engine.Send(new TestMessage { Id = 1 });
            await Task.WhenAny(tcs.Task, Task.Delay(1000));

            // Assert
            mockLog.Verify(x => x.WriteError(
                It.IsAny<Exception>(),
                It.Is<string>(s => s.Contains("An error occurred while dispatching message")),
                It.Is<string>(z => z == "MESSENGER")),
                Times.Once);
        }

        [Fact]
        public async Task Send_ShouldPreserveOrder_FIFO()
        {
            var receivedIds = new List<int>();
            var tcs = new TaskCompletionSource();
            const int messageCount = 50;
            var engine = new MessageEngine(new Mock<ILogManager>().Object);

            engine.Register<TestMessage>(this, m =>
            {
                receivedIds.Add(m.Id);
                if (receivedIds.Count == messageCount)
                    tcs.SetResult();
            });

            for (int i = 1; i <= messageCount; i++)
            {
                engine.Send(new TestMessage { Id = i });
            }

            await Task.WhenAny(tcs.Task, Task.Delay(2000));

            Assert.Equal(messageCount, receivedIds.Count);
            for (int i = 0; i < messageCount; i++)
            {
                Assert.Equal(i + 1, receivedIds[i]);
            }
        }

        [Fact]
        public async Task Register_ShouldNotPreventGarbageCollection()
        {
            var engine = new MessageEngine(new Mock<ILogManager>().Object);
            WeakReference weakRef = CreateAndRegisterInternal(engine);

            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            Assert.False(weakRef.IsAlive, "Object should have been GC collected");

            engine.Send(new TestMessage { Id = 999 });
            await Task.Delay(100);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference CreateAndRegisterInternal(MessageEngine engine)
        {
            var recipient = new object();
            var weakRef = new WeakReference(recipient);
            engine.Register<TestMessage>(recipient, _ => { });
            return weakRef;
        }

        [Fact]
        public async Task Performance_Throughput_Test()
        {
            const int totalMessages = 100_000;
            var tcs = new TaskCompletionSource();
            int processedCount = 0;
            var engine = new MessageEngine(new Mock<ILogManager>().Object);

            engine.Register<TestMessage>(this, _ =>
            {
                if (Interlocked.Increment(ref processedCount) == totalMessages)
                    tcs.TrySetResult();
            });

            for (int i = 0; i < totalMessages; i++)
            {
                engine.Send(new TestMessage { Id = i });
            }

            await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.Equal(totalMessages, processedCount);
        }
    }
}