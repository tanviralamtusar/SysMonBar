using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        public MetricControl()
        {
            InitializeComponent();
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetricControl)d;
            ctrl.BarRect.Fill = (Brush)e.NewValue;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (MetricControl)d;
            ctrl.UpdateBar();
        }

        private void UpdateBar()
        {
            if (ActualHeight <= 0) return;
            double pct = MaxValue > 0 ? (Value / MaxValue) : 0;
            if (pct > 1) pct = 1;
            BarRect.Height = pct * ActualHeight;
            BarRect.Fill = Color;
        }
    }
}
