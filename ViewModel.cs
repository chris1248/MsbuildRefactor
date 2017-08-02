using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Evaluation;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using Refactor;

namespace msbuildrefactor
{
	/// <summary>
	/// Class used as a DataContext for the Window. This contains most of the business logic. 
	/// It is also handles the data binding for the controls. This class is pretty much agnostic about the UI.
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

		#region Methods are called from UI Controls

		/// <summary>
		/// Loads a single property sheet into the system.
		/// </summary>
		/// <param name="prop_sheet_path">The path to the property sheet</param>
		public void LoadPropertySheet(string prop_sheet_path)
		{
			PropertySheetPath = prop_sheet_path;
			model.PropertySheetPath = prop_sheet_path;
			OnPropertyChanged("PropSheetProperties");
		}

		/// <summary>
		/// Loads all *.csproj and *.vcxproj files found under a directory.
		/// It searches all subdirectories.
		/// </summary>
		/// <param name="directoryPath">The path to search</param>
		/// <returns>The number of files discovered</returns>
		public int LoadAtDirectory(string directoryPath)
		{
			model.SetInputDirectory(directoryPath);
			OnPropertyChanged("FoundProperties");
			OnPropertyChanged("AllProjects");
			OnPropertyChanged("AllConfigurations");
			OnPropertyChanged("AllPlatforms");
			return model.Count;
		}

		/// <summary>
		/// Removes a property that matches a given value from all the projects for the current configuration and current platform.
		/// The property is then added to the property sheet with the given value.
		/// </summary>
		/// <param name="val">The given ReferenceValues instance to move</param>
		public void MoveProperty(ReferencedValues val)
		{
			model.MoveValue(val);
			OnPropertyChanged("FoundProperties");
			OnPropertyChanged("PropSheetProperties");
		}

		/// <summary>
		/// Removes a property that matches a given value from all the projects for ALL configuration and ALL platforms.
		/// The property is then added to the property sheet with the given value.
		/// </summary>
		/// <param name="val">The given ReferenceValues instance to move</param>
		public void MoveValueAllConfigs(ReferencedValues val)
		{
			model.MoveValueAllConfigs(val);
			OnPropertyChanged("FoundProperties");
			OnPropertyChanged("PropSheetProperties");
		}

		/// <summary>
		/// Deletes one property across all projects for the current configuration and current platform
		/// </summary>
		/// <param name="key"></param>
		public void DeleteProperty(string key)
		{
			model.Remove(key);
			OnPropertyChanged("FoundProperties");
		}

		/// <summary>
		/// Deletes one property across all projects for all configurations and all platforms
		/// </summary>
		/// <param name="key">The name of the property to remove</param>
		public void DeletePropertyXML(string key)
		{
			var props = new List<string>() { key };
			model.RemoveXml(props);
			OnPropertyChanged("FoundProperties");
		}

		/// <summary>
		/// Used to delete only a property that has a particular given value. It will
		/// only delete the value for the current configuration and current platform.
		/// </summary>
		/// <param name="val"></param>
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

		/// <summary>
		/// Removes all properties from the project files that are already in the property sheet (For the current configuration)
		/// </summary>
		public void RemovePropertiesFromProjects()
		{
			var names = (from p in PropSheet.Properties
						where !p.IsEnvironmentProperty
						where !p.IsGlobalProperty
						where !p.IsImported
						where !p.IsReservedProperty
						select p.Name).ToList();
			model.Remove(names);
			OnPropertyChanged("FoundProperties");
		}

		/// <summary>
		/// Removes all properties from the project files that are already in the property sheet (For ALL configurations)
		/// </summary>
		public void RemoveAllPropertiesFromProjects()
		{
			var names = (from p in PropSheet.Properties
						where !p.IsGlobalProperty
						where !p.IsReservedProperty
						where !p.IsImported
						where !p.IsEnvironmentProperty
						select p.Name).ToList();
			model.RemoveXml(names);
			OnPropertyChanged("FoundProperties");
		}

		/// <summary>
		/// Removes empty XML Elements in all projects for all configurations
		/// </summary>
		public void RemoveEmptyProps()
		{
			model.RemoveEmptyXMLElements();
			OnPropertyChanged("FoundProperties");
		}

		public void AttachImportForAll()
		{
			model.AttachImportForAll();
		}
		#endregion
	}
}
