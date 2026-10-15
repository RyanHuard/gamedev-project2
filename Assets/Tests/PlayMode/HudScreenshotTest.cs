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
    /// Not assertion-driven - captures a screenshot once the round has actually started (so the
    /// tutorial panel and HUD text are populated) for manual visual review of the tutorial-box
    /// layout fix and the new score counter.
    public class HudScreenshotTest
    {
        [UnityTest]
        public IEnumerator CaptureHudAfterRoundStart()
        {
            yield return EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Derby.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            GameEvents.RaiseGameStartRequested();

            // RoundManager's delayBeforeFirstPitch is 2s - wait past that so the tutorial text
            // and HUD are actually populated.
            yield return new WaitForSeconds(2.5f);

            Camera cam = Camera.main;
            Assert.IsNotNull(cam, "No Camera.main found in Derby.unity");

            // Camera.Render() into a custom RenderTexture doesn't pick up a Screen Space -
            // Overlay canvas (it isn't tied to any camera) - switch it to Screen Space - Camera
            // just for this capture so the HUD actually shows up in the output.
            var canvasGO = GameObject.Find("HUDCanvas");
            var canvas = canvasGO != null ? canvasGO.GetComponent<Canvas>() : null;
            RenderMode previousMode = default;
            Camera previousWorldCam = null;
            float previousPlaneDistance = 0f;
            if (canvas != null)
            {
                previousMode = canvas.renderMode;
                previousWorldCam = canvas.worldCamera;
                previousPlaneDistance = canvas.planeDistance;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                canvas.planeDistance = 1f;
            }

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

            if (canvas != null)
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousWorldCam;
                canvas.planeDistance = previousPlaneDistance;
            }

            File.WriteAllBytes("/tmp/hud_screenshot.png", tex.EncodeToPNG());
            Object.Destroy(tex);
            Object.Destroy(rt);

            Assert.Pass("Screenshot captured to /tmp/hud_screenshot.png");
        }
    }
}
