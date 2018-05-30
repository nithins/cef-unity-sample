using Aleab.CefUnity.Structs;
using System;
using System.Collections;
using UnityEngine;
using Xilium.CefGlue;

namespace Aleab.CefUnity
{
    [DisallowMultipleComponent]
    public class OffscreenCEF : MonoBehaviour
    {

        public static bool shouldQuit = false;
        static bool cefStarted = false;

        bool iStartedCef = false;

        private void Awake()
        {
            this.StartCef();
        }



        private void OnDestroy()
        {
            this.Quit();
        }

        private void StartCef()
        {
#if UNITY_EDITOR
            return;
#endif

            if (cefStarted)
                return;
            
#if UNITY_EDITOR
            CefRuntime.Load("./Assets/Plugins/x86_64");
#else
        CefRuntime.Load();
#endif

            var cefMainArgs = new CefMainArgs(new string[] { });
            var cefApp = new OffscreenCEFClient.OffscreenCEFApp();

            // This is where the code path diverges for child processes.
            //if (CefRuntime.ExecuteProcess(cefMainArgs, cefApp, IntPtr.Zero) != -1)
            //    Debug.LogError("Could not start the secondary process.");

            var cefSettings = new CefSettings
            {
                //ExternalMessagePump = true,
                MultiThreadedMessageLoop = false,
                SingleProcess = true,
                LogSeverity = CefLogSeverity.Verbose,
                LogFile = "cef.log",
                WindowlessRenderingEnabled = true,
                NoSandbox = true,
                ExternalMessagePump = true,
            };

            // Start the browser process (a child process).
            CefRuntime.Initialize(cefMainArgs, cefSettings, cefApp, IntPtr.Zero);

            DontDestroyOnLoad(this.gameObject.transform.root.gameObject);

            iStartedCef = true;
            cefStarted = true;
            
        }

        private void Quit()
        {
            if (iStartedCef)
            {
                shouldQuit = true;
                this.StopAllCoroutines();
                CefRuntime.Shutdown();
            }
        }

    }
}