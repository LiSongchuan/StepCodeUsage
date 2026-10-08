using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace StepCodeUsage.Converters
{
    public class PercentageToArcConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 3 ||
                !(values[0] is double width) ||
                !(values[1] is double height) ||
                !(values[2] is double percentage))
            {
                return DependencyProperty.UnsetValue;
            }

            if (width <= 0 || height <= 0) return DependencyProperty.UnsetValue;

            double radius = Math.Min(width, height) / 2 - 10; // 留出边距，10是圆环厚度的一半
            if (radius <= 0) return DependencyProperty.UnsetValue;

            double centerX = width / 2;
            double centerY = height / 2;

            // 限制百分比在 0~1 之间
            percentage = Math.Max(0, Math.Min(1, percentage));

            // 如果是100%，直接画一个完整的圆
            if (percentage >= 1)
            {
                return Geometry.Parse($"M {centerX},{centerY - radius} A {radius},{radius} 0 1 1 {centerX - 0.01},{centerY - radius} Z");
            }

            // 计算终点坐标
            double angle = percentage * 360;
            double radians = angle * Math.PI / 180;

            // 起始点在正上方 (centerX, centerY - radius)
            // 顺时针画圆
            double endX = centerX + radius * Math.Sin(radians);
            double endY = centerY - radius * Math.Cos(radians);

            // 是否是大弧（超过50%）
            bool isLargeArc = percentage > 0.5;

            // 构建 PathGeometry
            PathFigure figure = new PathFigure
            {
                StartPoint = new System.Windows.Point(centerX, centerY - radius),
                IsClosed = false
            };

            figure.Segments.Add(new ArcSegment
            {
                Point = new System.Windows.Point(endX, endY),
                Size = new System.Windows.Size(radius, radius),
                IsLargeArc = isLargeArc,
                SweepDirection = SweepDirection.Clockwise
            });

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            return geometry;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
