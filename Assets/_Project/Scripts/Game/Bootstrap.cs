using System;
using UnityEngine;

namespace BooTrayal.Game
{
    /// <summary>First object in the first scene. Also answers the build smoke test (-smoketest).</summary>
    public class Bootstrap : MonoBehaviour
    {
        public const string SmokeArg = "-smoketest";
        public const string SmokeOkMarker = "BOOTRAYAL_SMOKE_OK";

        void Awake()
        {
            // Headless test instances would otherwise spin at thousands of FPS and starve each other.
            if (Application.isBatchMode) Application.targetFrameRate = 60;
        }

        void Start()
        {
            Debug.Log($"BOO-TRAYAL v{Application.version} boot");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), SmokeArg) >= 0)
            {
                Debug.Log(SmokeOkMarker);
                Application.Quit(0);
            }
        }
    }
}
