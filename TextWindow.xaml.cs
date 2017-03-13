using System;
using System.Collections.Generic;
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

namespace msbuildrefactor
{
	/// <summary>
	/// Interaction logic for TextWindow.xaml
	/// </summary>
	public partial class TextWindow : Window
	{
		public TextWindow(string windowTitle, string content)
		{
			InitializeComponent();
			this.Title = windowTitle;
			textArea.Text = content;
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			Clipboard.SetText(textArea.Text);
		}
	}
}
