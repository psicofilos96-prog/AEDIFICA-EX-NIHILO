using System;
using Aedifica.Interaction;
using NUnit.Framework;

namespace Aedifica.Tests.EditMode
{
    public sealed class SpatialFaceResizeBenchmarkDiagnosticsTests
    {
        [Test]
        public void TimeoutAppliesAfterTwentyMinutesOfAnIndividualRun()
        {
            Assert.That(SpatialFaceResizeRunContext.RunTimeoutMinutes, Is.EqualTo(20));
            Assert.That(SpatialFaceResizeRunContext.TimedOut(0d), Is.False);
            Assert.That(SpatialFaceResizeRunContext.TimedOut(1200d), Is.False);
            Assert.That(SpatialFaceResizeRunContext.TimedOut(1200.001d), Is.True);
        }

        [Test]
        public void DiagnosticIdentifiesMethodPhaseFrameAndElapsedTime()
        {
            var context = new SpatialFaceResizeRunContext {
                Scenario = "L3", Pieces = 50000, Repeat = 2, Method = "face_linear",
                Phase = "face/BAAB/slot2", Frame = 37, FramesProcessed = 37,
                ElapsedSeconds = 1200.125d
            };
            string message = context.Describe();
            StringAssert.Contains("scenario=L3", message);
            StringAssert.Contains("pieces=50000", message);
            StringAssert.Contains("repeat=2", message);
            StringAssert.Contains("method=face_linear", message);
            StringAssert.Contains("phase=face/BAAB/slot2", message);
            StringAssert.Contains("frame=37", message);
            StringAssert.Contains("framesProcessed=37", message);
            StringAssert.Contains("elapsedSeconds=1200.125", message);
        }

        [Test]
        public void TimeoutWithFrameContextIsNotCountedAsSnapDivergence()
        {
            Assert.That(SpatialFaceResizeRunContext.IsDivergence("face/ABBA/slot1",
                new TimeoutException("frame=37")), Is.False);
            Assert.That(SpatialFaceResizeRunContext.IsDivergence("face/equivalence",
                new InvalidOperationException("frame=37: target differs")), Is.True);
            Assert.That(SpatialFaceResizeRunContext.IsDivergence("face/equivalence",
                new TimeoutException("frame=37")), Is.False);
        }
    }
}
