using Microsoft.Maui.Graphics;

namespace ExpenseTracker.Drawables;

public class BarChartDrawable : IDrawable
{
    public float Income { get; set; }
    public float Expense { get; set; }
    public float Balance { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        canvas.FillColor = Colors.Transparent;

        float max = Math.Max(Math.Max(Income, Expense), Balance);
        if (max <= 0) max = 1;

        float barWidth = dirtyRect.Width / 3f * 0.6f;
        float spacing = dirtyRect.Width / 3f;

        float baseY = dirtyRect.Height - 10;

        DrawBar(canvas, Income, max, spacing * 0 + spacing / 2 - barWidth / 2, baseY, barWidth, Colors.Green);
        DrawBar(canvas, Expense, max, spacing * 1 + spacing / 2 - barWidth / 2, baseY, barWidth, Colors.Red);
        DrawBar(canvas, Balance, max, spacing * 2 + spacing / 2 - barWidth / 2, baseY, barWidth, Colors.Blue);
    }

    private void DrawBar(ICanvas canvas, float value, float max, float x, float baseY, float width, Color color)
    {
        float height = (value / max) * (baseY - 20);

        canvas.FillColor = color;
        canvas.FillRectangle(x, baseY - height, width, height);

        canvas.FontColor = Colors.Black;
        canvas.FontSize = 12;
        canvas.DrawString(value.ToString("0"), x, baseY - height - 16, width, 20, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
