using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HalloweenDerby.Core;

namespace HalloweenDerby.Tests
{
    /// Not a real assertion-driven test - just a vehicle to capture a screenshot through the
    /// actual play-mode render path (the real scene Camera + CinemachineBrain, rendered to a
    /// RenderTexture) for manual visual review. WaitForEndOfFrame/ScreenCapture don't work in
    /// -batchmode (no real framebuffer surface), so this renders Camera.main directly instead -
    /// that also sidesteps whatever caused an ad-hoc edit-mode temp camera to produce
    /// rainbow-tinted output that didn't match any material's actual configuration.
    public class CameraFramingScreenshotTest
    {
        [UnityTest]
        public IEnumerator CapturePitchZoneCameraView()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            // Let CinemachineBrain position the Main Camera and lighting settle.
            for (int i = 0; i < 10; i++)
                yield return null;

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "No Camera.main found in Derby.unity");

            const int w = 1200, h = 800;
            var rt = new RenderTexture(w, h, 24);
            var previousTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = previousTarget;
            RenderTexture.active = null;

            File.WriteAllBytes("/tmp/playmode_camera_view.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);

            Assert.Pass("Screenshot captured to /tmp/playmode_camera_view.png");
        }
    }
}
