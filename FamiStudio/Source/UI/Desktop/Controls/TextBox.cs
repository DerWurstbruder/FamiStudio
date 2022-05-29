﻿using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System;
using System.Diagnostics;

using RenderBitmapAtlas = FamiStudio.GLBitmapAtlas;
using RenderBrush = FamiStudio.GLBrush;
using RenderGeometry = FamiStudio.GLGeometry;
using RenderControl = FamiStudio.GLControl;
using RenderGraphics = FamiStudio.GLGraphics;
using RenderCommandList = FamiStudio.GLCommandList;

namespace FamiStudio
{
    // TODO:
    //  - Copy/paste

    public class TextBox2 : RenderControl
    {
        private string text;
        private int scrollX;
        private int maxScrollX;
        private int selectionStart = -1;
        private int selectionLength = 0;
        private int caretIndex = 0;
        private int widthNoMargin;
        private int mouseSelectionChar;
        private bool mouseSelecting;
        private bool caretBlink = true;
        private float caretBlinkTime;

        private Color foreColor = Theme.LightGreyFillColor1;
        private Color backColor = Theme.DarkGreyLineColor1;

        private RenderBrush foreBrush;
        private RenderBrush backBrush;
        private RenderBrush selBrush;
        
        private int topMargin    = DpiScaling.ScaleForMainWindow(3);
        private int sideMargin   = DpiScaling.ScaleForMainWindow(4);
        private int scrollAmount = DpiScaling.ScaleForMainWindow(20);

        public Color ForeColor { get => foreColor; set { foreColor = value; foreBrush = null; MarkDirty(); } }
        public Color BackColor { get => backColor; set { backColor = value; backBrush = null;  selBrush = null; MarkDirty(); } }

        public TextBox2(string txt)
        {
            height = DpiScaling.ScaleForMainWindow(24);
            text = txt;
            //text = "Hello this is a very   ₭₭ long text bla bla bla toto titi tata tutu"; // For debugging.
        }

        public string Text
        {
            get { return text; }
            set 
            { 
                text = value;
                scrollX = 0;
                caretIndex = 0;
                selectionStart = 0;
                selectionLength = 0;
                UpdateScrollParams();
                MarkDirty(); 
            }
        }

        private void UpdateScrollParams()
        {
            maxScrollX = Math.Max(0, ThemeResources.FontMedium.MeasureString(text, false) - (width - sideMargin * 2));
            scrollX = Utils.Clamp(scrollX, 0, maxScrollX);
        }

        protected override void OnMouseMove(MouseEventArgs2 e)
        {
            Cursor.Current = enabled ? Cursors.Default : Cursors.IBeam;

            if (mouseSelecting)
            {
                var c = PixelToChar(e.X);
                var selMin = Math.Min(mouseSelectionChar, c);
                var selMax = Math.Max(mouseSelectionChar, c);

                SetAndMarkDirty(ref caretIndex, c);
                SetAndMarkDirty(ref selectionStart, selMin);
                SetAndMarkDirty(ref selectionLength, selMax - selMin);
                EnsureCaretVisible();
            }
        }

        protected override void OnMouseDown(MouseEventArgs2 e)
        {
            if (e.Left)
            {
                var c = PixelToChar(e.X);
                SetAndMarkDirty(ref caretIndex, c);
                SetAndMarkDirty(ref selectionStart, c);
                SetAndMarkDirty(ref selectionLength, 0);
                ClearSelection();
                ResetCaretBlink();

                mouseSelectionChar = c;
                mouseSelecting = true;
                Capture = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs2 e)
        {
            if (e.Left)
            {
                mouseSelecting = false;
                Capture = false;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs2 e)
        {
            var c0 = PixelToChar(e.X);
            var c1 = c0;

            // MATTT : This is buggy, clicking on a space select both left/right words.
            c0 = FindWordStart(c0, -1);
            c1 = FindWordStart(c1,  1);

            selectionStart  = c0;
            selectionLength = c1 - c0;
            caretIndex      = c1;

            MarkDirty();
        }

        private int FindWordStart(int c, int dir)
        {
            if (dir > 0)
            {
                while (c < text.Length && !char.IsWhiteSpace(text[c]))
                    c++;
                while (c < text.Length && char.IsWhiteSpace(text[c]))
                    c++;
            }
            else
            {
                while (c >= 1 &&  char.IsWhiteSpace(text[c - 1]))
                    c--;
                while (c >= 1 && !char.IsWhiteSpace(text[c - 1]))
                    c--;
            }

            Debug.Assert(c >= 0 && c <= text.Length);
            return c;
        }


        // MATTT : See if GLFW (or GTK) has key repeat, if it doesnt, well need to 
        // handle it ourselves.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // MATTT : Copy/paste.
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right)
            {
                var sign = e.KeyCode == Keys.Left ? -1 : 1;
                var prevCaretIndex = caretIndex;

                if (e.Modifiers.HasFlag(Keys.Control) || e.Modifiers.HasFlag(Keys.Alt))
                    caretIndex = FindWordStart(caretIndex, sign);
                else
                    caretIndex = Utils.Clamp(caretIndex + sign, 0, text.Length);

                if (e.Modifiers.HasFlag(Keys.Shift))
                {
                    var minCaret = Math.Min(prevCaretIndex, caretIndex);
                    var maxCaret = Math.Max(prevCaretIndex, caretIndex);

                    if (selectionLength == 0)
                    {
                        SetSelection(minCaret, maxCaret - minCaret);
                    }
                    else 
                    {
                        var selMin = selectionStart;
                        var selMax = selectionStart + selectionLength;

                        // This seem WAYYYY over complicated.
                        if (caretIndex < selMax && prevCaretIndex < selMax)
                            SetSelection(caretIndex, selMax - caretIndex);
                        else if (caretIndex >= selMin && prevCaretIndex > selMin)
                            SetSelection(selMin, caretIndex - selMin);
                        else if (caretIndex < selMin && prevCaretIndex > selMin)
                            SetSelection(caretIndex, selMin - caretIndex);
                        else if (caretIndex >= selMax && prevCaretIndex < selMax)
                            SetSelection(selMax, caretIndex - selMax);
                    }
                }
                else
                {
                    ClearSelection();
                }

                ResetCaretBlink();
                EnsureCaretVisible();
                MarkDirty();
            }
            else if (e.KeyCode == Keys.A && e.Modifiers.HasFlag(Keys.Control))
            {
                SelectAll();
            }
            else if (e.KeyCode == Keys.Back)
            {
                if (!DeleteSelection() && caretIndex > 0)
                {
                    caretIndex--;
                    text = RemoveStringRange(caretIndex, 1);
                    UpdateScrollParams();
                    MarkDirty();
                }
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (!DeleteSelection() && caretIndex < text.Length)
                {
                    text = RemoveStringRange(caretIndex, 1);
                    UpdateScrollParams();
                    MarkDirty();
                }
            }
            else if ((int)e.KeyCode >= 0 && (int)e.KeyCode <= 255 && ThemeResources.FontMedium.GetCharInfo((char)e.KeyCode, false) != null)
            {
                // MATTT : This is janky. Need equivalent of OnKeyPress().
                DeleteSelection();
                text = text.Insert(caretIndex, ((char)e.KeyCode).ToString());
                caretIndex++;
                UpdateScrollParams();
                EnsureCaretVisible();
                ClearSelection();
                MarkDirty();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ClearDialogFocus();
                e.Handled = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
        }

        public override void Tick(float delta)
        {
            caretBlinkTime += delta;
            SetAndMarkDirty(ref caretBlink, Utils.Frac(caretBlinkTime) < 0.5f);
        }

        private void UpdateCaretBlink()
        {
            SetAndMarkDirty(ref caretBlink, Utils.Frac(caretBlinkTime) < 0.5f);
        }

        private void ResetCaretBlink()
        {
            caretBlinkTime = 0;
            UpdateCaretBlink();
        }

        private void EnsureCaretVisible()
        {
            var px = CharToPixel(caretIndex, false);
            if (px < 0)
                SetAndMarkDirty(ref scrollX, Utils.Clamp(scrollX + px - scrollAmount, 0, maxScrollX));
            else if (px > widthNoMargin)
                SetAndMarkDirty(ref scrollX, Utils.Clamp(scrollX + px - widthNoMargin + scrollAmount, 0, maxScrollX));
        }

        private int PixelToChar(int x, bool margin = true)
        {
            return ThemeResources.FontMedium.GetNumCharactersForSize(text, x - (margin ? sideMargin : 0) + scrollX, true);
        }

        private int CharToPixel(int c, bool margin = true)
        {
            var px = (margin ? sideMargin : 0) - scrollX;
            if (c > 0)
                px += ThemeResources.FontMedium.MeasureString(text.Substring(0, c), false);
            return px;
        }

        public void SetSelection(int start, int len)
        {
            SetAndMarkDirty(ref selectionStart, start);
            SetAndMarkDirty(ref selectionLength, Math.Max(0, len));
        }

        public void ClearSelection()
        {
            SetAndMarkDirty(ref selectionStart, 0);
            SetAndMarkDirty(ref selectionLength, 0);
        }

        public void SelectAll()
        {
            selectionStart = 0;
            selectionLength = text.Length;
            caretIndex = text.Length;
            MarkDirty();
        }

        private string RemoveStringRange(int start, int len)
        {
            var left  = text.Substring(0, start);
            var right = text.Substring(start + len);

            return left + right;
        }

        private bool DeleteSelection()
        {
            if (selectionLength > 0)
            {
                text = RemoveStringRange(selectionStart, selectionLength);

                if (caretIndex >= selectionStart)
                    caretIndex -= selectionLength;

                ClearSelection();
                UpdateScrollParams();

                return true;
            }

            return false;
        }

        protected override void OnAddedToDialog()
        {
            widthNoMargin = width - sideMargin * 2;
            UpdateScrollParams();
        }

        protected override void OnRender(RenderGraphics g)
        {
            var c = parentDialog.CommandList;

            if (foreBrush == null) foreBrush = g.CreateSolidBrush(foreColor);
            if (backBrush == null) backBrush = g.CreateSolidBrush(backColor);
            if (selBrush  == null) selBrush  = g.CreateSolidBrush(Theme.Darken(backColor));

            c.FillAndDrawRectangle(0, 0, width - 1, height - 1, backBrush, foreBrush);
            
            if (selectionLength > 0 && HasDialogFocus)
            {
                var sx0 = Utils.Clamp(CharToPixel(selectionStart), sideMargin, width - sideMargin);
                var sx1 = selectionLength > 0 ? Utils.Clamp(CharToPixel(selectionStart + selectionLength), sideMargin, width - sideMargin) : sx0;

                if (sx0 != sx1)
                    c.FillRectangle(sx0, topMargin, sx1, height - topMargin, selBrush);
            }

            c.DrawText(text, ThemeResources.FontMedium, sideMargin - scrollX, 0, foreBrush, RenderTextFlags.MiddleLeft | RenderTextFlags.Clip, 0, height, sideMargin, width - sideMargin);

            if (caretBlink && HasDialogFocus)
            {
                var cx = CharToPixel(caretIndex);
                c.DrawLine(cx, topMargin, cx, height - topMargin, foreBrush);
            }
        }
    }
}
