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
			model = new PropertyExtractor(SelectedConfiguration, SelectedPlatform);
		}

		#region Properties for Data Binding
		public MSBProject PropSheet { get { return model.PropertySheet; } }
		public String SelectedConfiguration { get; set; }
		public String SelectedPlatform { get; set; }
		public List<MSBProject> AllProjects { get { return model.AllProjects; } }
		public List<String> AllConfigurations { get { return model.AllConfigurations.Keys.ToList(); } }
		public List<String> AllPlatforms { get { return model.AllPlatforms.Keys.ToList(); } }
		public string PropertySheetPath { get; set; }
		private ObservableCollection<CommonProperty> _propSheetProperties = new ObservableCollection<CommonProperty>();
		public ObservableCollection<CommonProperty> PropSheetProperties
		{
			get
			{
				if (model.PropertySheet != null)
				{
					_propSheetProperties.Clear();
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
		public ObservableConcurrentDictionary<String, ReferencedProperty> FoundProperties { get { return model.AllFoundProperties; } }
		#endregion

		#region Methods for calling from UI Controls
		public void LoadPropertySheet(string prop_sheet_path)
		{
			PropertySheetPath = prop_sheet_path;
			model.PropertySheetPath = prop_sheet_path;
			OnPropertyChanged("PropSheetProperties");
		}

		public int LoadAtDirectory(string directoryPath)
		{
			model.SetInputDirectory(directoryPath);
			OnPropertyChanged("FoundProperties");
			OnPropertyChanged("AllProjects");
			OnPropertyChanged("AllConfigurations");
			OnPropertyChanged("AllPlatforms");
			return model.Count;
		}

		public void MoveProperty(ReferencedValues val)
		{
			model.MoveValue(val);
			OnPropertyChanged("FoundProperties");
			OnPropertyChanged("PropSheetProperties");
		}

		public void DeleteProperty(string key)
		{
			model.Remove(key);
			OnPropertyChanged("FoundProperties");
		}

		/// <summary>
		/// Deletes one property across all projects for all configurations and all platforms
		/// </summary>
		/// <param name="key"></param>
		public void DeletePropertyXML(string key)
		{
			var props = new List<string>() { key };
			model.RemoveXml(props);
			OnPropertyChanged("FoundProperties");
		}

		public void DeleteValue(ReferencedValues val)
		{
			model.Remove(val);
			OnPropertyChanged("FoundProperties");
		}

		public void SaveAllProjects() { model.SaveAll(); }
		public void SavePropertySheet() { model.PropertySheet.Save(); }
		public void UpdateConfigSelection()
		{
			model.SetGlobalProperty("Configuration", SelectedConfiguration);
			OnPropertyChanged("PropSheetProperties");
		}
		public void UpdatePlatformSelection()
		{
			model.SetGlobalProperty("Platform", SelectedPlatform);
			OnPropertyChanged("PropSheetProperties");
		}

		public void RemovePropertiesFromProjects()
		{
			var names = from p in PropSheet.Properties
						select p.Name;
			model.Remove(names.ToList());
		}

		public void RemoveAllPropertiesFromProjects()
		{
			var names = from p in PropSheet.Properties
						where !p.IsGlobalProperty
						where !p.IsReservedProperty
						where !p.IsImported
						where !p.IsEnvironmentProperty
						select p.Name;
			model.RemoveXml(names.ToList());
			OnPropertyChanged("FoundProperties");
		}

		public void RemoveEmptyProps()
		{
			model.RemoveEmptyXMLElements();
			OnPropertyChanged("FoundProperties");
		}
		#endregion
	}
}
