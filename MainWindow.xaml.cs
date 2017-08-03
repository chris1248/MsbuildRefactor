using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
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
			LoadConfigFile();
		}

		private void LoadConfigFile()
		{
			vm.LoadConfigFile();
			searchPath.ItemsSource = vm.InputDirectories;
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

		private void Click_create_prop_sheet(object sender, RoutedEventArgs e)
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
				if (!searchPath.Items.Contains(directoryPath))
				{
					searchPath.Items.Add(directoryPath);
				}
				searchPath.Text = directoryPath;
				vm.LoadAtDirectory(directoryPath);
			}
			LoadingDirectory = false;
		}

		private void searchPath_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			LoadingDirectory = true;
			string path = searchPath.SelectedItem as string;
			vm.LoadAtDirectory(path);
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
			if (vm.PropSheet != null && e.Data.GetDataPresent("myFormat"))
			{
				ReferencedValues propdata = e.Data.GetData("myFormat") as ReferencedValues;
				if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
				{
					// Same as deleting a property
					vm.DeleteValue(propdata);
				}
				else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
				{
					vm.MoveValueAllConfigs(propdata);
				}
				else
				{
					vm.MoveProperty(propdata);
				}
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

		private void allPropsLV_KeyDown(object sender, KeyEventArgs e)
		{
			CommonShiftMessage();
		}

		private void allPropsLV_KeyUp(object sender, KeyEventArgs e)
		{
			if ((allPropsLV.SelectedItem != null) && (e.Key == Key.Delete))
			{
				var pair = (KeyValuePair<String, ReferencedProperty>)allPropsLV.SelectedItem;
				if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
				{
					vm.DeletePropertyXML(pair.Key);
				}
				else
				{
					vm.DeleteProperty(pair.Key);
				}
			}
			statusMessage.Text = String.Empty;
		}
		private void CommonShiftMessage()
		{
			if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
				statusMessage.Text = "Operate on ALL configurations and ALL platforms";
		}
		private void detailsLV_KeyDown(object sender, KeyEventArgs e)
		{
			CommonShiftMessage();
		}

		private void detailsLV_KeyUp(object sender, KeyEventArgs e)
		{
			if ((detailsLV.SelectedItem != null) && (e.Key == Key.Delete))
			{
				var p = (KeyValuePair<String, ReferencedValues>)detailsLV.SelectedItem;
				vm.DeleteValue(p.Value);
			}
			statusMessage.Text = String.Empty;
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
			var sb = new StringBuilder();
			foreach (MSBProject proj in query)
			{
				sb.AppendFormat("{0}\n", proj.FullPath);
			}
			string windowTitle = String.Format("Projects defining property value of: {0} for property: {1}", pair.Key, vals.Owner.Name);
			var stuff = new TextWindow(windowTitle, sb.ToString());
			stuff.Show();
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			var pair = (KeyValuePair<string, ReferencedProperty>)allPropsLV.SelectedItem;
			ReferencedProperty prop = pair.Value;
			var sb = new StringBuilder();
			foreach (ReferencedValues val in prop.PropertyValues.Values)
			{
				if (String.IsNullOrEmpty(val.EvaluatedValue))
					sb.AppendFormat("<blank>\n");
				else
					sb.AppendFormat("{0}\n", val.EvaluatedValue);

				foreach(MSBProject proj in val.Projects)
				{
					sb.AppendFormat("\t{0}\n", proj.FullPath);
				}
			}
			string windowTitle = String.Format("All Properties values for property: {0}", prop.Name);
			var stuff = new TextWindow(windowTitle, sb.ToString());
			stuff.Show();
		}

		private void OpenFile_Click(object sender, RoutedEventArgs e)
		{
			MSBProject proj = (MSBProject)allProjectsLV.SelectedItem;
			if (proj != null)
			{
				Process.Start(proj.FullPath);
			}
		}

		private void allProjectsLV_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			MSBProject proj = (MSBProject)allProjectsLV.SelectedItem;
			if (proj != null)
			{
				Process.Start(proj.FullPath);
			}
		}

		private void Operation_RemoveProperties_Click(object sender, RoutedEventArgs e)
		{
			vm.RemovePropertiesFromProjects();
		}

		private void Operation_RemoveProperties_All_Click(object sender, RoutedEventArgs e)
		{
			vm.RemoveAllPropertiesFromProjects();
		}

		private void Operation_RemoveEmptyProps_Click(object sender, RoutedEventArgs e)
		{
			vm.RemoveEmptyProps();
		}

		private void commonLV_KeyUp(object sender, KeyEventArgs e)
		{
			statusMessage.Text = String.Empty;
		}

		private void button_Click(object sender, RoutedEventArgs e)
		{
			vm.AttachImportForAll();
		}
	}
}
