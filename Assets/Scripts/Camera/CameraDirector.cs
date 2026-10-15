using UnityEngine;
using Unity.Cinemachine;
using HalloweenDerby.Core;

namespace HalloweenDerby.CameraSystem
{
    /// Swaps Cinemachine priority between the behind-the-batter camera and a ball-follow
    /// camera on contact, then blends back once the ball resolves (landed / hit a target).
    public class CameraDirector : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera batterCamera;
        [SerializeField] private CinemachineCamera ballFollowCamera;
        [SerializeField] private int activePriority = 20;
        [SerializeField] private int inactivePriority = 5;

        private void OnEnable()
        {
            GameEvents.BallCameraRequested += HandleBallCameraRequested;
            GameEvents.BallCameraReleased += HandleBallCameraReleased;
        }

        private void OnDisable()
        {
            GameEvents.BallCameraRequested -= HandleBallCameraRequested;
            GameEvents.BallCameraReleased -= HandleBallCameraReleased;
        }

        private void Start()
        {
            batterCamera.Priority = activePriority;
            ballFollowCamera.Priority = inactivePriority;
        }

        private void HandleBallCameraRequested(Transform ballTransform)
        {
            ballFollowCamera.Follow = ballTransform;
            ballFollowCamera.LookAt = ballTransform;
            ballFollowCamera.Priority = activePriority;
            batterCamera.Priority = inactivePriority;
        }

        private void HandleBallCameraReleased()
        {
            ballFollowCamera.Priority = inactivePriority;
            batterCamera.Priority = activePriority;
        }
    }
}
