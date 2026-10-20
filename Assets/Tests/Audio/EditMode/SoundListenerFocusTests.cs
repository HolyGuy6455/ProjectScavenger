using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Scavenger.Audio.Tests
{
    public sealed class SoundListenerFocusTests
    {
        private GameObject cameraObject;
        private Camera camera;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("SoundListenerFocus test camera");
            camera = cameraObject.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OffsetCameraUsesVisibleGroundPointForPerspectiveAndOrthographicViews(bool orthographic)
        {
            camera.orthographic = orthographic;
            camera.orthographicSize = 7;
            camera.transform.position = new Vector3(10, 13, -10);
            Vector3 visibleGroundPoint = new Vector3(2, 3, 6);
            camera.transform.LookAt(visibleGroundPoint);
            Vector3 playerFallback = new Vector3(-40, 3, 50);

            Vector3 focus = SoundListenerFocus.Calculate(camera, playerFallback);

            Assert.That(Vector3.Distance(focus, visibleGroundPoint), Is.LessThan(0.001f),
                "Attenuation must follow the visible center on the player's ground plane, including camera offset.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParallelOrUpwardViewUsesFallback(bool upward)
        {
            camera.transform.position = new Vector3(10, 13, -10);
            camera.transform.rotation = Quaternion.LookRotation(upward ? new Vector3(0, 1, 1) : Vector3.forward);
            Vector3 fallback = new Vector3(4, 3, 6);

            Assert.That(SoundListenerFocus.Calculate(camera, fallback), Is.EqualTo(fallback));
        }

        [Test]
        public void MissingCameraUsesFallback()
        {
            Vector3 fallback = new Vector3(4, 3, 6);
            Assert.That(SoundListenerFocus.Calculate(null, fallback), Is.EqualTo(fallback));
        }
    }
}
