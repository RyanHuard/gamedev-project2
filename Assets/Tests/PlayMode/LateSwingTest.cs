using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HalloweenDerby.Ball;
using HalloweenDerby.Core;
using HalloweenDerby.Pitching;

namespace HalloweenDerby.Tests
{
    /// Regression test for the exact bug reported: a swing that lands after the ball
    /// geometrically crosses the plate, but still within the pitch's own WeakWindow (i.e. a
    /// legal "late" swing per BallContactResolver's own timing math), used to silently fail -
    /// the ball flipped out of its swingable (Pitching) state the instant it reached the plate,
    /// before the late-swing grace window SwingController itself allows had even started.
    public class LateSwingTest
    {
        [UnityTest]
        public IEnumerator SwingJustAfterPlateCrossing_StillRegistersContact()
        {
            var go = new GameObject("TestBall");
            var ball = go.AddComponent<BallFlightController>();
            var rb = go.GetComponent<Rigidbody>();
            yield return null; // let Awake() run so the private rb field is populated

            var pitch = ScriptableObject.CreateInstance<PitchTypeData>();
            pitch.FlightDuration = 0.5f;
            pitch.WeakWindow = 0.15f;

            ball.LaunchPitch(pitch, new Vector3(0f, 1f, 18f), new Vector3(0f, 1f, 0f));

            // Simulate the ball having traveled to just past the plate, but still inside the
            // late-swing grace window (FlightDuration + WeakWindow), then run one tick to update
            // internal state the same way FixedUpdate would.
            SetPrivate(ball, "pitchElapsed", pitch.FlightDuration + pitch.WeakWindow * 0.4f);
            var tick = typeof(BallFlightController).GetMethod("TickPitchFlight", BindingFlags.NonPublic | BindingFlags.Instance);
            tick.Invoke(ball, null);

            ball.ResolveContact(ContactQuality.Good, Vector3.forward, 20f, 0f);

            Assert.IsFalse(rb.isKinematic,
                "A swing inside the late-swing grace window should register contact (Rigidbody " +
                "should switch to non-kinematic/Hit state) - instead the ball had already left " +
                "its swingable state and silently ignored the swing.");

            Object.DestroyImmediate(go);
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(target, value);
        }
    }
}
