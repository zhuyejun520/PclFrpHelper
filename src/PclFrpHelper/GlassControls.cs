using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PclFrpHelper
{
    // ---------------- Liquid Glass theme & controls ----------------

    internal static class Glass
    {
        public static readonly Color Accent = Color.FromArgb(10, 132, 255);
        public static readonly Color AccentDeep = Color.FromArgb(0, 100, 220);
        public static readonly Color Text = Color.FromArgb(28, 28, 30);
        public static readonly Color SubText = Color.FromArgb(108, 112, 120);
        public static readonly Color PageBg = Color.FromArgb(246, 249, 254);

        public static Font F(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        public static GraphicsPath Round(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            if (radius <= 0) { p.AddRectangle(r); return p; }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var arc = new Rectangle(r.X, r.Y, d, d);
            p.AddArc(arc, 180, 90);
            arc.X = r.Right - d; p.AddArc(arc, 270, 90);
            arc.Y = r.Bottom - d; p.AddArc(arc, 0, 90);
            arc.X = r.X; p.AddArc(arc, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color c)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, Rectangle r, int radius, Color c, float w = 1f)
        {
            var rr = new Rectangle(r.X, r.Y, r.Width, r.Height);
            using (var p = Round(rr, radius))
            using (var pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        public static void Shadow(Graphics g, Rectangle r, int radius, int depth, int alphaBase)
        {
            for (int i = depth; i >= 1; i--)
            {
                int a = Math.Max(1, alphaBase * (depth - i + 1) / (depth * 3));
                var rr = new Rectangle(r.X - i, r.Y - i + 3, r.Width + i * 2, r.Height + i * 2);
                using (var p = Round(rr, radius + i))
                using (var b = new SolidBrush(Color.FromArgb(a, 40, 60, 90)))
                    g.FillPath(b, p);
            }
        }

        public static void Card(Graphics g, Rectangle r, int radius = 18)
        {
            Shadow(g, r, radius, 6, 44);
            using (var p = Round(r, radius))
            using (var b = new LinearGradientBrush(r, Color.FromArgb(228, 255, 255, 255), Color.FromArgb(170, 238, 244, 252), LinearGradientMode.Vertical))
                g.FillPath(b, p);
            StrokeRound(g, r, radius, Color.FromArgb(200, 255, 255, 255), 1.2f);
        }

        public static void GlassButtonPaint(Graphics g, Rectangle r, bool primary, bool hover, bool down)
        {
            int radius = r.Height / 2;
            if (primary)
            {
                var c1 = down ? AccentDeep : (hover ? Color.FromArgb(36, 152, 255) : Accent);
                var c2 = down ? Color.FromArgb(0, 88, 196) : (hover ? Color.FromArgb(0, 118, 238) : AccentDeep);
                using (var p = Round(r, radius))
                using (var b = new LinearGradientBrush(r, c1, c2, LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                using (var p = Round(r, radius))
                using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
                    g.DrawPath(pen, p);
                using (var p = Round(new Rectangle(r.X + 4, r.Y + 2, r.Width - 8, Math.Max(2, r.Height / 3)), radius))
                using (var b = new LinearGradientBrush(new Rectangle(r.X + 4, r.Y + 2, r.Width - 8, Math.Max(2, r.Height / 3)), Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                    g.FillPath(b, p);
            }
            else
            {
                var fill = down ? Color.FromArgb(244, 236, 242, 250) : (hover ? Color.FromArgb(250, 255, 255, 255) : Color.FromArgb(230, 255, 255, 255));
                using (var p = Round(r, radius))
                using (var b = new LinearGradientBrush(r, Color.FromArgb(215, 255, 255, 255), fill, LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                StrokeRound(g, r, radius, Color.FromArgb(hover ? 220 : 180, 255, 255, 255), 1f);
            }
        }

        public static void PaintScene(Graphics g, Rectangle area)
        {
            using (var b = new LinearGradientBrush(area, Color.FromArgb(238, 245, 255), Color.FromArgb(219, 232, 252), LinearGradientMode.ForwardDiagonal))
                g.FillRectangle(b, area);
            Blob(g, area.X - 80, area.Y - 100, 700, Color.FromArgb(235, 96, 160, 255));
            Blob(g, area.X + area.Width - 340, area.Y + area.Height - 300, 680, Color.FromArgb(205, 176, 140, 255));
            Blob(g, area.X + area.Width - 280, area.Y - 180, 560, Color.FromArgb(185, 118, 214, 255));
            Blob(g, area.X - 140, area.Y + area.Height - 240, 500, Color.FromArgb(175, 255, 176, 214));
            using (var b = new LinearGradientBrush(area, Color.FromArgb(118, 255, 255, 255), Color.FromArgb(92, 244, 249, 255), LinearGradientMode.Vertical))
                g.FillRectangle(b, area);
            using (var b = new LinearGradientBrush(new Rectangle(area.X, area.Y, area.Width, 96), Color.FromArgb(90, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), LinearGradientMode.Vertical))
                g.FillRectangle(b, area.X, area.Y, area.Width, 96);
        }

        public static void Blob(Graphics g, int x, int y, int size, Color color)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(x, y, size, size);
                using (var b = new PathGradientBrush(path))
                {
                    b.CenterColor = color;
                    b.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillPath(b, path);
                }
            }
        }

        public static void Backdrop(Graphics g, Control c)
        {
            var form = c.FindForm();
            if (form == null)
            {
                using (var b = new SolidBrush(PageBg)) g.FillRectangle(b, c.ClientRectangle);
                return;
            }
            var p = c.PointToScreen(Point.Empty);
            var o = form.PointToScreen(Point.Empty);
            PaintScene(g, new Rectangle(o.X - p.X, o.Y - p.Y, form.ClientSize.Width, form.ClientSize.Height));
        }
    }

    internal class GlButton : Control
    {
        private bool _hover, _down;
        public bool Primary;
        public Color TextColor = Glass.Text;

        public GlButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Glass.F(9f);
            Cursor = Cursors.Hand;
            Height = 30;
            BackColor = Glass.PageBg;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.Backdrop(g, this);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Glass.GlassButtonPaint(g, r, Primary, _hover, _down);
            var color = Primary ? Color.White : TextColor;
            TextRenderer.DrawText(g, Text, Font, r, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    internal class GlTextBox : Control
    {
        private readonly TextBox _box = new TextBox();
        private bool _focus;
        public bool MultilineBox { get { return _box.Multiline; } set { _box.Multiline = value; LayoutBox(); Invalidate(); } }
        public bool ReadOnlyBox { get { return _box.ReadOnly; } set { _box.ReadOnly = value; } }
        public ScrollBars ScrollBarsBox { get { return _box.ScrollBars; } set { _box.ScrollBars = value; LayoutBox(); } }
        public string BoxText { get { return _box.Text; } set { _box.Text = value; } }
        public TextBox Inner { get { return _box; } }

        public GlTextBox()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 30;
            BackColor = Glass.PageBg;
            _box.BorderStyle = BorderStyle.None;
            _box.Font = Glass.F(9.5f);
            _box.BackColor = Color.FromArgb(252, 253, 255);
            _box.ForeColor = Glass.Text;
            _box.GotFocus += (s, e) => { _focus = true; Invalidate(); };
            _box.LostFocus += (s, e) => { _focus = false; Invalidate(); };
            _box.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);
            Controls.Add(_box);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutBox();
        }

        private void LayoutBox()
        {
            int pad = 10;
            if (_box.Multiline)
                _box.SetBounds(pad, pad, Math.Max(10, Width - pad * 2), Math.Max(10, Height - pad * 2));
            else
                _box.SetBounds(pad, Math.Max(pad - 3, (Height - _box.PreferredHeight) / 2), Math.Max(10, Width - pad * 2), _box.PreferredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.Backdrop(g, this);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Glass.Round(r, 12))
            using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                g.FillPath(b, p);
            Glass.StrokeRound(g, r, 12, _focus ? Color.FromArgb(220, Glass.Accent) : Color.FromArgb(190, 255, 255, 255), _focus ? 1.6f : 1f);
            base.OnPaint(e);
        }
    }

    internal class GlTabStrip : Control
    {
        private string[] _tabs = new string[0];
        private int _index;
        private int _hover = -1;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex
        {
            get { return _index; }
            set { if (value >= 0 && value < _tabs.Length && value != _index) { _index = value; Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); } }
        }

        public GlTabStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 36;
            Font = Glass.F(9.5f, FontStyle.Bold);
            Cursor = Cursors.Hand;
            BackColor = Glass.PageBg;
        }

        public void SetTabs(params string[] tabs) { _tabs = tabs; Invalidate(); }

        private Rectangle TabRect(int i)
        {
            int w = Width / Math.Max(1, _tabs.Length);
            return new Rectangle(i * w, 0, w, Height);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = -1;
            for (int i = 0; i < _tabs.Length; i++) if (TabRect(i).Contains(e.Location)) idx = i;
            if (idx != _hover) { _hover = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            for (int i = 0; i < _tabs.Length; i++) if (TabRect(i).Contains(e.Location)) { SelectedIndex = i; break; }
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.Backdrop(g, this);
            var track = new Rectangle(0, 2, Width - 1, Height - 5);
            using (var p = Glass.Round(track, (Height - 5) / 2))
            using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                g.FillPath(b, p);
            for (int i = 0; i < _tabs.Length; i++)
            {
                var r = TabRect(i);
                var inner = new Rectangle(r.X + 3, r.Y + 4, r.Width - 6, r.Height - 9);
                if (i == _index)
                {
                    Glass.Shadow(g, inner, inner.Height / 2, 3, 70);
                    using (var p = Glass.Round(inner, inner.Height / 2))
                    using (var b = new LinearGradientBrush(inner, Color.FromArgb(255, 255, 255), Color.FromArgb(238, 245, 255), LinearGradientMode.Vertical))
                        g.FillPath(b, p);
                    Glass.StrokeRound(g, inner, inner.Height / 2, Color.FromArgb(220, 255, 255, 255));
                }
                else if (i == _hover)
                {
                    using (var p = Glass.Round(inner, inner.Height / 2))
                    using (var b = new SolidBrush(Color.FromArgb(90, 255, 255, 255)))
                        g.FillPath(b, p);
                }
                TextRenderer.DrawText(g, _tabs[i], Font, r, i == _index ? Glass.Text : Glass.SubText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }

    internal class GlList : Control
    {
        public string[] Headers = new string[0];
        public float[] Weights = new float[0];
        private readonly List<string[]> _rows = new List<string[]>();
        private readonly List<object> _tags = new List<object>();
        private int _selected = -1;
        private int _hover = -1;
        public event EventHandler SelectionChanged;
        public object SelectedTag { get { return _selected >= 0 && _selected < _tags.Count ? _tags[_selected] : null; } }
        public int RowCount { get { return _rows.Count; } }

        public GlList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Glass.F(9f);
            Cursor = Cursors.Hand;
            BackColor = Glass.PageBg;
        }

        public void Clear() { _rows.Clear(); _tags.Clear(); _selected = -1; _hover = -1; Invalidate(); }
        public void Add(string[] cells, object tag) { _rows.Add(cells); _tags.Add(tag); Invalidate(); }
        public void Select(int i)
        {
            if (i < -1 || i >= _rows.Count) return;
            if (i != _selected) { _selected = i; Invalidate(); if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty); }
        }

        private const int HeaderHeight = 28;
        private const int RowHeight = 32;

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = -1;
            if (e.Y > HeaderHeight) { int i = (e.Y - HeaderHeight) / RowHeight; if (i >= 0 && i < _rows.Count) idx = i; }
            if (idx != _hover) { _hover = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e) { _hover = -1; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Y > HeaderHeight)
            {
                int i = (e.Y - HeaderHeight) / RowHeight;
                if (i >= 0 && i < _rows.Count) Select(i);
            }
            base.OnMouseDown(e);
        }

        private int[] ColumnX()
        {
            var xs = new int[Headers.Length + 1];
            float sum = 0; foreach (var w in Weights) sum += w;
            if (sum <= 0) sum = Math.Max(1, Headers.Length);
            float acc = 0;
            for (int i = 0; i < Headers.Length; i++)
            {
                xs[i] = (int)(8 + acc * (Width - 16) / sum);
                acc += Weights.Length > i ? Weights[i] : 1;
            }
            xs[Headers.Length] = Width - 8;
            return xs;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.Backdrop(g, this);
            var body = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Glass.Round(body, 14))
            using (var b = new SolidBrush(Color.FromArgb(190, 255, 255, 255)))
                g.FillPath(b, p);
            Glass.StrokeRound(g, body, 14, Color.FromArgb(190, 255, 255, 255));

            var xs = ColumnX();
            var headerFont = Glass.F(8.5f);
            for (int c = 0; c < Headers.Length; c++)
            {
                var rr = new Rectangle(xs[c], 6, xs[c + 1] - xs[c] - 4, 18);
                TextRenderer.DrawText(g, Headers[c], headerFont, rr, Glass.SubText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
            using (var pen = new Pen(Color.FromArgb(120, 150, 165, 190)))
                g.DrawLine(pen, 10, HeaderHeight, Width - 10, HeaderHeight);

            for (int i = 0; i < _rows.Count; i++)
            {
                var row = new Rectangle(6, HeaderHeight + i * RowHeight, Width - 12, RowHeight - 4);
                if (i == _selected)
                {
                    using (var p = Glass.Round(row, 10))
                    using (var b = new SolidBrush(Color.FromArgb(48, Glass.Accent)))
                        g.FillPath(b, p);
                    Glass.StrokeRound(g, row, 10, Color.FromArgb(130, Glass.Accent));
                }
                else if (i == _hover)
                {
                    using (var p = Glass.Round(row, 10))
                    using (var b = new SolidBrush(Color.FromArgb(120, 255, 255, 255)))
                        g.FillPath(b, p);
                }
                var cells = _rows[i];
                for (int c = 0; c < Headers.Length && c < cells.Length; c++)
                {
                    var rr = new Rectangle(xs[c] + 4, row.Y + 4, xs[c + 1] - xs[c] - 8, row.Height - 8);
                    TextRenderer.DrawText(g, cells[c], Font, rr, Glass.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
        }
    }

    internal class GlSurface : Panel
    {
        public GlSurface()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Glass.PageBg;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.Backdrop(g, this);
            base.OnPaint(e);
        }
    }

    internal class GlLabel : Label
    {
        public GlLabel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Glass.PageBg;
            Font = Glass.F(9f);
            ForeColor = Glass.SubText;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Glass.Backdrop(g, this);
            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            if (TextAlign == ContentAlignment.MiddleCenter || TextAlign == ContentAlignment.TopCenter || TextAlign == ContentAlignment.BottomCenter)
                flags |= TextFormatFlags.HorizontalCenter;
            else if (TextAlign == ContentAlignment.MiddleRight || TextAlign == ContentAlignment.TopRight || TextAlign == ContentAlignment.BottomRight)
                flags |= TextFormatFlags.Right;
            else flags |= TextFormatFlags.Left;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, ForeColor, flags);
        }
    }

}
