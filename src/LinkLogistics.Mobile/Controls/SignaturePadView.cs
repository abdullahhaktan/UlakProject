using SkiaSharp;

namespace LinkLogistics.Mobile.Controls;

/// <summary>
/// A finger-drawing signature area. Strokes are captured as polylines and
/// can be exported to a white-background PNG for upload.
/// </summary>
public sealed class SignaturePadView : GraphicsView
{
    private readonly List<List<PointF>> _strokes = new();
    private readonly SignatureDrawable _drawable;

    public SignaturePadView()
    {
        _drawable = new SignatureDrawable(_strokes);
        Drawable = _drawable;
        HeightRequest = 180;
        BackgroundColor = Colors.White;

        StartInteraction += (_, e) =>
        {
            _strokes.Add(new List<PointF> { e.Touches[0] });
            Invalidate();
        };
        DragInteraction += (_, e) =>
        {
            if (_strokes.Count > 0)
            {
                _strokes[^1].Add(e.Touches[0]);
                Invalidate();
            }
        };
    }

    public bool HasContent => _strokes.Any(s => s.Count > 1);

    public void Clear()
    {
        _strokes.Clear();
        Invalidate();
    }

    /// <summary>Renders the strokes to a PNG with a white background.</summary>
    public byte[]? ExportPng()
    {
        if (!HasContent)
        {
            return null;
        }

        var width = Math.Max(1, (int)Width);
        var height = Math.Max(1, (int)Height);

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
        };

        foreach (var stroke in _strokes.Where(s => s.Count > 1))
        {
            using var path = new SKPath();
            path.MoveTo(stroke[0].X, stroke[0].Y);
            for (var i = 1; i < stroke.Count; i++)
            {
                path.LineTo(stroke[i].X, stroke[i].Y);
            }

            canvas.DrawPath(path, paint);
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed class SignatureDrawable(List<List<PointF>> strokes) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            canvas.FillColor = Colors.White;
            canvas.FillRectangle(dirtyRect);

            canvas.StrokeColor = Colors.Black;
            canvas.StrokeSize = 2.5f;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            foreach (var stroke in strokes.Where(s => s.Count > 1))
            {
                var path = new PathF();
                path.MoveTo(stroke[0].X, stroke[0].Y);
                for (var i = 1; i < stroke.Count; i++)
                {
                    path.LineTo(stroke[i].X, stroke[i].Y);
                }

                canvas.DrawPath(path);
            }
        }
    }
}
