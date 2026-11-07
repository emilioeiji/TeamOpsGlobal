using System.Drawing;
using System.Windows.Forms;

namespace TeamOps.UI.Forms;

public sealed class HTMLFormHaidaiPreview : Form
{
    private readonly string _filePath;
    private readonly Microsoft.Web.WebView2.WinForms.WebView2 _webView;

    public HTMLFormHaidaiPreview(string filePath, int referenceWidth, int referenceHeight)
    {
        _filePath = filePath;
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Text = $"Haidai - pré-visualização fiel ({referenceWidth} × {referenceHeight})";
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterParent;

        _webView = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
        Controls.Add(_webView);
        Shown += InitializePreview;
    }

    private async void InitializePreview(object? sender, EventArgs e)
    {
        await _webView.EnsureCoreWebView2Async();
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Navigate(new Uri(_filePath).AbsoluteUri);
    }
}
