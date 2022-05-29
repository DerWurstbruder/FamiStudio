using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;

using Color     = System.Drawing.Color;
using Point     = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;

using RenderBitmapAtlasRef = FamiStudio.GLBitmapAtlasRef;
using RenderBrush          = FamiStudio.GLBrush;
using RenderFont           = FamiStudio.GLFont;
using RenderControl        = FamiStudio.GLControl;
using RenderGraphics       = FamiStudio.GLGraphics;
using RenderCommandList    = FamiStudio.GLCommandList;

namespace FamiStudio
{
    public class ContextMenu : RenderControl
    {
        const int DefaultItemSizeY    = 22;
        const int DefaultIconPos      = 4;
        const int DefaultTextPosX     = 22;
        const int DefaultMenuMinSizeX = 100;

        int itemSizeY;
        int iconPos;
        int textPosX;
        int minSizeX;

        int hoveredItemIndex = -1;
        RenderBitmapAtlasRef[] bmpExpansions;
        RenderBitmapAtlasRef[] bmpContextMenu;
        RenderBitmapAtlasRef bmpMenuCheckOn;
        RenderBitmapAtlasRef bmpMenuCheckOff;
        RenderBitmapAtlasRef bmpMenuRadio;
        ContextMenuOption[] menuOptions;

        protected override void OnRenderInitialized(RenderGraphics g)
        {
            bmpExpansions   = g.GetBitmapAtlasRefs(ExpansionType.Icons);
            bmpMenuCheckOn  = g.GetBitmapAtlasRef("MenuCheckOn");
            bmpMenuCheckOff = g.GetBitmapAtlasRef("MenuCheckOff");
            bmpMenuRadio    = g.GetBitmapAtlasRef("MenuRadio");
        }

        private void UpdateRenderCoords()
        {
            itemSizeY = ScaleForMainWindow(DefaultItemSizeY);
            iconPos   = ScaleForMainWindow(DefaultIconPos);
            textPosX  = ScaleForMainWindow(DefaultTextPosX);
            minSizeX  = ScaleForMainWindow(DefaultMenuMinSizeX);
        }

        public void Initialize(RenderGraphics g, ContextMenuOption[] options)
        {
            UpdateRenderCoords();

            menuOptions = options;
            bmpContextMenu = new RenderBitmapAtlasRef[options.Length];

            // Measure size.
            var sizeX = 0;
            var sizeY = 0;

            for (int i = 0; i < menuOptions.Length; i++)
            {
                ContextMenuOption option = menuOptions[i];

                sizeX = Math.Max(sizeX, (int)g.MeasureString(option.Text, ThemeResources.FontMedium));
                sizeY += itemSizeY;

                if (!string.IsNullOrEmpty(option.Image))
                {
                    bmpContextMenu[i] = g.GetBitmapAtlasRef(option.Image);
                    Debug.Assert(bmpContextMenu != null);
                }
            }

            width  = Math.Max(minSizeX, sizeX + textPosX) + iconPos; 
            height = sizeY;
        }

        protected int GetIndexAtCoord(int x, int y)
        {
            var idx = -1;

            if (x >= 0 && 
                y >= 0 &&
                x < Width &&
                y < Height)
            {
                idx = Math.Min(y / itemSizeY, menuOptions.Length - 1);;
            }

            return idx;
        }

        protected override void OnMouseDown(MouseEventArgs2 e)
        {
            var itemIndex = GetIndexAtCoord(e.X, e.Y);

            SetHoveredItemIndex(itemIndex);

            if (hoveredItemIndex >= 0)
            {
                App.HideContextMenu();
                MarkDirty();
                menuOptions[hoveredItemIndex].Callback();
            }
        }

        protected override void OnMouseMove(MouseEventArgs2 e)
        {
            var itemIndex = GetIndexAtCoord(e.X, e.Y);
            SetHoveredItemIndex(itemIndex);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            SetHoveredItemIndex(-1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                App.HideContextMenu();
            }
            else if (hoveredItemIndex >= 0)
            {
                if (e.KeyCode == Keys.Enter)
                {
                    App.HideContextMenu();
                    MarkDirty();
                    menuOptions[hoveredItemIndex].Callback();
                }
                else if (e.KeyCode == Keys.Up)
                {
                    SetHoveredItemIndex(Math.Max(0, hoveredItemIndex - 1));
                }
                else if (e.KeyCode == Keys.Down)
                {
                    SetHoveredItemIndex(Math.Min(menuOptions.Length - 1, hoveredItemIndex + 1));
                }
            }
        }

        protected void SetHoveredItemIndex(int idx)
        {
            if (idx != hoveredItemIndex)
            {
                hoveredItemIndex = idx;
                UpdateTooltip();
                MarkDirty();
            }
        }

        protected void UpdateTooltip()
        {
            App.SetToolTip(hoveredItemIndex >= 0 ? menuOptions[hoveredItemIndex].ToolTip : null);
        }

        public override void Tick(float delta)
        {
        }

        protected override void OnRender(RenderGraphics g)
        {
            Debug.Assert(menuOptions != null && menuOptions.Length > 0);

            var c = g.CreateCommandList();

            c.DrawRectangle(0, 0, Width - 1, Height - 1, ThemeResources.LightGreyFillBrush1);

            for (int i = 0, y = 0; i < menuOptions.Length; i++, y += itemSizeY)
            {
                ContextMenuOption option = menuOptions[i];

                c.PushTranslation(0, y);

                var hover = i == hoveredItemIndex;

                if (hover)
                    c.FillRectangle(0, 0, Width, itemSizeY, ThemeResources.MediumGreyFillBrush1);

                if (option.Separator) 
                    c.DrawLine(0, 0, Width, 0, ThemeResources.LightGreyFillBrush1);

                var bmp = bmpContextMenu[i];

                if (bmp == null)
                {
                    var checkState = option.CheckState();
                    switch (checkState)
                    {
                        case ContextMenuCheckState.Checked:   bmp = bmpMenuCheckOn;  break;
                        case ContextMenuCheckState.Unchecked: bmp = bmpMenuCheckOff; break;
                        case ContextMenuCheckState.Radio:     bmp = bmpMenuRadio;    break;
                    }
                }

                if (bmp != null)
                {
                    c.DrawBitmapAtlas(bmp, iconPos, iconPos, 1, 1, hover ? Theme.LightGreyFillColor2 : Theme.LightGreyFillColor1);
                }

                c.DrawText(option.Text, ThemeResources.FontMedium, textPosX, 0, hover ? ThemeResources.LightGreyFillBrush2 : ThemeResources.LightGreyFillBrush1, RenderTextFlags.MiddleLeft, Width, itemSizeY);
                c.PopTransform();
            }

            g.Clear(Theme.DarkGreyFillColor1);
            g.DrawCommandList(c);
        }
    }
}
