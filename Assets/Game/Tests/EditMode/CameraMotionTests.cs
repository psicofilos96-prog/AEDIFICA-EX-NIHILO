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

        [TestCase(2f, 0f)]
        [TestCase(0f, 3f)]
        [TestCase(2f, 3f)]
        public void WorldGrabCorrectionKeepsInitialGroundPointUnderCursor(float x, float z)
        {
            var plane = new Plane(Vector3.up, Vector3.zero);
            var first = new Ray(new Vector3(0f, 10f, 0f), Vector3.down);
            var next = new Ray(new Vector3(x, 10f, z), Vector3.down);
            Assert.That(CityBuilderCamera.TryGroundPoint(first, plane, out Vector3 grabbed), Is.True);
            Assert.That(CityBuilderCamera.TryGroundPoint(next, plane, out Vector3 current), Is.True);
            Assert.That(CameraMotion.GrabCorrection(grabbed, current), Is.EqualTo(new Vector3(-x, 0f, -z)));
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

        [Test]
        public void CenterViewportRayFollowsCameraForward()
        {
            var cameraObject = new GameObject("Orbit ray test");
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                cameraObject.transform.SetPositionAndRotation(new Vector3(2f, 10f, -5f), Quaternion.Euler(45f, 30f, 0f));
                Ray ray = CityBuilderCamera.ViewportCenterRay(camera);
                Assert.That(Vector3.Angle(ray.direction, cameraObject.transform.forward), Is.LessThan(0.001f));
            }
            finally { Object.DestroyImmediate(cameraObject); }
        }

        [TestCase(80f, 0f)]
        [TestCase(0f, -60f)]
        [TestCase(80f, -60f)]
        public void RmbOrbitKeepsPivotAndDistanceAndDoesNotJumpOnRelease(float horizontal, float vertical)
        {
            var settings = new CameraSettings();
            var motion = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            Vector3 before = motion.Position;
            Vector3 pivot = before + motion.Rotation * Vector3.forward * 20f;
            motion.BeginOrbit(pivot, settings);
            float distance = Vector3.Distance(before, pivot);
            Assert.That(Vector3.Distance(motion.Position, before), Is.LessThan(0.001f));
            for (int i = 0; i < 4; i++)
                motion.Step(new CameraInput { RotatePixels = new Vector2(horizontal / 4f, vertical / 4f) }, 0.016f, settings);
            Assert.That(motion.OrbitPivot, Is.EqualTo(pivot));
            Assert.That(motion.Focus, Is.EqualTo(pivot));
            Assert.That(Vector3.Distance(motion.Position, pivot), Is.EqualTo(distance).Within(0.001f));
            Assert.That(motion.Rotation.eulerAngles.z, Is.EqualTo(0f).Within(0.001f));
            Vector3 releasePosition = motion.Position;
            motion.EndOrbit();
            motion.Step(default, 0.016f, settings);
            Assert.That(Vector3.Distance(motion.Position, releasePosition), Is.LessThan(0.001f));
            Assert.That(motion.Pitch, Is.InRange(settings.pitchMin, settings.pitchMax));
        }

        [Test]
        public void OrbitResultDependsOnAccumulatedDragNotFrameDuration()
        {
            var settings = new CameraSettings();
            var one = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            var many = new CameraMotion(Vector3.zero, 0f, 45f, 25f, settings);
            one.BeginOrbit(Vector3.zero, settings);
            many.BeginOrbit(Vector3.zero, settings);
            one.Step(new CameraInput { RotatePixels = new Vector2(50f, -20f) }, 0.1f, settings);
            for (int i = 0; i < 10; i++)
                many.Step(new CameraInput { RotatePixels = new Vector2(5f, -2f) }, 0.01f, settings);
            Assert.That(Vector3.Distance(one.Position, many.Position), Is.LessThan(0.001f));
            Assert.That(one.Focus, Is.EqualTo(many.Focus));
        }

        [Test]
        public void OrbitPivotAcceptsNearbySurfaceAndRejectsDistantSurface()
        {
            var origin = new Vector3(0f, 10f, 0f);
            var ray = new Ray(origin, Vector3.down);
            var plane = new Plane(Vector3.up, Vector3.zero);
            Vector3 near = CityBuilderCamera.SelectOrbitPivot(ray, origin, 10f, new Vector3(0f, 0f, 0f), plane, out var nearSource);
            Assert.That(nearSource, Is.EqualTo(CityBuilderCamera.OrbitPivotSource.Surface));
            Assert.That(near, Is.EqualTo(Vector3.zero));
            Vector3 distant = CityBuilderCamera.SelectOrbitPivot(ray, origin, 10f, new Vector3(0f, -1000f, 0f), plane, out var distantSource);
            Assert.That(distantSource, Is.EqualTo(CityBuilderCamera.OrbitPivotSource.NavigationPlane));
            Assert.That(Vector3.Distance(origin, distant), Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void OrbitPivotBoundsNearParallelAndExtremePlaneIntersections()
        {
            var origin = new Vector3(0f, 10f, 0f);
            var plane = new Plane(Vector3.up, Vector3.zero);
            var shallow = new Ray(origin, new Vector3(1f, -0.001f, 0f).normalized);
            Vector3 pivot = CityBuilderCamera.SelectOrbitPivot(shallow, origin, 25f, null, plane, out var source);
            Assert.That(source, Is.EqualTo(CityBuilderCamera.OrbitPivotSource.BoundedRay));
            Assert.That(Vector3.Distance(origin, pivot), Is.EqualTo(25f).Within(0.001f));
            var far = new Ray(origin, new Vector3(1f, -0.2f, 0f).normalized);
            Vector3 farPivot = CityBuilderCamera.SelectOrbitPivot(far, origin, 25f, null, plane, out source);
            Assert.That(source, Is.EqualTo(CityBuilderCamera.OrbitPivotSource.BoundedRay));
            Assert.That(Vector3.Distance(origin, farPivot), Is.EqualTo(25f).Within(0.001f));
            Assert.That(float.IsNaN(farPivot.x) || float.IsInfinity(farPivot.x), Is.False);
        }

        [TestCase(15f)]
        [TestCase(80f)]
        public void SmallOrbitDragHasBoundedAngularAndSpatialResponseAtPitchLimits(float pitch)
        {
            var settings = new CameraSettings();
            var motion = new CameraMotion(Vector3.zero, 0f, pitch, 25f, settings);
            motion.BeginOrbit(Vector3.zero, settings);
            Vector3 before = motion.Position;
            motion.Step(new CameraInput { RotatePixels = new Vector2(2f, -2f) }, 0.016f, settings);
            Assert.That(Mathf.Abs(motion.Yaw), Is.LessThanOrEqualTo(0.4f + 0.001f));
            Assert.That(Mathf.Abs(motion.Pitch - pitch), Is.LessThanOrEqualTo(0.4f + 0.001f));
            Assert.That(Vector3.Distance(before, motion.Position), Is.LessThan(0.25f));
            Assert.That(Vector3.Distance(motion.Position, motion.OrbitPivot), Is.EqualTo(25f).Within(0.001f));
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
