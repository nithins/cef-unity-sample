using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Xilium.CefGlue;

namespace UnityCef
{
    /// <summary>
    /// Class responsible for creating and updating a CEF browser instance. 
    /// </summary>
    /// <remarks>
    /// Add this to any gameobject having a MeshRenderer or RawImage component.
    /// 
    /// 1. When any Browser instance is in the scene the messagePump needs to be 
    ///    on continuously. One pump is enough for all Browser instances. Hence the
    ///    Pump is switched on by the first browser instance that is created. However,
    ///    it is swithced off by this instance as well. Hence if you have scenarios
    ///    with multiple which are dynamically created/destroyed, this could be an issue. 
    ///    
    /// 2. Other design considerations, see <see cref="UnityCefEngine"/>'s remarks. 
    /// 
    /// </remarks>
    public class UnityCefBrowser : MonoBehaviour
    {


        [Space]
        [SerializeField]
        private Size windowSize = new Size(1280, 720);

        [SerializeField]
        private string url = "http://www.google.com";

        [Space]
        [SerializeField]
        private bool hideScrollbars = false;

        private OffscreenCEFClient cefClient;

        public Texture2D BrowserTexture { get; private set; }


        // Use this for initialization
        void Start()
        {

            this.BrowserTexture = new Texture2D(this.windowSize.Width, this.windowSize.Height, TextureFormat.BGRA32, false);

            if (GetComponent<RawImage>() != null) GetComponent<RawImage>().texture = BrowserTexture;
            if (GetComponent<MeshRenderer>() != null) GetComponent<MeshRenderer>().material.mainTexture = BrowserTexture;

#if !UNITY_EDITOR

            // Instruct CEF to not render to a window.
            CefWindowInfo cefWindowInfo = CefWindowInfo.Create();
            cefWindowInfo.SetAsWindowless(IntPtr.Zero, false);

            // Settings for the browser window itself (e.g. enable JavaScript?).
            CefBrowserSettings cefBrowserSettings = new CefBrowserSettings()
            {
                BackgroundColor = new CefColor(255, 60, 85, 115),
                JavaScript = CefState.Enabled,
                JavaScriptAccessClipboard = CefState.Disabled,
                JavaScriptCloseWindows = CefState.Disabled,
                JavaScriptDomPaste = CefState.Disabled,
                JavaScriptOpenWindows = CefState.Disabled,
                LocalStorage = CefState.Disabled
            };

            // Initialize some of the custom interactions with the browser process.
            this.cefClient = new OffscreenCEFClient(this.windowSize, this.hideScrollbars);

            // Start up the browser instance.
            CefBrowserHost.CreateBrowser(cefWindowInfo, this.cefClient, cefBrowserSettings, string.IsNullOrEmpty(this.url) ? "http://www.google.com" : this.url);
#endif
            StartCoroutine(UpdateTexture());
            StartCoroutine(MessagePump());

        }

        IEnumerator UpdateTexture()
        {
            while (!UnityCefEngine.shouldQuit)
            {
#if !UNITY_EDITOR
                this.cefClient.UpdateTexture(this.BrowserTexture);
#endif
                yield return null;
            }
        }

        static bool messagePumpIsOff = true;

        private IEnumerator MessagePump()
        {
            if (messagePumpIsOff)
            {
                messagePumpIsOff = false;

                while (!UnityCefEngine.shouldQuit)
                {
#if !UNITY_EDITOR
                    CefRuntime.DoMessageLoopWork();
#endif
                    yield return null;
                }
            }
        }


        bool destroyed = false;

        public void Quit()
        {
            StopAllCoroutines();
            if (!destroyed && this.cefClient != null)
                this.cefClient.Shutdown();
            destroyed = true;
            messagePumpIsOff = true;
        }

        private void OnDestroy()
        {
            Quit();
        }

        private void OnApplicationQuit()
        {
            Quit();
        }

    }
}