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
