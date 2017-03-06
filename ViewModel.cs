using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;
using System.Xml.Linq;
using System.Xml;
using System.Collections;
using System.Collections.Concurrent;
using Refactor;

namespace msbuildrefactor
{
	/// <summary>
	/// Class used as a DataContext for the Window. This contains most of the UI logic and
	/// also handles the data binding for the controls.
	/// </summary>
	class ViewModel : BaseProperty
	{
		private PropertyExtractor model;
		public ViewModel()
		{
			SelectedConfiguration = "Debug";
			SelectedPlatform = "AnyCPU";
		}

		#region Properties for Data Binding
		public CSProject PropSheet { get { return model.PropertySheet; } }
		public String SelectedConfiguration { get; set; }
		public String SelectedPlatform { get; set; }
		public List<CSProject> AllProjects
		{
			get
			{
				if (model != null)
					return model.AllProjects;
				else
					return new List<CSProject>();
			}
		}
		public List<String> AllConfigurations
		{
			get
			{
				if (model != null)
				{
					var configs = model.AllConfigurations;
					return configs.Keys.ToList();
				}
				else
				{
					return new List<String>();
				}
			}
		}
		public List<String> AllPlatforms
		{
			get
			{
				if (model != null)
				{
					var platforms = model.AllPlatforms;
					return platforms.Keys.ToList();
				}
				else
				{
					return new List<String>();
				}
			}
		}

		private ObservableCollection<CommonProperty> _propSheetProperties = new ObservableCollection<CommonProperty>();
		public ObservableCollection<CommonProperty> PropSheetProperties
		{
			get {
				if ((model != null) && (model.PropertySheet != null))
				{
					foreach (ProjectProperty prop in model.PropertySheet.AllEvaluatedProperties)
					{
						if (prop.Xml != null)
							_propSheetProperties.Add(new CommonProperty(prop.Name, prop.EvaluatedValue));
					}
				}
				return _propSheetProperties;
			}
		}
		public int CountFoundFiles { get { return model.CountFoundFiles; } }
		public Dictionary<String, ReferencedProperty> FoundProperties
		{
			get
			{
				if (model != null)
					return model.AllFoundProperties;
				else
					return new Dictionary<string, ReferencedProperty>();
			}
		}
		#endregion

		#region Methods for calling from UI Controls
		public void LoadPropertySheet(string prop_sheet_path)
		{
			model.PropertySheetPath = prop_sheet_path;
			OnPropertyChanged("PropSheetProperties");
		}

		public int LoadAtDirectory(string directoryPath)
		{
			model = new PropertyExtractor(directoryPath, SelectedConfiguration, SelectedPlatform);
			OnPropertyChanged("FoundProperties");
			OnPropertyChanged("AllProjects");
			OnPropertyChanged("AllConfigurations");
			OnPropertyChanged("AllPlatforms");
			return model.Count;
		}

		public void MoveProperty(ReferencedValues prop)
		{
			string name = prop.Owner.Name;
			string value = prop.EvaluatedValue;

			model.Move(name, value);
			OnPropertyChanged("FoundProperties");
		}

		public void DeleteProperty(string key)
		{
			model.Remove(key);
			OnPropertyChanged("FoundProperties");
		}

		public void SaveAllProjects()
		{
			model.SaveAll();
		}

		public void SavePropertySheet()
		{
			model.PropertySheet.Save();
		}

		public void UpdateConfigSelection()
		{
			model.SetGlobalProperty("Configuration", SelectedConfiguration);
		}

		public void UpdatePlatformSelection()
		{
			model.SetGlobalProperty("Platform", SelectedPlatform);
		}
		#endregion
	}
}
