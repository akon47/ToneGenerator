using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ToneGenerator.AudioSource;

namespace ToneGenerator
{
	/// <summary>
	/// MainWindow.xaml에 대한 상호 작용 논리
	/// </summary>
	public partial class MainWindow : Window
	{
		public ToneAudioRenderer ToneAudioRenderer
		{
			get { return (ToneAudioRenderer)GetValue(ToneAudioRendererProperty); }
			set { SetValue(ToneAudioRendererProperty, value); }
		}

		public static readonly DependencyProperty ToneAudioRendererProperty = DependencyProperty.Register(
			"ToneAudioRenderer", typeof(ToneAudioRenderer), typeof(MainWindow), new PropertyMetadata(null));

		public MainWindow()
		{
			InitializeComponent();
			ToneAudioRenderer = new ToneAudioRenderer();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			base.OnClosing(e);

			ToneAudioRenderer?.Dispose();
		}
	}
}
