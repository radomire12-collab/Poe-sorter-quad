using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MapSorter.UI;

public sealed class SelectionOverlay : Form
{
    private Point _start;
    private Point _current;
    private bool _selecting;
    private Rectangle? _selection;

    private SelectionOverlay(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        Opacity = 0.3;
        BackColor = Color.Black;
        TopMost = true;
        Bounds = SystemInformation.VirtualScreen;
    }

    public static Rectangle? SelectRegion(string title)
    {
        using var overlay = new SelectionOverlay(title);
        return overlay.ShowDialog() == DialogResult.OK ? overlay._selection : null;
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

        _selecting = true;
        _start = PointToScreen(e.Location);
        _current = _start;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_selecting)
        {
            return;
        }

        _current = PointToScreen(e.Location);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!_selecting || e.Button != MouseButtons.Left)
        {
            return;
        }

        _current = PointToScreen(e.Location);
        _selection = NormalizeRectangle(_start, _current);
        _selecting = false;
        DialogResult = DialogResult.OK;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_selecting)
        {
            var rect = NormalizeRectangle(_start, _current);
            var screenRect = new Rectangle(
                rect.X - SystemInformation.VirtualScreen.X,
                rect.Y - SystemInformation.VirtualScreen.Y,
                rect.Width,
                rect.Height);

            using var brush = new SolidBrush(Color.FromArgb(80, Color.DeepSkyBlue));
            using var pen = new Pen(Color.DeepSkyBlue, 2) { DashStyle = DashStyle.Dash };
            e.Graphics.FillRectangle(brush, screenRect);
            e.Graphics.DrawRectangle(pen, screenRect);
        }

        DrawInstruction(e.Graphics);
    }

    private void DrawInstruction(Graphics g)
    {
        const string message = "Drag to select area • Release to confirm • Esc to cancel";
        using var font = new Font("Segoe UI", 14, FontStyle.Bold);
        var size = g.MeasureString(message, font);
        var position = new PointF(
            (Width - size.Width) / 2f,
            (Height - size.Height) / 2f);

        using var bgBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
        using var textBrush = new SolidBrush(Color.White);
        var rect = new RectangleF(position, size);
        rect.Inflate(20, 10);
        g.FillRectangle(bgBrush, rect);
        g.DrawString(message, font, textBrush, position);
    }

    private static Rectangle NormalizeRectangle(Point a, Point b)
    {
        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);
        return new Rectangle(x1, y1, x2 - x1, y2 - y1);
    }
}


