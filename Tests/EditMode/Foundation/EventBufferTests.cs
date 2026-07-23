using Combat.Foundation.Events;
using NUnit.Framework;

namespace Combat.Tests.Foundation
{
    public sealed class EventBufferTests
    {
        [Test]
        public void Write_PreservesOrderAndExposesReadOnlyStream()
        {
            var buffer = new EventBuffer<string>();

            buffer.Write("spawn");
            buffer.Write("move");
            buffer.Write("death");

            EventStream<string> stream = buffer.AsStream();

            Assert.AreEqual(3, stream.Count);
            Assert.AreEqual("spawn", stream[0]);
            Assert.AreEqual("move", stream[1]);
            Assert.AreEqual("death", stream[2]);
        }

        [Test]
        public void Clear_RemovesBufferedEvents()
        {
            var buffer = new EventBuffer<int>();
            buffer.Write(1);
            buffer.Write(2);

            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(0, buffer.AsStream().Count);
        }

        [Test]
        public void Recorder_AppendsStreamsInOrder()
        {
            var buffer = new EventBuffer<int>();
            var recorder = new EventRecorder<int>();

            buffer.Write(10);
            buffer.Write(20);
            recorder.Record(buffer.AsStream());
            buffer.Clear();
            buffer.Write(30);
            recorder.Record(buffer.AsStream());

            Assert.AreEqual(3, recorder.Count);
            Assert.AreEqual(10, recorder[0]);
            Assert.AreEqual(20, recorder[1]);
            Assert.AreEqual(30, recorder[2]);
        }

        [Test]
        [Timeout(1000)]
        public void Recorder_RecordFromOwnEvents_RecordsOriginalCountOnly()
        {
            var recorder = new EventRecorder<int>();
            recorder.Record(new EventStream<int>(new[] { 10, 20 }));

            recorder.Record(new EventStream<int>(recorder.Events));

            Assert.AreEqual(4, recorder.Count);
            Assert.AreEqual(10, recorder[0]);
            Assert.AreEqual(20, recorder[1]);
            Assert.AreEqual(10, recorder[2]);
            Assert.AreEqual(20, recorder[3]);
        }

        [Test]
        public void EventSequence_Next_ReturnsIncreasingValues()
        {
            var sequence = new EventSequence();

            Assert.AreEqual(1, sequence.Next());
            Assert.AreEqual(2, sequence.Next());
            Assert.AreEqual(3, sequence.Next());
        }
    }
}
