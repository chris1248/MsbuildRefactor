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
	class ViewModel
	{
		private PropertyExtractor model;
		public ViewModel()
		{
			SelectedConfiguration = "Debug";
			SelectedPlatform = "AnyCPU";
		}
		public DirectoryInfo InputDir { get; set; }
		
		public CSProject PropSheet { get { return model.PropertySheet; } }

		public String SelectedConfiguration { get; set; }
		public String SelectedPlatform { get; set; }
		public List<CSProject> AllProjects { get { return model.AllProjects; } }
		public List<string> AllConfigurations { get { return model.AllConfigurations.Keys.ToList(); } }

		private ObservableCollection<CommonProperty> _commonProps;

		public ObservableCollection<CommonProperty> PropSheetProperties
		{
			get {
				// Lazy Initialize
				if (_commonProps == null)
				{
					_commonProps = new ObservableCollection<CommonProperty>();
				}
				foreach (ProjectProperty prop in model.PropertySheet.AllEvaluatedProperties)
				{
					if (prop.Xml != null)
						_commonProps.Add(new CommonProperty(prop.Name, prop.EvaluatedValue));
				}
				
				return _commonProps;
			}
		}

		public void MoveProperty(ReferencedValues prop)
		{
			string name  = prop.Owner.Name;
			string value = prop.EvaluatedValue;

			model.Move(name, value);
		}

		public ObservableConcurrentDictionary<String, ReferencedValues> SelectedValues = new ObservableConcurrentDictionary<string, ReferencedValues>();
		public ObservableConcurrentDictionary<String, ReferencedProperty> FoundProperties = new ObservableConcurrentDictionary<string, ReferencedProperty>();

		public void LoadPropertySheet(string prop_sheet_path)
		{
			model.PropertySheetPath = prop_sheet_path;
		}

		internal int LoadAtDirectory(string directoryPath, string ignorePattern)
		{
			model = new PropertyExtractor(directoryPath, SelectedConfiguration, SelectedPlatform);
			return model.Count;
		}

		internal void RemoveFoundProp(string key)
		{
			model.Remove(key);
		}

		internal void SaveAllProjects()
		{
			model.SaveAll();
		}

		internal void SavePropertySheet()
		{
			model.PropertySheet.Save();
		}
	}
}
