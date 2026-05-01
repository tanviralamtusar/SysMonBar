using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using UserControl = System.Windows.Controls.UserControl;

namespace SysMonBar
{
    public partial class MetricControl : UserControl
    {
        public static readonly DependencyProperty ColorProperty =
            DependencyProperty.Register("Color", typeof(Brush), typeof(MetricControl),
                new PropertyMetadata(Brushes.DodgerBlue, OnColorChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(MetricControl),
                new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty MaxValueProperty =
            DependencyProperty.Register("MaxValue", typeof(double), typeof(MetricControl),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty DisplayModeProperty =
            DependencyProperty.Register("DisplayMode", typeof(string), typeof(MetricControl),
                new PropertyMetadata("Bar", OnModeChanged));

        public static readonly DependencyProperty GraphWidthProperty =
            DependencyProperty.Register("GraphWidth", typeof(double), typeof(MetricControl),
                new PropertyMetadata(36.0, OnModeChanged));

        public static readonly DependencyProperty TextValueProperty =
            DependencyProperty.Register("TextValue", typeof(string), typeof(MetricControl),
                new PropertyMetadata("", OnTextValueChanged));

        private List<double> _history = new List<double>();

        public Brush Color
        {
            get => (Brush)GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double MaxValue
        {
            get => (double)GetValue(MaxValueProperty);
            set => SetValue(MaxValueProperty, value);
        }

        public string DisplayMode
        {
            get => (string)GetValue(DisplayModeProperty);
            set => SetValue(DisplayModeProperty, value);
        }

        public double GraphWidth
        {
            get => (double)GetValue(GraphWidthProperty);
            set => SetValue(GraphWidthProperty, value);
        }

        public string TextValue
        {
            get => (string)GetValue(TextValueProperty);
            set => SetValue(TextValueProperty, value);
        }

        public MetricControl()
        {
            InitializeComponent();
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetricControl)d;
            ctrl.BarRect.Fill = (Brush)e.NewValue;
            ctrl.TextDisplay.Foreground = (Brush)e.NewValue;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetricControl)d;
            ctrl.UpdateBar();
        }

        private static void OnTextValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetricControl)d;
            ctrl.TextDisplay.Text = (string)e.NewValue;
        }

        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetricControl)d;
            if (ctrl.DisplayMode == "Graph")
            {
                ctrl.Width = ctrl.GraphWidth;
            }
            else if (ctrl.DisplayMode == "Text")
            {
                ctrl.Width = double.NaN; // Auto
            }
            else
            {
                ctrl.Width = 12;
            }
            ctrl.UpdateBar();
        }

        private void UpdateBar()
        {
            if (ActualHeight <= 0) return;

            // Maintain history
            _history.Add(Value);
            if (_history.Count > 20) _history.RemoveAt(0);

            if (DisplayMode == "Graph")
            {
                BarRect.Visibility = Visibility.Collapsed;
                TextDisplay.Visibility = Visibility.Collapsed;
                GraphLine.Visibility = Visibility.Visible;
                GraphLine.Stroke = Color;

                GraphLine.Points.Clear();
                double maxVal = MaxValue > 0 ? MaxValue : 100;
                
                double widthScale = ActualWidth > 0 ? ActualWidth : Width;
                
                for (int i = 0; i < _history.Count; i++)
                {
                    double x = (i / (double)(20 - 1)) * widthScale;
                    
                    double pct = _history[i] / maxVal;
                    if (pct > 1) pct = 1;
                    if (pct < 0) pct = 0;
                    
                    double y = ActualHeight - (pct * ActualHeight);
                    GraphLine.Points.Add(new System.Windows.Point(x, y));
                }
            }
            else if (DisplayMode == "Text")
            {
                GraphLine.Visibility = Visibility.Collapsed;
                BarRect.Visibility = Visibility.Collapsed;
                TextDisplay.Visibility = Visibility.Visible;
            }
            else
            {
                GraphLine.Visibility = Visibility.Collapsed;
                TextDisplay.Visibility = Visibility.Collapsed;
                BarRect.Visibility = Visibility.Visible;

                double pct = MaxValue > 0 ? (Value / MaxValue) : 0;
                if (pct > 1) pct = 1;
                BarRect.Height = pct * ActualHeight;
                BarRect.Fill = Color;
            }
        }
    }
}
