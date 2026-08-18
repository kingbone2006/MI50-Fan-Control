using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MI50FanControl.Models;
using WpfUserControl = System.Windows.Controls.UserControl;
using WpfCanvas = System.Windows.Controls.Canvas;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace MI50FanControl.Controls
{
    public partial class CurveGraphControl : WpfUserControl
    {
        public static readonly DependencyProperty ProfileProperty =
            DependencyProperty.Register(
                nameof(Profile),
                typeof(FanCurveProfile),
                typeof(CurveGraphControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnProfileChanged));

        public static readonly DependencyProperty CurrentTemperatureProperty =
            DependencyProperty.Register(
                nameof(CurrentTemperature),
                typeof(float),
                typeof(CurveGraphControl),
                new FrameworkPropertyMetadata(0f, FrameworkPropertyMetadataOptions.AffectsRender, OnTempChanged));

        public FanCurveProfile? Profile
        {
            get => (FanCurveProfile?)GetValue(ProfileProperty);
            set => SetValue(ProfileProperty, value);
        }

        public float CurrentTemperature
        {
            get => (float)GetValue(CurrentTemperatureProperty);
            set => SetValue(CurrentTemperatureProperty, value);
        }

        public event Action? CurveChanged;

        private CurvePoint? _draggedPoint = null;
        private Ellipse? _draggedCircle = null;
        private bool _isDragging = false;

        private const double PaddingLeft = 42;
        private const double PaddingBottom = 30;
        private const double PaddingTop = 15;
        private const double PaddingRight = 20;
        private const float MinTemp = 20f;
        private const float MaxTemp = 100f;

        public CurveGraphControl()
        {
            InitializeComponent();
            GraphCanvas.MouseLeftButtonUp += GraphCanvas_MouseLeftButtonUp;
            GraphCanvas.MouseMove += GraphCanvas_MouseMove;
            GraphCanvas.MouseLeave += GraphCanvas_MouseLeave;
            GraphCanvas.MouseLeftButtonDown += GraphCanvas_MouseLeftButtonDown;
        }

        private static void OnProfileChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CurveGraphControl control)
            {
                control.RedrawGraph();
            }
        }

        private static void OnTempChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CurveGraphControl control)
            {
                control.RedrawGraph();
            }
        }

        private void GraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawGraph();
        }

        public void RedrawGraph()
        {
            GraphCanvas.Children.Clear();

            double width = GraphCanvas.ActualWidth;
            double height = GraphCanvas.ActualHeight;
            if (width < 50 || height < 50) return;

            double graphWidth = width - PaddingLeft - PaddingRight;
            double graphHeight = height - PaddingTop - PaddingBottom;

            // 1. Horizontal grid lines (0% to 100%)
            for (int p = 0; p <= 100; p += 25)
            {
                double y = PaddingTop + graphHeight - (p / 100.0) * graphHeight;
                var line = new Line
                {
                    X1 = PaddingLeft,
                    Y1 = y,
                    X2 = PaddingLeft + graphWidth,
                    Y2 = y,
                    Stroke = new SolidColorBrush(WpfColor.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                GraphCanvas.Children.Add(line);

                var txt = new WpfTextBlock
                {
                    Text = $"{p}%",
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(110, 118, 129)),
                    FontSize = 10,
                    TextAlignment = TextAlignment.Right,
                    Width = 34
                };
                WpfCanvas.SetLeft(txt, 4);
                WpfCanvas.SetTop(txt, y - 7);
                GraphCanvas.Children.Add(txt);
            }

            // 2. Vertical grid lines (20°C to 100°C)
            for (float t = MinTemp; t <= MaxTemp; t += 20f)
            {
                double x = PaddingLeft + ((t - MinTemp) / (MaxTemp - MinTemp)) * graphWidth;
                var line = new Line
                {
                    X1 = x,
                    Y1 = PaddingTop,
                    X2 = x,
                    Y2 = PaddingTop + graphHeight,
                    Stroke = new SolidColorBrush(WpfColor.FromArgb(40, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                GraphCanvas.Children.Add(line);

                var txt = new WpfTextBlock
                {
                    Text = $"{t:F0}°C",
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(110, 118, 129)),
                    FontSize = 10,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                };
                WpfCanvas.SetLeft(txt, x - 12);
                WpfCanvas.SetTop(txt, PaddingTop + graphHeight + 6);
                GraphCanvas.Children.Add(txt);
            }

            if (Profile == null || Profile.Points == null || Profile.Points.Count == 0) return;

            var sortedPoints = Profile.Points.OrderBy(pt => pt.Temperature).ToList();

            // 3. Build Polyline for Smooth Curve Path
            var polyPoints = new PointCollection();

            double startX = PaddingLeft;
            double startY = PaddingTop + graphHeight - (sortedPoints[0].FanSpeedPercent / 100.0) * graphHeight;
            polyPoints.Add(new WpfPoint(startX, startY));

            foreach (var pt in sortedPoints)
            {
                double ptX = PaddingLeft + ((Math.Clamp(pt.Temperature, MinTemp, MaxTemp) - MinTemp) / (MaxTemp - MinTemp)) * graphWidth;
                double ptY = PaddingTop + graphHeight - (Math.Clamp(pt.FanSpeedPercent, 0f, 100f) / 100.0) * graphHeight;
                polyPoints.Add(new WpfPoint(ptX, ptY));
            }

            double endX = PaddingLeft + graphWidth;
            double endY = PaddingTop + graphHeight - (sortedPoints[^1].FanSpeedPercent / 100.0) * graphHeight;
            polyPoints.Add(new WpfPoint(endX, endY));

            // Draw Area Fill Under Curve
            var areaFigure = new PathFigure { StartPoint = new WpfPoint(startX, PaddingTop + graphHeight) };
            areaFigure.Segments.Add(new LineSegment(new WpfPoint(startX, startY), false));
            foreach (var pt in polyPoints)
            {
                areaFigure.Segments.Add(new LineSegment(pt, false));
            }
            areaFigure.Segments.Add(new LineSegment(new WpfPoint(endX, PaddingTop + graphHeight), false));
            areaFigure.IsClosed = true;

            var areaGeometry = new PathGeometry();
            areaGeometry.Figures.Add(areaFigure);

            var areaPath = new Path
            {
                Data = areaGeometry,
                Fill = new LinearGradientBrush(
                    WpfColor.FromArgb(50, 0, 210, 255),
                    WpfColor.FromArgb(0, 0, 210, 255),
                    new WpfPoint(0, 0),
                    new WpfPoint(0, 1))
            };
            GraphCanvas.Children.Add(areaPath);

            // Draw Main Curve Line
            var polyline = new Polyline
            {
                Points = polyPoints,
                Stroke = new SolidColorBrush(WpfColor.FromRgb(0, 210, 255)),
                StrokeThickness = 2.8
            };
            GraphCanvas.Children.Add(polyline);

            // 4. Draw Interactive Draggable Node Points
            foreach (var pt in sortedPoints)
            {
                double ptX = PaddingLeft + ((Math.Clamp(pt.Temperature, MinTemp, MaxTemp) - MinTemp) / (MaxTemp - MinTemp)) * graphWidth;
                double ptY = PaddingTop + graphHeight - (Math.Clamp(pt.FanSpeedPercent, 0f, 100f) / 100.0) * graphHeight;

                var circle = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = new SolidColorBrush(WpfColor.FromRgb(0, 210, 255)),
                    Stroke = new SolidColorBrush(WpfColor.FromRgb(255, 255, 255)),
                    StrokeThickness = 2,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = pt,
                    ToolTip = $"Bấm giữ để kéo chỉnh: {pt.Temperature:F0}°C ➔ {pt.FanSpeedPercent:F0}%"
                };

                // Mouse interaction events for dragging
                circle.MouseEnter += (s, e) =>
                {
                    circle.Width = 18;
                    circle.Height = 18;
                    circle.Fill = new SolidColorBrush(WpfColor.FromRgb(56, 189, 248));
                    WpfCanvas.SetLeft(circle, ptX - 9);
                    WpfCanvas.SetTop(circle, ptY - 9);
                };

                circle.MouseLeave += (s, e) =>
                {
                    if (!_isDragging || _draggedPoint != pt)
                    {
                        circle.Width = 14;
                        circle.Height = 14;
                        circle.Fill = new SolidColorBrush(WpfColor.FromRgb(0, 210, 255));
                        WpfCanvas.SetLeft(circle, ptX - 7);
                        WpfCanvas.SetTop(circle, ptY - 7);
                    }
                };

                circle.MouseLeftButtonDown += (s, e) =>
                {
                    _draggedPoint = pt;
                    _draggedCircle = circle;
                    _isDragging = true;
                    circle.CaptureMouse();
                    e.Handled = true;
                };

                WpfCanvas.SetLeft(circle, ptX - 7);
                WpfCanvas.SetTop(circle, ptY - 7);
                GraphCanvas.Children.Add(circle);

                // Live Text Tag next to point
                var tagText = new WpfTextBlock
                {
                    Text = $"{pt.Temperature:F0}° / {pt.FanSpeedPercent:F0}%",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(WpfColor.FromRgb(240, 246, 252))
                };
                WpfCanvas.SetLeft(tagText, ptX - 18);
                WpfCanvas.SetTop(tagText, ptY - 22);
                GraphCanvas.Children.Add(tagText);
            }

            // 5. Draw Live Temperature Marker
            if (CurrentTemperature >= MinTemp && CurrentTemperature <= MaxTemp)
            {
                double curX = PaddingLeft + ((CurrentTemperature - MinTemp) / (MaxTemp - MinTemp)) * graphWidth;
                float curSpeed = Profile.CalculateFanSpeed(CurrentTemperature);
                double curY = PaddingTop + graphHeight - (curSpeed / 100.0) * graphHeight;

                var liveLine = new Line
                {
                    X1 = curX,
                    Y1 = PaddingTop,
                    X2 = curX,
                    Y2 = PaddingTop + graphHeight,
                    Stroke = new SolidColorBrush(WpfColor.FromRgb(249, 115, 22)),
                    StrokeThickness = 1.5,
                    StrokeDashArray = new DoubleCollection { 3, 2 }
                };
                GraphCanvas.Children.Add(liveLine);

                var curDot = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Fill = new SolidColorBrush(WpfColor.FromRgb(249, 115, 22)),
                    Stroke = new SolidColorBrush(WpfColor.FromRgb(255, 255, 255)),
                    StrokeThickness = 2,
                    ToolTip = $"Nhiệt độ hiện tại: {CurrentTemperature:F0}°C ➔ Tốc độ quạt: {curSpeed:F0}%"
                };
                WpfCanvas.SetLeft(curDot, curX - 6);
                WpfCanvas.SetTop(curDot, curY - 6);
                GraphCanvas.Children.Add(curDot);
            }
        }

        private void GraphCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging && _draggedPoint != null)
            {
                var pos = e.GetPosition(GraphCanvas);
                double graphWidth = GraphCanvas.ActualWidth - PaddingLeft - PaddingRight;
                double graphHeight = GraphCanvas.ActualHeight - PaddingTop - PaddingBottom;

                if (graphWidth > 0 && graphHeight > 0)
                {
                    double relX = (pos.X - PaddingLeft) / graphWidth;
                    double relY = 1.0 - ((pos.Y - PaddingTop) / graphHeight);

                    float newTemp = (float)Math.Round(MinTemp + Math.Clamp(relX, 0.0, 1.0) * (MaxTemp - MinTemp));
                    float newSpeed = (float)Math.Round(Math.Clamp(relY, 0.0, 1.0) * 100.0);

                    _draggedPoint.Temperature = Math.Clamp(newTemp, MinTemp, MaxTemp);
                    _draggedPoint.FanSpeedPercent = Math.Clamp(newSpeed, 0f, 100f);

                    RedrawGraph();
                    CurveChanged?.Invoke();
                }
            }
        }

        private void GraphCanvas_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _draggedCircle?.ReleaseMouseCapture();
                _draggedCircle = null;
                _draggedPoint = null;

                if (Profile != null && Profile.Points != null)
                {
                    Profile.Points = Profile.Points.OrderBy(p => p.Temperature).ToList();
                }

                RedrawGraph();
                CurveChanged?.Invoke();
            }
        }

        private void GraphCanvas_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                _draggedCircle?.ReleaseMouseCapture();
                _draggedCircle = null;
                _draggedPoint = null;

                if (Profile != null && Profile.Points != null)
                {
                    Profile.Points = Profile.Points.OrderBy(p => p.Temperature).ToList();
                }

                RedrawGraph();
                CurveChanged?.Invoke();
            }
        }

        private void GraphCanvas_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Double click on canvas to add a new point
            if (e.ClickCount == 2 && Profile != null)
            {
                var pos = e.GetPosition(GraphCanvas);
                double graphWidth = GraphCanvas.ActualWidth - PaddingLeft - PaddingRight;
                double graphHeight = GraphCanvas.ActualHeight - PaddingTop - PaddingBottom;

                if (graphWidth > 0 && graphHeight > 0)
                {
                    double relX = (pos.X - PaddingLeft) / graphWidth;
                    double relY = 1.0 - ((pos.Y - PaddingTop) / graphHeight);

                    if (relX >= 0.0 && relX <= 1.0 && relY >= 0.0 && relY <= 1.0)
                    {
                        float newTemp = (float)Math.Round(MinTemp + relX * (MaxTemp - MinTemp));
                        float newSpeed = (float)Math.Round(relY * 100.0);

                        Profile.Points.Add(new CurvePoint(newTemp, newSpeed));
                        Profile.Points = Profile.Points.OrderBy(p => p.Temperature).ToList();

                        RedrawGraph();
                        CurveChanged?.Invoke();
                    }
                }
            }
        }
    }
}
