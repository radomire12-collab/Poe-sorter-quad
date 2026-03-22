using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MapSorter.Configuration;

namespace MapSorter.UI;

public sealed class GridAlignmentOverlay : Form
{
    private readonly GridConfig _config;
    private readonly string _label;
    private readonly Point _screenOffset;
    private Point _origin;
    private SizeF _slotSize;
    private bool _draggingOrigin;
    private Point _dragStart;
    private Point _originAtDragStart;

    private GridAlignmentOverlay(GridConfig config, string label)
    {
        _config = config;
        _label = label;
        _origin = new Point(config.Origin.X, config.Origin.Y);
        _slotSize = new SizeF((float)config.SlotSize.Width, (float)config.SlotSize.Height);
        _screenOffset = new Point(-SystemInformation.VirtualScreen.X, -SystemInformation.VirtualScreen.Y);

        Bounds = SystemInformation.VirtualScreen;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        TopMost = true;
        Cursor = Cursors.SizeAll;
        Opacity = 0.35;
        KeyPreview = true;
        BackColor = Color.Black;
    }

    public static Point? Align(GridConfig config, string label)
    {
        using var overlay = new GridAlignmentOverlay(config, label);
        return overlay.ShowDialog() == DialogResult.OK ? overlay._origin : null;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Activate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _draggingOrigin = true;
        _dragStart = e.Location;
        _originAtDragStart = _origin;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_draggingOrigin)
        {
            return;
        }

        var delta = new Point(e.X - _dragStart.X, e.Y - _dragStart.Y);
        _origin = ClampOrigin(new Point(_originAtDragStart.X + delta.X, _originAtDragStart.Y + delta.Y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_draggingOrigin)
        {
            _draggingOrigin = false;
        }

        Cursor = Cursors.Default;
    }

    protected override void OnDoubleClick(EventArgs e)
    {
        Confirm();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var step = (keyData & Keys.Shift) != 0 ? 10 : 1;

        switch (keyData & Keys.KeyCode)
        {
            case Keys.Left:
                AdjustOrigin(-step, 0);
                return true;
            case Keys.Right:
                AdjustOrigin(step, 0);
                return true;
            case Keys.Up:
                AdjustOrigin(0, -step);
                return true;
            case Keys.Down:
                AdjustOrigin(0, step);
                return true;
            case Keys.Add:
            case Keys.Oemplus:
                AdjustSlotSize(step * 0.05f);
                return true;
            case Keys.Subtract:
            case Keys.OemMinus:
                AdjustSlotSize(-step * 0.05f);
                return true;
            case Keys.Enter:
                Confirm();
                return true;
            case Keys.Escape:
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var offsetOrigin = new Point(_origin.X + _screenOffset.X, _origin.Y + _screenOffset.Y);
        var gridRect = new Rectangle(offsetOrigin, CurrentGridSize);

        using var borderPen = new Pen(Color.Lime, 2);
        using var gridPen = new Pen(Color.FromArgb(160, Color.Lime), 1);
        using var fillBrush = new SolidBrush(Color.FromArgb(60, Color.Black));

        e.Graphics.FillRectangle(fillBrush, gridRect);
        e.Graphics.DrawRectangle(borderPen, gridRect);

        DrawGridLines(e.Graphics, gridRect, gridPen);
        DrawHud(e.Graphics);
    }

    private void DrawGridLines(Graphics g, Rectangle rect, Pen pen)
    {
        var slotWidth = _slotSize.Width + _config.SlotSpacing.X;
        var slotHeight = _slotSize.Height + _config.SlotSpacing.Y;

        for (var col = 1; col < _config.Cols; col++)
        {
            var x = rect.Left + col * slotWidth - _config.SlotSpacing.X;
            g.DrawLine(pen, x, rect.Top, x, rect.Bottom);
        }

        for (var row = 1; row < _config.Rows; row++)
        {
            var y = rect.Top + row * slotHeight - _config.SlotSpacing.Y;
            g.DrawLine(pen, rect.Left, y, rect.Right, y);
        }
    }

    private void DrawHud(Graphics g)
    {
        const int padding = 16;
        var text = $"{_label.ToUpperInvariant()} GRID\nDrag inside to move • +/- change cell size (Shift = x5) • Arrow keys nudge (Shift = 10px) • Enter confirm • Esc cancel\nOrigin: {_origin.X}, {_origin.Y}  Size: {_slotSize.Width:F2}x{_slotSize.Height:F2}px";
        using var font = new Font("Segoe UI", 12, FontStyle.Bold);
        var textSize = g.MeasureString(text, font);
        var rect = new RectangleF(padding, padding, textSize.Width + padding, textSize.Height + padding);
        using var bg = new SolidBrush(Color.FromArgb(180, 0, 0, 0));
        using var fg = new SolidBrush(Color.White);
        g.FillRectangle(bg, rect);
        g.DrawString(text, font, fg, new PointF(padding + 6, padding + 4));
    }

    private Size CurrentGridSize => CalculateGridSize(_config, _slotSize);

    private void AdjustOrigin(int dx, int dy)
    {
        _origin = ClampOrigin(new Point(_origin.X + dx, _origin.Y + dy));
        Invalidate();
    }

    private Point ClampOrigin(Point origin)
    {
        var gridSize = CurrentGridSize;
        var screen = SystemInformation.VirtualScreen;
        var maxX = screen.Right - gridSize.Width;
        var maxY = screen.Bottom - gridSize.Height;
        var minX = screen.Left;
        var minY = screen.Top;

        var clampedX = Math.Clamp(origin.X, minX, maxX);
        var clampedY = Math.Clamp(origin.Y, minY, maxY);
        return new Point(clampedX, clampedY);
    }

    private void Confirm()
    {
        _config.SlotSize = new SizeConfig(_slotSize.Width, _slotSize.Height);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Size CalculateGridSize(GridConfig config, SizeF slotSize)
    {
        var width = config.Cols * slotSize.Width + Math.Max(0, config.Cols - 1) * config.SlotSpacing.X;
        var height = config.Rows * slotSize.Height + Math.Max(0, config.Rows - 1) * config.SlotSpacing.Y;
        return new Size((int)Math.Round(width), (int)Math.Round(height));
    }

    private void AdjustSlotSize(float delta)
    {
        if (Math.Abs(delta) < 0.0001f)
        {
            return;
        }

        const float minCell = 10f;
        const float maxCell = 200f;
        var newWidth = Math.Clamp(_slotSize.Width + delta, minCell, maxCell);
        var newHeight = Math.Clamp(_slotSize.Height + delta, minCell, maxCell);

        if (Math.Abs(newWidth - _slotSize.Width) < 0.0001f && Math.Abs(newHeight - _slotSize.Height) < 0.0001f)
        {
            return;
        }

        _slotSize = new SizeF(newWidth, newHeight);
        Invalidate();
    }
}

