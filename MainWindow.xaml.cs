using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
	public class User
	{
		public string Name { get; set; }
		public int Age { get; set; }
		public string EvaluatedValue { get; set; }
		public override string ToString()
		{
			return this.Name + ", " + this.Age + " years old";
		}
	}
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			InitializeComponent();
		}

		private Dictionary<string, string> global_properties = new Dictionary<string, string>();

		private void Click_choose_prop_sheet(object sender, RoutedEventArgs e)
		{
			if (global_properties.Count == 0)
			{
				global_properties.Add("Configuration", configInput.Text);
				global_properties.Add("Platform", platformInput.Text);
			}

			// Create OpenFileDialog 
			Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
			
			// Set filter for file extension and default file extension 
			dlg.DefaultExt = ".props";
			dlg.Filter = "Property Sheet files (*.props, *.xml)|*.props;*.xml|All files (*.*)|*.*";

			// Display OpenFileDialog by calling ShowDialog method 
			Nullable<bool> result = dlg.ShowDialog();

			// Get the selected file name and display in a TextBox 
			if (result == true)
			{
				propSheetPath.Text = dlg.FileName;
				vm.LoadPropertySheet(propSheetPath.Text, global_properties);
				commonLV.ItemsSource = vm.PropSheetProperties;
			}
		}

		private void Click_choose_directory(object sender, RoutedEventArgs e)
		{
			if (global_properties.Count == 0)
			{
				global_properties.Add("Configuration", configInput.Text);
				global_properties.Add("Platform", platformInput.Text);
			}

			var browse = new System.Windows.Forms.FolderBrowserDialog();
			System.Windows.Forms.DialogResult result = browse.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				string directoryPath = browse.SelectedPath;
				searchPath.Text = directoryPath;
				int count = vm.LoadAtDirectory(directoryPath, global_properties, ignorePatternTBx.Text);
				projCount.Text = String.Format("Number of files: {0}", count);
				allPropsLV.ItemsSource = vm.FoundProperties;
			}
		}

		#region sorting
		private GridViewColumnHeader listViewSortCol = null;
		private SortAdorner listViewSortAdorner = null;

		private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
		{
			GridViewColumnHeader column = (sender as GridViewColumnHeader);
			if (listViewSortCol != null)
			{
				AdornerLayer.GetAdornerLayer(listViewSortCol).Remove(listViewSortAdorner);
				allPropsLV.Items.SortDescriptions.Clear();
			}

			ListSortDirection newDir = ListSortDirection.Ascending;
			if (listViewSortCol == column && listViewSortAdorner.Direction == newDir)
				newDir = ListSortDirection.Descending;

			listViewSortCol = column;
			listViewSortAdorner = new SortAdorner(listViewSortCol, newDir);
			AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);
			string sortBy = column.Tag.ToString();
			allPropsLV.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
		}
		#endregion

		private void allPropsLV_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ReferencedProperty item = allPropsLV.SelectedItem as ReferencedProperty;
			if (item != null)
			{
				vm.GetPropertyValues(item);
				detailsLV.ItemsSource = vm.SelectedValues;
			}
		}

		#region drag and drop
		private void detailsLV_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			// Store the mouse position
			startPoint = e.GetPosition(null);
		}

		private Point startPoint;
		private void detailsLV_MouseMove(object sender, MouseEventArgs e)
		{
			if (e.LeftButton == MouseButtonState.Pressed)
			{
				Point mousePos = e.GetPosition(null);
				Vector diff = startPoint - mousePos;

				if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
					Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
				{
					// Get the dragged ListViewItem
					ListView listView = sender as ListView;
					ListViewItem listViewItem = FindAnchestor<ListViewItem>((DependencyObject)e.OriginalSource);

					if (listViewItem != null)
					{
						// Find the data behind the ListViewItem
						ReferencedValues contact = (ReferencedValues)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);

						// Initialize the drag & drop operation
						DataObject dragData = new DataObject("myFormat", contact);
						DragDrop.DoDragDrop(listViewItem, dragData, DragDropEffects.Move);
					}
				}
			}
		}

		// Helper to search up the VisualTree
		private static T FindAnchestor<T>(DependencyObject current)
			where T : DependencyObject
		{
			do
			{
				if (current is T)
				{
					return (T)current;
				}
				current = VisualTreeHelper.GetParent(current);
			}
			while (current != null);
			return null;
		}

		private void commonLV_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent("myFormat"))
			{
				ReferencedValues propdata = e.Data.GetData("myFormat") as ReferencedValues;
				ListView listView = sender as ListView;
				vm.MoveProperty(propdata);
				detailsLV.ItemsSource = vm.SelectedValues;
			}
		}

		private void commonLV_DragEnter(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent("myFormat") || sender == e.Source)
			{
				e.Effects = DragDropEffects.None;
			}
		}
		#endregion
	}
}
