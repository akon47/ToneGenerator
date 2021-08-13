using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ToneGenerator.AudioSource;

namespace ToneGenerator
{
	public sealed class ToneVisualizer : Canvas
	{
		public double Frequency
		{
			get { return (double)GetValue(FrequencyProperty); }
			set { SetValue(FrequencyProperty, value); }
		}
		public static readonly DependencyProperty FrequencyProperty = DependencyProperty.Register(
			"Frequency", typeof(double), typeof(ToneVisualizer),
			new FrameworkPropertyMetadata(1000.0d, FrameworkPropertyMetadataOptions.AffectsRender));

		public double Volume
		{
			get { return (double)GetValue(VolumeProperty); }
			set { SetValue(VolumeProperty, value); }
		}
		public static readonly DependencyProperty VolumeProperty = DependencyProperty.Register(
			"Volume", typeof(double), typeof(ToneVisualizer),
			new FrameworkPropertyMetadata(0.5d, FrameworkPropertyMetadataOptions.AffectsRender));

		public int Samples
		{
			get { return (int)GetValue(SamplesProperty); }
			set { SetValue(SamplesProperty, value); }
		}
		public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(
			"Samples", typeof(int), typeof(ToneVisualizer),
			new FrameworkPropertyMetadata(400, FrameworkPropertyMetadataOptions.AffectsRender));

		private Pen centerLinePen = new Pen(Brushes.DarkGray, 1.0d);
		private Pen graphPen = new Pen(Brushes.White, 1.0d);

		protected override void OnRender(DrawingContext dc)
		{
			base.OnRender(dc);


			double centerY = ActualHeight / 2.0d;
			double tincr = 2 * Math.PI * Frequency / 48000;

			PathGeometry pathGeometry = new PathGeometry();

			Point startPoint = new Point(0, centerY);
			for (int i = 1; i < Samples; i++)
			{
				double v = (Math.Sin(i * tincr) * Volume);
				Point endPoint = new Point((ActualWidth / Samples) * i, (centerY + (v * centerY)));
				//pathGeometry.AddGeometry(new EllipseGeometry(endPoint, 1, 1));
				pathGeometry.AddGeometry(new LineGeometry(startPoint, endPoint));
				startPoint = endPoint;
			}

			dc.DrawLine(centerLinePen, new Point(0, centerY), new Point(ActualWidth, centerY));
			dc.DrawGeometry(Brushes.White, graphPen, pathGeometry);
		}
	}
}
