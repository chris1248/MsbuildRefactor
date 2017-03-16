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
using Refactor;

namespace msbuildrefactor
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private bool LoadingDirectory;
		public MainWindow()
		{
			InitializeComponent();
		}

		private void Click_choose_prop_sheet(object sender, RoutedEventArgs e)
		{
			// Create OpenFileDialog 
			var dlg = new Microsoft.Win32.OpenFileDialog();

			// Set filter for file extension and default file extension 
			dlg.DefaultExt = ".props";
			dlg.Filter = "Property Sheet files (*.props, *.xml)|*.props;*.xml|All files (*.*)|*.*";

			// Display OpenFileDialog by calling ShowDialog method 
			Nullable<bool> result = dlg.ShowDialog();

			// Get the selected file name and display in a TextBox 
			if (result == true)
			{
				propSheetPath.Text = dlg.FileName;
				vm.LoadPropertySheet(propSheetPath.Text);
			}
		}

		private void propSheetCreate_Click(object sender, RoutedEventArgs e)
		{
			var dlg = new Microsoft.Win32.SaveFileDialog();
			var result = dlg.ShowDialog(this);
			if (result == true)
			{
				propSheetPath.Text = dlg.FileName;
				// Even though this is not an existing file, it still will work the same
				vm.LoadPropertySheet(propSheetPath.Text);
			}
		}

		private void Click_choose_directory(object sender, RoutedEventArgs e)
		{
			LoadingDirectory = true;
			var browse = new System.Windows.Forms.FolderBrowserDialog();
			System.Windows.Forms.DialogResult result = browse.ShowDialog();
			if (result == System.Windows.Forms.DialogResult.OK)
			{
				string directoryPath = browse.SelectedPath;
				searchPath.Text = directoryPath;
				vm.LoadAtDirectory(directoryPath);
			}
			LoadingDirectory = false;
		}

		#region sorting
		private GridViewColumnHeader allPropsViewSortCol = null;
		private SortAdorner allPropsViewSortAdorner = null;

		private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
		{
			SortColumnBy(sender as GridViewColumnHeader, allPropsLV, ref allPropsViewSortCol, ref allPropsViewSortAdorner);
		}

		private GridViewColumnHeader detailsViewSortCol = null;
		private SortAdorner detailsViewSortAdorner = null;

		private void DetailsViewColumnHeader_Click(object sender, RoutedEventArgs e)
		{
			SortColumnBy(sender as GridViewColumnHeader, detailsLV, ref detailsViewSortCol, ref detailsViewSortAdorner);
		}

		private void SortColumnBy(GridViewColumnHeader column, ListView listView, ref GridViewColumnHeader sortColumn, ref SortAdorner sortAdorner)
		{
			if (sortColumn != null)
			{
				AdornerLayer.GetAdornerLayer(sortColumn).Remove(sortAdorner);
				listView.Items.SortDescriptions.Clear();
			}

			ListSortDirection newDir = ListSortDirection.Ascending;
			if (sortColumn == column && sortAdorner.Direction == newDir)
				newDir = ListSortDirection.Descending;

			sortColumn = column;
			sortAdorner = new SortAdorner(sortColumn, newDir);
			AdornerLayer.GetAdornerLayer(sortColumn).Add(sortAdorner);
			string sortBy = column.Tag.ToString();
			listView.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
		}
		#endregion

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
						var source = (KeyValuePair<String, ReferencedValues>)listView.ItemContainerGenerator.ItemFromContainer(listViewItem);
						ReferencedValues contact = source.Value;

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
				vm.MoveProperty(propdata);
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

		private void Click_savePropBtn(object sender, RoutedEventArgs e) { vm.SavePropertySheet(); }

		private void Click_saveAllBtn(object sender, RoutedEventArgs e) { vm.SaveAllProjects(); }

		private void allPropsLV_KeyUp(object sender, KeyEventArgs e)
		{
			if ((allPropsLV.SelectedItem != null) && (e.Key == Key.Delete))
			{
				var pair = (KeyValuePair<String, ReferencedProperty>)allPropsLV.SelectedItem;
				vm.DeleteProperty(pair.Key);
			}
		}

		private void detailsLV_KeyUp(object sender, KeyEventArgs e)
		{
			if ((detailsLV.SelectedItem != null) && (e.Key == Key.Delete))
			{
				var p = (KeyValuePair<String, ReferencedValues>)detailsLV.SelectedItem;
				vm.DeleteValue(p.Value);
			}
		}

		private void globalConfigs_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!LoadingDirectory)
				vm.UpdateConfigSelection();
		}

		private void globalPlatforms_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!LoadingDirectory)
				vm.UpdatePlatformSelection();
		}

		private void ShowProjects_Click(object sender, RoutedEventArgs e)
		{
			var pair = (KeyValuePair<string, ReferencedValues>)detailsLV.SelectedItem;
			ReferencedValues vals = pair.Value;
			var query = from proj in vals.Projects
						orderby proj.FullPath ascending
						select proj;
			StringBuilder sb = new StringBuilder();
			foreach (CSProject proj in query)
			{
				sb.AppendFormat("{0}\n", proj.FullPath);
			}
			string windowTitle = String.Format("Projects definining property value of: {0}", pair.Key);
			TextWindow stuff = new TextWindow(windowTitle, sb.ToString());
			stuff.Show();
		}
	}
}
