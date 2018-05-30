using Aleab.CefUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Xilium.CefGlue;

[RequireComponent(typeof(MeshRenderer))]
public class Browser : MonoBehaviour {


    [Space]
    [SerializeField]
    private Aleab.CefUnity.Structs.Size windowSize = new Aleab.CefUnity.Structs.Size(1280, 720);

    [SerializeField]
    private string url = "http://www.google.com";

    [Space]
    [SerializeField]
    private bool hideScrollbars = false;

    private OffscreenCEFClient cefClient;

    public Texture2D BrowserTexture { get; private set; }


    // Use this for initialization
    void Start () {

        this.BrowserTexture = new Texture2D(this.windowSize.Width, this.windowSize.Height, TextureFormat.BGRA32, false);
        this.GetComponent<MeshRenderer>().material.mainTexture = this.BrowserTexture;

#if UNITY_EDITOR
        return;
#endif



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

        StartCoroutine(UpdateTexture());
        StartCoroutine(MessagePump());

    }

    IEnumerator UpdateTexture()
    {
        while (!OffscreenCEF.shouldQuit)
        {
            this.cefClient.UpdateTexture(this.BrowserTexture);
            yield return null;
        }
    }

    static bool messagePumpIsOff = true;

    private IEnumerator MessagePump()
    {
        if (messagePumpIsOff)
        {
            messagePumpIsOff = false;

            while (!OffscreenCEF.shouldQuit)
            {
                CefRuntime.DoMessageLoopWork();
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
