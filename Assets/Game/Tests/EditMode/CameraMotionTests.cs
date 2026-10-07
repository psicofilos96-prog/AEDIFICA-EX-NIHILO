using Aedifica.Interaction.Camera;
using NUnit.Framework;
using UnityEngine;

namespace Aedifica.Tests.EditMode
{
    public sealed class CameraMotionTests
    {
        [Test]
        public void PitchAndDistanceStayWithinConfiguredLimits()
        {
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            motion.Step(new CameraInput { RotatePixels = new Vector2(0f, -10000f), Scroll = 10000f }, 0.016f, settings);
            Assert.That(motion.Pitch, Is.EqualTo(settings.pitchMax));
            Assert.That(motion.Distance, Is.EqualTo(settings.zoomMinDistance));
            motion.Step(new CameraInput { RotatePixels = new Vector2(0f, 10000f), Scroll = -10000f }, 0.016f, settings);
            Assert.That(motion.Pitch, Is.EqualTo(settings.pitchMin));
            Assert.That(motion.Distance, Is.EqualTo(settings.zoomMaxDistance));
        }

        [Test]
        public void OneWheelStepMakesUsefulProgressWithoutOvershooting()
        {
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            motion.Step(new CameraInput { Scroll = 1f }, 0.016f, settings);
            Assert.That(motion.Distance, Is.InRange(18f, 19f));
            Assert.That(motion.Distance, Is.GreaterThan(settings.zoomMinDistance));
        }

        [Test]
        public void WheelZoomReachesNearMinimumProgressivelyAndCanMoveAwayFromIt()
        {
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 2f, settings);
            float wheelStep = CameraScrollProcessor.Process(1f, out _);
            float previous = motion.Distance;
            for (int i = 0; i < 10; i++)
            {
                motion.Step(new CameraInput { Scroll = wheelStep }, 0.016f, settings);
                Assert.That(motion.Distance, Is.InRange(settings.zoomMinDistance, previous));
                Assert.That(float.IsNaN(motion.Distance) || float.IsInfinity(motion.Distance), Is.False);
                previous = motion.Distance;
            }
            Assert.That(motion.Distance, Is.EqualTo(0.5f).Within(0.000001f));
            motion.Step(new CameraInput { Scroll = wheelStep }, 0.016f, settings);
            Assert.That(motion.Distance, Is.EqualTo(0.5f).Within(0.000001f));
            motion.Step(new CameraInput { Scroll = -wheelStep }, 0.016f, settings);
            Assert.That(motion.Distance, Is.GreaterThan(0.5f));
            Assert.That(motion.Distance, Is.LessThan(1f));
        }

        [Test]
        public void RightDragRotatesWithoutPanning()
        {
            var input = new CameraInput();
            CameraInputReader.AssignMouseDrag(ref input, true, true, new Vector2(50f, -20f));
            Assert.That(input.RotatePixels, Is.EqualTo(new Vector2(50f, -20f)));
            Assert.That(input.PanPixels, Is.EqualTo(Vector2.zero));
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            motion.Step(input, 0.016f, settings);
            Assert.That(motion.Yaw, Is.EqualTo(10f).Within(0.001f));
            Assert.That(motion.Pitch, Is.EqualTo(49f).Within(0.001f));
            Assert.That(motion.Focus, Is.EqualTo(Vector3.zero));
        }

        [TestCase(40f, 0f, 7.2f, 45f)]
        [TestCase(0f, 40f, 0f, 37.8f)]
        public void RmbChangesOnlyRequestedAngle(float x, float y, float expectedYaw, float expectedPitch)
        {
            var settings = new CameraSettings { smoothing = 0f };
            var focus = new Vector3(5f, 0f, -7f);
            var motion = new CameraMotion(focus, 0f, 45f, 25f, settings);
            motion.BeginOrbit();
            motion.Step(new CameraInput { RotatePixels = new Vector2(x, y) }, 0.016f, settings);
            Assert.That(motion.Yaw, Is.EqualTo(expectedYaw).Within(0.0001f));
            Assert.That(motion.Pitch, Is.EqualTo(expectedPitch).Within(0.0001f));
            Assert.That(motion.Focus, Is.EqualTo(focus));
            Assert.That(motion.Distance, Is.EqualTo(25f));
        }

        [Test]
        public void RotationCaptureWaitsForStableLockRegardlessOfWarpDuration()
        {
            var filter = new RotationCaptureFilter();
            Vector2 center = new Vector2(400f, 300f);
            filter.BeginCapture();
            foreach (int frames in new[] { 1, 2, 5, 12 })
            {
                filter.BeginCapture();
                for (int i = 0; i < frames; i++)
                    Assert.That(filter.Filter(new Vector2(-50f, 23f), center, center, true), Is.EqualTo(Vector2.zero));
                Assert.That(filter.Filter(Vector2.zero, center, center, true), Is.EqualTo(Vector2.zero));
                Assert.That(filter.Filter(new Vector2(20f, -10f), center, center, true), Is.EqualTo(new Vector2(20f, -10f)));
                filter.Reset();
            }
            Assert.That(filter.Filter(new Vector2(20f, -10f), center, center, false), Is.EqualTo(Vector2.zero));
            Assert.That(filter.Filter(Vector2.zero, center, center, true), Is.EqualTo(Vector2.zero));
            Assert.That(filter.Filter(Vector2.right, center, center, true), Is.EqualTo(Vector2.right));
            filter.Reset();
            Assert.That(filter.Filter(Vector2.right, center, center, true), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void RmbStartPositionDoesNotChangeRotationOrNavigationCenter()
        {
            var settings = new CameraSettings();
            var starts = new[] { new Vector2(400f, 300f), new Vector2(0f, 0f),
                new Vector2(800f, 0f), new Vector2(0f, 600f), new Vector2(800f, 600f) };
            foreach (Vector2 start in starts)
            {
                var motion = new CameraMotion(new Vector3(3f, 0f, -2f), 0f, 45f, 25f, settings);
                Vector3 focus = motion.Focus;
                float distance = motion.Distance;
                motion.BeginOrbit();
                var filter = new RotationCaptureFilter();
                filter.BeginCapture();
                Vector2 warp = new Vector2(400f, 300f) - start;
                for (int i = 0; i < 5; i++)
                    motion.Step(new CameraInput { RotatePixels = filter.Filter(warp, new Vector2(400f, 300f), new Vector2(400f, 300f), true) }, 0.016f, settings);
                Assert.That(motion.Yaw, Is.Zero);
                Assert.That(motion.Pitch, Is.EqualTo(45f));
                Assert.That(motion.Focus, Is.EqualTo(focus));
                Assert.That(motion.Distance, Is.EqualTo(distance));
                filter.Filter(Vector2.zero, new Vector2(400f, 300f), new Vector2(400f, 300f), true);
                motion.Step(new CameraInput { RotatePixels = filter.Filter(new Vector2(20f, -10f), new Vector2(400f, 300f), new Vector2(400f, 300f), true) }, 0.016f, settings);
                Assert.That(motion.Yaw, Is.EqualTo(3.6f).Within(0.001f));
                Assert.That(motion.Pitch, Is.EqualTo(46.8f).Within(0.001f));
                Assert.That(motion.Focus, Is.EqualTo(focus));
                Assert.That(motion.Distance, Is.EqualTo(distance));
                motion.EndOrbit();
            }
        }

        [Test]
        public void LeftDragPansWithoutRotation()
        {
            var input = new CameraInput();
            CameraInputReader.AssignMouseDrag(ref input, false, true, new Vector2(50f, -20f));
            Assert.That(input.RotatePixels, Is.EqualTo(Vector2.zero));
            Assert.That(input.PanPixels, Is.EqualTo(new Vector2(50f, -20f)));
        }

        [Test]
        public void MiddleDragDoesNotPan()
        {
            var input = new CameraInput();
            CameraInputReader.AssignMouseDrag(ref input, false, false, new Vector2(50f, -20f));
            Assert.That(input.PanPixels, Is.EqualTo(Vector2.zero));
            Assert.That(input.RotatePixels, Is.EqualTo(Vector2.zero));
        }

        [TestCase(1f, 0.75f, CameraScrollRegime.Wheel)]
        [TestCase(-1f, -0.75f, CameraScrollRegime.Wheel)]
        [TestCase(0.025f, 0.025f / 120f, CameraScrollRegime.Precision)]
        [TestCase(-0.025f, -0.025f / 120f, CameraScrollRegime.Precision)]
        [TestCase(0f, 0f, CameraScrollRegime.Precision)]
        public void ScrollProcessingMatchesMeasuredRegimes(float raw, float expected, CameraScrollRegime expectedRegime)
        {
            float processed = CameraScrollProcessor.Process(raw, out var regime);
            Assert.That(processed, Is.EqualTo(expected).Within(0.000001f));
            Assert.That(regime, Is.EqualTo(expectedRegime));
        }

        [Test]
        public void ScrollTransitionIsContinuousAndPreservesSign()
        {
            float previous = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float raw = i / 100f;
                float positive = CameraScrollProcessor.Process(raw, out _);
                float negative = CameraScrollProcessor.Process(-raw, out _);
                Assert.That(positive, Is.GreaterThan(previous));
                Assert.That(negative, Is.EqualTo(-positive).Within(0.000001f));
                previous = positive;
            }
            Assert.That(CameraScrollProcessor.Process(0.5f, out var regime), Is.GreaterThan(0f));
            Assert.That(regime, Is.EqualTo(CameraScrollRegime.Transition));
        }

        [Test]
        public void UnexpectedScrollValuesStayFiniteAndZoomLimitsHold()
        {
            Assert.That(CameraScrollProcessor.Process(float.NaN, out _), Is.Zero);
            Assert.That(CameraScrollProcessor.Process(float.PositiveInfinity, out _), Is.Zero);
            Assert.That(CameraScrollProcessor.Process(1000f, out _), Is.EqualTo(3f));
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            for (int i = 0; i < 100; i++)
                motion.Step(new CameraInput { Scroll = CameraScrollProcessor.Process(1f, out _) }, 0.016f, settings);
            Assert.That(motion.Distance, Is.EqualTo(settings.zoomMinDistance));
            for (int i = 0; i < 100; i++)
                motion.Step(new CameraInput { Scroll = CameraScrollProcessor.Process(-1f, out _) }, 0.016f, settings);
            Assert.That(motion.Distance, Is.EqualTo(settings.zoomMaxDistance));
        }

        [Test]
        public void SpeedIncreasesWithDistance()
        {
            var settings = new CameraSettings();
            settings.Normalize();
            Assert.That(CameraMotion.MoveSpeed(settings.zoomMinDistance, settings), Is.EqualTo(settings.moveSpeedMin));
            Assert.That(CameraMotion.MoveSpeed(settings.zoomMaxDistance, settings), Is.EqualTo(settings.moveSpeedMax));
        }

        [Test]
        public void MovementIsHorizontalAndUsesYaw()
        {
            Vector3 direction = CameraMotion.HorizontalMove(Vector2.up, 90f);
            Assert.That(direction.y, Is.EqualTo(0f));
            Assert.That(direction.x, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WasdReachesRearDomeRegionEvenWhenZoomedIn()
        {
            var settings = new CameraSettings { smoothing = 0f, moveSpeedMin = 8f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            float wheelStep = CameraScrollProcessor.Process(1f, out _);
            for (int i = 0; i < 20; i++)
                motion.Step(new CameraInput { Scroll = wheelStep }, 0.016f, settings);
            Assert.That(motion.Focus, Is.EqualTo(Vector3.zero), "Wheel zoom must not move the navigation center toward the dome.");
            Assert.That(motion.Distance, Is.EqualTo(0.5f));
            var rearDome = new Vector3(12f, 0f, 27f);
            var direction = new Vector2(rearDome.x, rearDome.z).normalized;
            motion.Step(new CameraInput { Move = direction }, 4f, settings);
            Assert.That(Vector3.Distance(motion.Focus, rearDome), Is.LessThan(3f));
            Assert.That(motion.Distance, Is.EqualTo(0.5f));
            Assert.That(motion.Pitch, Is.EqualTo(45f));
            Assert.That(motion.Yaw, Is.Zero);
        }

        [Test]
        public void WasdContinuesPastRearDomeWithoutSpatialClamp()
        {
            var settings = new CameraSettings { smoothing = 0f, moveSpeedMin = 8f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, settings.zoomMinDistance, settings);
            float previousZ = motion.Focus.z;
            for (int i = 0; i < 60; i++)
            {
                motion.Step(new CameraInput { Move = Vector2.up }, 1f, settings);
                Assert.That(motion.Focus.z, Is.GreaterThan(previousZ));
                Assert.That(motion.Focus.x, Is.Zero);
                Assert.That(motion.Focus.y, Is.Zero);
                previousZ = motion.Focus.z;
            }
            Assert.That(motion.Focus.z, Is.GreaterThan(150f));
            Assert.That(motion.Distance, Is.EqualTo(0.5f));
        }

        [Test]
        public void WasdAndKeyboardYawPreservePitchAndDistance()
        {
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            motion.Step(new CameraInput { Move = Vector2.up, KeyboardYaw = 1f }, 1f, settings);
            Assert.That(motion.Yaw, Is.EqualTo(settings.keyboardYawSpeed));
            Assert.That(motion.Focus.y, Is.Zero);
            Assert.That(motion.Focus.x, Is.GreaterThan(0f));
            Assert.That(motion.Pitch, Is.EqualTo(45f));
            Assert.That(motion.Distance, Is.EqualTo(25f));
        }

        [TestCase(0f, 15f, 50f, 0f)]
        [TestCase(0f, 80f, 0f, 50f)]
        [TestCase(90f, 45f, 50f, 0f)]
        [TestCase(135f, 60f, 0f, 50f)]
        public void ScreenPanUsesCameraRightAndUp(float yaw, float pitch, float x, float y)
        {
            var settings = new CameraSettings { smoothing = 0f };
            var motion = new CameraMotion(Vector3.zero, yaw, pitch, 25f, settings);
            motion.Step(new CameraInput { PanPixels = new Vector2(x, y) }, 0.016f, settings);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 expected = -(rotation * new Vector3(x, y, 0f)) * (settings.panSpeed * 25f);
            Assert.That(Vector3.Distance(motion.Focus, expected), Is.LessThan(0.0001f));
            Assert.That(Vector3.Dot(motion.Focus, rotation * Vector3.forward), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(motion.Yaw, Is.EqualTo(yaw));
            Assert.That(motion.Pitch, Is.EqualTo(pitch));
            Assert.That(motion.Distance, Is.EqualTo(25f));
        }

        [TestCase(50f, 0f)]
        [TestCase(-50f, 0f)]
        [TestCase(0f, 50f)]
        [TestCase(0f, -50f)]
        [TestCase(50f, 50f)]
        [TestCase(-50f, -50f)]
        public void ScreenPanMovesWorldPointWithMouseOnBothScreenAxes(float x, float y)
        {
            var objectWithCamera = new GameObject("Screen pan projection test");
            try
            {
                var camera = objectWithCamera.AddComponent<UnityEngine.Camera>();
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                var settings = new CameraSettings { smoothing = 0f };
                var motion = new CameraMotion(Vector3.zero, 35f, 45f, 25f, settings);
                camera.transform.SetPositionAndRotation(motion.Position, motion.Rotation);
                Vector3 before = camera.WorldToScreenPoint(Vector3.zero);
                motion.Step(new CameraInput { PanPixels = new Vector2(x, y) }, 0.016f, settings);
                camera.transform.SetPositionAndRotation(motion.Position, motion.Rotation);
                Vector3 after = camera.WorldToScreenPoint(Vector3.zero);
                if (x == 0f) Assert.That(after.x - before.x, Is.EqualTo(0f).Within(0.001f));
                else Assert.That((after.x - before.x) * x, Is.GreaterThan(0f));
                if (y == 0f) Assert.That(after.y - before.y, Is.EqualTo(0f).Within(0.001f));
                else Assert.That((after.y - before.y) * y, Is.GreaterThan(0f));
            }
            finally { Object.DestroyImmediate(objectWithCamera); }
        }

        [TestCase(15f)]
        [TestCase(80f)]
        public void ScreenPanIsFiniteAtPitchLimitsAndBoundedByZoomDistance(float pitch)
        {
            var settings = new CameraSettings { smoothing = 0f };
            var near = new CameraMotion(Vector3.zero, 35f, pitch, settings.zoomMinDistance, settings);
            var far = new CameraMotion(Vector3.zero, 35f, pitch, settings.zoomMaxDistance, settings);
            var input = new CameraInput { PanPixels = new Vector2(100f, 100f) };
            near.Step(input, 0.016f, settings);
            far.Step(input, 0.016f, settings);
            Assert.That(near.Focus.magnitude, Is.EqualTo(Mathf.Sqrt(2f) * 100f * settings.panSpeed * settings.zoomMinDistance).Within(0.0001f));
            Assert.That(far.Focus.magnitude, Is.EqualTo(Mathf.Sqrt(2f) * 100f * settings.panSpeed * settings.zoomMaxDistance).Within(0.0001f));
            Assert.That(float.IsNaN(far.Focus.x) || float.IsInfinity(far.Focus.x), Is.False);
        }

        [Test]
        public void ScreenPanDependsOnPixelDeltaAndNotStartingCursorPosition()
        {
            var settings = new CameraSettings { smoothing = 0f };
            Vector3 expected = CameraMotion.ScreenPan(new Vector2(20f, -15f), 35f, 45f, 25f, settings);
            foreach (Vector2 start in new[] { Vector2.zero, new Vector2(400f, 300f), new Vector2(800f, 600f) })
            {
                var motion = new CameraMotion(Vector3.zero, 35f, 45f, 25f, settings);
                motion.Step(new CameraInput { PanPixels = (start + new Vector2(20f, -15f)) - start }, 0.016f, settings);
                Assert.That(Vector3.Distance(motion.Focus, expected), Is.LessThan(0.0001f));
            }
        }

        [TestCase(0f, 45f)]
        [TestCase(90f, 45f)]
        [TestCase(45f, 70f)]
        public void OrbitChangesPositionAroundFixedFocus(float yaw, float pitch)
        {
            var settings = new CameraSettings { smoothing = 0f };
            var focus = new Vector3(3f, 0f, -2f);
            var motion = new CameraMotion(focus, 0f, 45f, 25f, settings);
            motion.Step(new CameraInput { RotatePixels = new Vector2(yaw / settings.yawSpeed,
                (45f - pitch) / settings.pitchSpeed) }, 0.016f, settings);
            Assert.That(motion.Focus, Is.EqualTo(focus));
            Assert.That(motion.Distance, Is.EqualTo(25f));
            Assert.That((motion.Position - focus).magnitude, Is.EqualTo(25f).Within(0.001f));
            Assert.That(motion.Rotation.eulerAngles.z, Is.EqualTo(0f).Within(0.001f));
        }

        [TestCase(80f, 0f)]
        [TestCase(0f, -60f)]
        [TestCase(80f, -60f)]
        public void RmbRotationPreservesFocusAndDistanceAcrossGestures(float horizontal, float vertical)
        {
            var settings = new CameraSettings();
            var motion = new CameraMotion(new Vector3(3f, 0f, -2f), 0f, 45f, 25f, settings);
            Vector3 focus = motion.Focus;
            float distance = motion.Distance;
            for (int gesture = 0; gesture < 3; gesture++)
            {
                motion.BeginOrbit();
                Assert.That(motion.Focus, Is.EqualTo(focus));
                Assert.That(motion.Distance, Is.EqualTo(distance));
                for (int i = 0; i < 4; i++)
                    motion.Step(new CameraInput { RotatePixels = new Vector2(horizontal / 4f, vertical / 4f) }, 0.016f, settings);
                motion.EndOrbit();
                Assert.That(motion.Focus, Is.EqualTo(focus));
                Assert.That(motion.Distance, Is.EqualTo(distance));
                Assert.That(Vector3.Distance(motion.Position, focus), Is.EqualTo(distance).Within(0.001f));
            }
        }

        [Test]
        public void RmbRotationDependsOnAccumulatedDragNotFrameDuration()
        {
            var settings = new CameraSettings();
            var one = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            var many = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            one.BeginOrbit();
            many.BeginOrbit();
            one.Step(new CameraInput { RotatePixels = new Vector2(50f, -20f) }, 0.1f, settings);
            for (int i = 0; i < 10; i++)
                many.Step(new CameraInput { RotatePixels = new Vector2(5f, -2f) }, 0.01f, settings);
            Assert.That(Vector3.Distance(one.Position, many.Position), Is.LessThan(0.001f));
            Assert.That(one.Focus, Is.EqualTo(many.Focus));
        }

        [TestCase(15f)]
        [TestCase(80f)]
        public void SmallRmbDragHasBoundedAngularAndSpatialResponseAtPitchLimits(float pitch)
        {
            var settings = new CameraSettings();
            var motion = new CameraMotion(Vector3.zero, 0f, pitch, 25f, settings);
            motion.BeginOrbit();
            Vector3 before = motion.Position;
            motion.Step(new CameraInput { RotatePixels = new Vector2(2f, -2f) }, 0.016f, settings);
            Assert.That(Mathf.Abs(motion.Yaw), Is.LessThanOrEqualTo(0.4f + 0.001f));
            Assert.That(Mathf.Abs(motion.Pitch - pitch), Is.LessThanOrEqualTo(0.4f + 0.001f));
            Assert.That(Vector3.Distance(before, motion.Position), Is.LessThan(0.25f));
            Assert.That(Vector3.Distance(motion.Position, motion.Focus), Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void InvalidSettingsAndInputRemainFinite()
        {
            var settings = new CameraSettings { zoomMinDistance = float.NaN, zoomMaxDistance = float.NegativeInfinity,
                pitchMin = float.NaN, pitchMax = float.PositiveInfinity, moveSpeedMin = float.NaN,
                moveSpeedMax = float.PositiveInfinity, smoothing = float.NaN };
            var motion = new CameraMotion(Vector3.zero, float.NaN, float.NaN, float.NaN, settings);
            motion.Step(new CameraInput { Scroll = float.NaN, Move = new Vector2(float.NaN, float.PositiveInfinity) }, 0.016f, settings);
            Assert.That(float.IsNaN(motion.Position.x) || float.IsInfinity(motion.Position.x), Is.False);
            Assert.That(motion.Distance, Is.GreaterThanOrEqualTo(settings.zoomMinDistance));
            Assert.That(motion.Pitch, Is.InRange(settings.pitchMin, settings.pitchMax));
        }
    }
}
