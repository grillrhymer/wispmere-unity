using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wispmere;

namespace Wispmere.Editor
{
    [InitializeOnLoad]
    public static class ArtDirectionCameraPlayModeTest
    {
        private const string RunningKey = "Wispmere.ArtCameraTest.Running";
        private const string ClockStartedKey = "Wispmere.ArtCameraTest.ClockStarted";
        private const string PhaseKey = "Wispmere.ArtCameraTest.Phase";
        private const string StartTimeKey = "Wispmere.ArtCameraTest.StartTime";
        private const string ScenePath = "Assets/Wispmere/Scenes/ART_DIRECTION_TEST.unity";

        private static Vector3 _initialCameraPosition;
        private static Vector3 _initialTargetPosition;
        private static Quaternion _initialCameraRotation;
        private static float _initialTargetYaw;

        static ArtDirectionCameraPlayModeTest()
        {
            EditorApplication.update -= Update;
            EditorApplication.update += Update;
        }

        [MenuItem("Wispmere/Test/Run Art Direction Camera Play Mode Test")]
        public static void Run()
        {
            if (SessionState.GetBool(RunningKey, false))
            {
                Debug.LogError("[Wispmere] The art direction camera test is already running.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetInt(PhaseKey, 0);
            SessionState.SetBool(ClockStartedKey, false);
            SessionState.SetBool(RunningKey, true);
            Debug.Log("[Wispmere] Starting the art direction camera Play Mode test.");
            EditorApplication.isPlaying = true;
        }

        private static void Update()
        {
            if (!SessionState.GetBool(RunningKey, false) || !EditorApplication.isPlaying) return;

            try
            {
                if (!SessionState.GetBool(ClockStartedKey, false))
                {
                    SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
                    SessionState.SetBool(ClockStartedKey, true);
                }

                if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) > 15.0)
                    throw new TimeoutException("Timed out during art direction camera test phase "
                        + SessionState.GetInt(PhaseKey, 0) + ".");

                switch (SessionState.GetInt(PhaseKey, 0))
                {
                    case 0:
                        CheckInitialSceneAndFrame();
                        SetPhase(1);
                        break;
                    case 1:
                        CheckFollowAndRotation();
                        Finish(true, "Player tracking, smooth rotation, terrain framing, and all eight visual subjects passed.");
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected art direction camera test phase.");
                }
            }
            catch (Exception exception)
            {
                Finish(false, exception.ToString());
            }
        }

        private static void CheckInitialSceneAndFrame()
        {
            var camera = UnityEngine.Object.FindAnyObjectByType<ArtDirectionCameraFollow>();
            Require(camera != null && camera.target != null, "The follow camera or target is missing in Play Mode.");
            var viewCamera = camera.GetComponent<Camera>();
            Require(viewCamera != null && viewCamera.allowMSAA && !viewCamera.allowHDR,
                "The presentation camera does not have the expected clean MSAA rendering settings.");
            Require(viewCamera.nearClipPlane >= 0.1f && viewCamera.farClipPlane <= 100f,
                "Presentation camera clipping planes are outside the expected range.");
            Require(camera.pitch >= 35f && camera.pitch <= 50f && camera.distance >= 18f,
                "The camera is not at the intended pulled-back action-RPG angle.");

            string[] subjects =
            {
                "Player Character - Existing Customizer",
                "NPC - Tansy, Wayfinder",
                "House - Crooked Lantern Cottage",
                "Tree - Turquoise Crown",
                "Rock - Faceted Slate",
                "Resource Node - Moonstone Ore",
                "Grass And Ground Cover",
                "Magical Plant - Lantern Bloom"
            };

            Transform root = GameObject.Find("Eight Core Style Tests").transform;
            foreach (string subjectName in subjects)
            {
                Transform subject = root.Find(subjectName);
                Require(subject != null, "A core visual subject is missing: " + subjectName);
                Require(IsVisible(viewCamera, subject), "A core visual subject is outside the camera frame: " + subjectName);
            }

            _initialCameraPosition = camera.transform.position;
            _initialTargetPosition = camera.target.position;
            _initialCameraRotation = camera.transform.rotation;
            _initialTargetYaw = camera.target.eulerAngles.y;
            camera.target.position += new Vector3(2f, 0f, 1f);
            camera.target.rotation = Quaternion.Euler(0f, _initialTargetYaw + 55f, 0f);
            Physics.SyncTransforms();
        }

        private static void CheckFollowAndRotation()
        {
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(StartTimeKey, 0f) < 1.4)
                return;

            var camera = UnityEngine.Object.FindAnyObjectByType<ArtDirectionCameraFollow>();
            Require(camera != null && camera.target != null, "Follow camera disappeared during Play Mode.");
            Require(Vector3.Distance(camera.transform.position, _initialCameraPosition) > 1f,
                "The camera did not follow the moved player.");
            Require(Vector3.Distance(camera.target.position, _initialTargetPosition) > 2f,
                "The test player did not move as expected.");
            Require(Quaternion.Angle(_initialCameraRotation, camera.transform.rotation) > 10f,
                "Camera rotation did not ease toward the turned player.");
            Require(Vector3.Distance(camera.transform.position, camera.target.position) > camera.minimumDistance,
                "Camera collision handling pulled the camera inside its minimum safe distance.");
        }

        private static bool IsVisible(Camera camera, Transform subject)
        {
            var renderers = subject.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                if (!renderer.enabled) continue;
                Vector3 point = camera.WorldToViewportPoint(renderer.bounds.center);
                if (point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f)
                    return true;
            }
            return false;
        }

        private static void SetPhase(int phase)
        {
            SessionState.SetInt(PhaseKey, phase);
            SessionState.SetFloat(StartTimeKey, (float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(ClockStartedKey, true);
        }

        private static void Finish(bool success, string message)
        {
            SessionState.SetBool(RunningKey, false);
            if (success) Debug.Log("[Wispmere] Art direction camera test passed. " + message);
            else Debug.LogError("[Wispmere] Art direction camera test failed. " + message);
            EditorApplication.isPlaying = false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Wispmere] " + message);
        }
    }
}
