using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;

using RenderBitmapAtlas = FamiStudio.GLBitmapAtlas;
using RenderBrush = FamiStudio.GLBrush;
using RenderGeometry = FamiStudio.GLGeometry;
using RenderControl = FamiStudio.GLControl;
using RenderGraphics = FamiStudio.GLGraphics;
using RenderCommandList = FamiStudio.GLCommandList;

namespace FamiStudio
{
    public class LinkLabel2 : RenderControl
    {
        private string text;
        private string url;
        private bool hover;
        private int lineOffset = DpiScaling.ScaleForMainWindow(4);

        public LinkLabel2(string txt, string link)
        {
            text = txt;
            url = link;
            height = DpiScaling.ScaleForMainWindow(24);
        }

        protected override void OnMouseMove(MouseEventArgs2 e)
        {
            // MATTT : Need hand cursor here!
            var insideText = e.X >= 0 && e.X < MeasureString();
            Cursor.Current = insideText ? Cursors.CopyCursor : Cursors.Default;
            SetAndMarkDirty(ref hover, insideText);
        }

        protected override void OnMouseLeave(System.EventArgs e)
        {
            Cursor.Current = Cursors.Default;
            SetAndMarkDirty(ref hover, false);
        }

        private int MeasureString()
        {
            return ThemeResources.FontMedium.MeasureString(text, false);
        }

        protected override void OnMouseDown(MouseEventArgs2 e)
        {
            if (e.Left)
                PlatformUtils.OpenUrl(url);
        }

        protected override void OnRender(RenderGraphics g)
        {
            Debug.Assert(enabled); // TODO : Add support for disabled state.

            var c = parentDialog.CommandList;
            var sx = MeasureString();
            var brush = hover ? ThemeResources.LightGreyFillBrush2 : ThemeResources.LightGreyFillBrush1;

            c.DrawText(text, ThemeResources.FontMedium, 0, 0, brush, RenderTextFlags.MiddleLeft, 0, height);
            c.DrawLine(0, height  - lineOffset, sx, height - lineOffset, brush);
        }
    }
}
