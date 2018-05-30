
using UnityEngine;
using Xilium.CefGlue;

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;


namespace UnityCef
{
    /// <summary>
    /// Class responsible for loading and managing the life cycle Xilium.CefGlue. 
    /// </summary>
    /// <remarks>
    /// Add this to an empty game object in every scene that uses <see cref="UnityCefBrowser"/>
    /// 
    /// 
    /// 1. Currently Xilium.CefGlue can only be instantiated once in any process
    ///    Hence, it does not work well in unity editor, causing it to crash on multiple play attempts.
    ///    For this reason, its interfacing with CefGlue is done only on build versions. 
    /// 
    /// 2. Only one instance should ever be created by the entire application. This instance must persist
    ///    across scene loads. Thus it should be attached to an empty gameobject. The first instance that 
    ///    gets created survives across all subsequent scenes. Others remain dummies that dont do anything
    ///    and die along with the scene. 
    /// 
    /// 3. The first unique instance which survives across scenes will shutdown on application quit. However,
    ///    OnApplicationQuit is called before OnDestroy. Browser instance might still be around and they will have 
    ///    to shut down first. Hence they will have to quit first in OnApplicationQuit. And this will quit OnDestroy only. 
    ///    For normal BrowserInstance shutdowns, they will quit OnDestroy as well. Hence, in browser instances, Quit is
    ///    called on OnDestroy and OnApplicationQuit.
    /// </remarks>

    [DisallowMultipleComponent]
    public class UnityCefEngine : MonoBehaviour
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
            if (cefStarted)
                return;
#if !UNITY_EDITOR

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
#endif
            DontDestroyOnLoad(this.gameObject.transform.root.gameObject);

            iStartedCef = true;
            cefStarted = true;
            
        }

        private void Quit()
        {
            if (iStartedCef)
            {
                shouldQuit = true;
#if !UNITY_EDITOR
                CefRuntime.Shutdown();
#endif
            }
        }

    }

    [Serializable]
    public struct Size
    {
        [SerializeField]
        private int _width;

        [SerializeField]
        private int _height;

        public int Width
        {
            get { return this._width; }
            private set { this._width = value; }
        }

        public int Height
        {
            get { return this._height; }
            private set { this._height = value; }
        }

        public float Ratio { get { return (float)this.Width / this.Height; } }

        public Size(int width, int heigth)
        {
            this._width = width;
            this._height = heigth;
        }

        public static Size operator *(int k, Size s)
        {
            return new Size(k * s.Width, k * s.Height);
        }

        public static Size operator *(Size s, int k)
        {
            return k * s;
        }

        public static bool operator ==(Size lhs, Size rhs)
        {
            return lhs.Width == rhs.Width && lhs.Height == rhs.Height;
        }

        public static bool operator !=(Size lhs, Size rhs)
        {
            return !(lhs == rhs);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(Size))
                return false;
            return this == (Size)obj;
        }

        public override int GetHashCode()
        {
            return this.Width ^ this.Height;
        }
    }


    internal class OffscreenCEFClient : CefClient
    {
        private readonly OffscreenLoadHandler _loadHandler;
        private readonly OffscreenRenderHandler _renderHandler;

        private static readonly object sPixelLock = new object();
        private byte[] sPixelBuffer;

        private CefBrowserHost sHost;

        public OffscreenCEFClient(Size windowSize, bool hideScrollbars = false)
        {
            this._loadHandler = new OffscreenLoadHandler(this, hideScrollbars);
            this._renderHandler = new OffscreenRenderHandler(windowSize.Width, windowSize.Height, this);

            this.sPixelBuffer = new byte[windowSize.Width * windowSize.Height * 4];

            Debug.Log("Constructed Offscreen Client");
        }

        public void UpdateTexture(Texture2D pTexture)
        {
            if (this.sHost != null)
            {
                lock (sPixelLock)
                {
                    if (this.sHost != null)
                    {
                        pTexture.LoadRawTextureData(this.sPixelBuffer);
                        pTexture.Apply(false);
                    }
                }
            }
        }

        public void Shutdown()
        {
            if (this.sHost != null)
            {
                Debug.Log("Host Cleanup");
                this.sHost.CloseBrowser(true);
                this.sHost.Dispose();
                this.sHost = null;
            }
        }

#region Interface

        protected override CefRenderHandler GetRenderHandler()
        {
            return this._renderHandler;
        }

        protected override CefLoadHandler GetLoadHandler()
        {
            return this._loadHandler;
        }

#endregion Interface

#region Handlers

        internal class OffscreenLoadHandler : CefLoadHandler
        {
            private OffscreenCEFClient client;
            private bool hideScrollbars;

            public OffscreenLoadHandler(OffscreenCEFClient client, bool hideScrollbars)
            {
                this.client = client;
                this.hideScrollbars = hideScrollbars;
            }

            protected override void OnLoadStart(CefBrowser browser, CefFrame frame, CefTransitionType transitionType)
            {
                if (browser != null)
                    this.client.sHost = browser.GetHost();

                if (frame.IsMain)
                    Debug.LogFormat("START: {0}", browser.GetMainFrame().Url);
            }

            protected override void OnLoadEnd(CefBrowser browser, CefFrame frame, int httpStatusCode)
            {
                if (frame.IsMain)
                {
                    Debug.LogFormat("END: {0}, {1}", browser.GetMainFrame().Url, httpStatusCode.ToString());

                    if (this.hideScrollbars)
                        this.HideScrollbars(frame);
                }
            }

            private void HideScrollbars(CefFrame frame)
            {
                string jsScript = "var head = document.head;" +
                                  "var style = document.createElement('style');" +
                                  "style.type = 'text/css';" +
                                  "style.appendChild(document.createTextNode('::-webkit-scrollbar { visibility: hidden; }'));" +
                                  "head.appendChild(style);";
                frame.ExecuteJavaScript(jsScript, string.Empty, 107);
            }
        }

        internal class OffscreenRenderHandler : CefRenderHandler
        {
            private OffscreenCEFClient client;

            private readonly int _windowWidth;
            private readonly int _windowHeight;

            public OffscreenRenderHandler(int windowWidth, int windowHeight, OffscreenCEFClient client)
            {
                this._windowWidth = windowWidth;
                this._windowHeight = windowHeight;
                this.client = client;
            }

            protected override bool GetRootScreenRect(CefBrowser browser, ref CefRectangle rect)
            {
                return GetViewRect(browser, ref rect);
            }

            protected override bool GetScreenPoint(CefBrowser browser, int viewX, int viewY, ref int screenX, ref int screenY)
            {
                screenX = viewX;
                screenY = viewY;
                return true;
            }

            protected override bool GetViewRect(CefBrowser browser, ref CefRectangle rect)
            {
                rect.X = 0;
                rect.Y = 0;
                rect.Width = this._windowWidth;
                rect.Height = this._windowHeight;
                return true;
            }

            [SecurityCritical]
            protected override void OnPaint(CefBrowser browser, CefPaintElementType type, CefRectangle[] dirtyRects, IntPtr buffer, int width, int height)
            {
                if (browser != null)
                {
                    lock (sPixelLock)
                    {
                        if (browser != null)
                            Marshal.Copy(buffer, this.client.sPixelBuffer, 0, this.client.sPixelBuffer.Length);
                    }
                }
            }

            protected override bool GetScreenInfo(CefBrowser browser, CefScreenInfo screenInfo)
            {
                return false;
            }

            protected override void OnCursorChange(CefBrowser browser, IntPtr cursorHandle, CefCursorType type, CefCursorInfo customCursorInfo)
            {
            }

            protected override void OnPopupSize(CefBrowser browser, CefRectangle rect)
            {
            }

            protected override void OnScrollOffsetChanged(CefBrowser browser, double x, double y)
            {
            }

            protected override void OnImeCompositionRangeChanged(CefBrowser browser, CefRange selectedRange, CefRectangle[] characterBounds)
            {
            }
        }

#endregion Handlers

        public class OffscreenCEFApp : CefApp
        {
            protected override void OnBeforeCommandLineProcessing(string processType, CefCommandLine commandLine)
            {
                Console.WriteLine("OnBeforeCommandLineProcessing: {0} {1}", processType, commandLine);

                // TODO: currently on linux platform location of locales and pack files are determined
                // incorrectly (relative to main module instead of libcef.so module).
                // Once issue http://code.google.com/p/chromiumembedded/issues/detail?id=668 will be resolved this code can be removed.
                if (CefRuntime.Platform == CefRuntimePlatform.Linux)
                {
                    var path = new Uri(Assembly.GetEntryAssembly().CodeBase).LocalPath;
                    path = Path.GetDirectoryName(path);

                    commandLine.AppendSwitch("resources-dir-path", path);
                    commandLine.AppendSwitch("locales-dir-path", Path.Combine(path, "locales"));
                }
            }
        }
    }
}