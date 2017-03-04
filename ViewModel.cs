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
		public ViewModel()
		{
			SelectedConfiguration = "Debug";
			SelectedPlatform = "AnyCPU";
		}
		public DirectoryInfo InputDir { get; set; }
		private CSProject _propSheet;
		public CSProject PropSheet { get { return _propSheet; } }

		private Dictionary<string,string> GetGlobalProperties()
		{
			var results = new Dictionary<string, string>();
			results.Add("Configuration", SelectedConfiguration);
			results.Add("Platform", SelectedPlatform);
			return results;
		}

		public String SelectedConfiguration { get; set; }
		public String SelectedPlatform { get; set; }

		private ObservableCollection<CommonProperty> _commonProps;

		public ObservableCollection<CommonProperty> PropSheetProperties
		{
			get {
				// Lazy Initialize
				if (_commonProps == null)
				{
					_commonProps = new ObservableCollection<CommonProperty>();
				}
				foreach (ProjectProperty prop in _propSheet.AllEvaluatedProperties)
				{
					if (prop.Xml != null)
						_commonProps.Add(new CommonProperty(prop.Name, prop.EvaluatedValue));
				}
				
				return _commonProps;
			}
		}

		public void MoveProperty(ReferencedValues prop)
		{
			string moved_name  = prop.Owner.Name;
			string moved_value = prop.EvaluatedValue;
			
			// Don't re-add a property to the property sheet if the value is already there
			string propexists = _propSheet.GetPropertyValue(moved_name);
			if (string.IsNullOrEmpty(propexists))
			{
				_propSheet.SetProperty(moved_name, moved_value);
				_propSheet.MarkDirty();
				_propSheet.ReevaluateIfNecessary();
				_commonProps.Add(new CommonProperty(moved_name, moved_value));
			}

			// Remove properties from the old files
			var toBeRemoved = new List<CSProject>();
			foreach (CSProject proj in prop.Owner.Projects)
			{
				var local = proj.GetProperty(moved_name);
				if (local != null) // && String.Compare(prop.EvaluatedValue, local.EvaluatedValue, StringComparison.InvariantCultureIgnoreCase) == 0)
				{
					if (local.Xml.Location.File.Contains(InputDir.FullName))
					{
						string local_value = local.EvaluatedValue;
						if (String.Compare(moved_name, "OutputPath", StringComparison.OrdinalIgnoreCase) == 0)
						{
							local_value = proj.OutputPath;
						}

						if (String.Compare(moved_value, local_value, StringComparison.OrdinalIgnoreCase) == 0)
						{
							if (proj.RemoveProperty(local))
							{
								toBeRemoved.Add(proj);
								proj.MarkDirty();
							}
						}
					}
				}

				
			}

			// Remove property from the reference List
			ReferencedProperty owner = prop.Owner;
			owner.RemoveProjects(toBeRemoved);
			if (owner.UsedCount == 0)
			{
				bool removed = FoundProperties.Remove(owner.Name);
				Debug.Assert(removed, "Property was not removed from the list");
			}

			// Modify the Values in the details List View
			SelectedValues.Remove(moved_value);
		}

		private void AttachImportIfNecessary(Project proj)
		{
			bool isCommonPropAttached = false;
			string name = Path.GetFileName(_propSheet.FullPath).ToLower();
			// Add in the import to the common property sheet
			foreach (ResolvedImport import in proj.Imports)
			{
				if (import.ImportedProject.FullPath.ToLower().Contains(name))
				{
					isCommonPropAttached = true;
				}
			}
			if (!isCommonPropAttached)
			{
				// Method one
				XDocument doc = XDocument.Load(proj.FullPath);
				Uri uc = new Uri(_propSheet.FullPath);
				Uri ui = new Uri(proj.FullPath);
				Uri dif = ui.MakeRelativeUri(uc);
				string relative = dif.OriginalString;
				XNamespace ns = doc.Root.Name.Namespace;
				XElement import = new XElement(ns + "Import", new XAttribute("Project", relative));
				IXmlLineInfo info = import as IXmlLineInfo;
				doc.Root.AddFirst(import);
				doc.Save(proj.FullPath);
				proj.MarkDirty();
				proj.ReevaluateIfNecessary();
			}
		}

		public void LoadPropertySheet(string prop_sheet_path)
		{
			_propSheet = new CSProject(prop_sheet_path, GetGlobalProperties(), "14.0");
		}

		public ObservableConcurrentDictionary<String, ReferencedProperty> FoundProperties = new ObservableConcurrentDictionary<string, ReferencedProperty>();

		private ObservableCollection<CSProject> _allProjects = new ObservableCollection<CSProject>();

		public ObservableCollection<CSProject> AllProjects => _allProjects;

		private ObservableCollection<String> _AllConfigurations = new ObservableCollection<String>();
		public ObservableCollection<string> AllConfigurations
		{
			get { return _AllConfigurations; }
			set { _AllConfigurations = value; }
		}

		private ObservableCollection<string> _AllPlatforms = new ObservableCollection<string>();
		public ObservableCollection<string> AllPlatforms
		{
			get { return _AllPlatforms; }
			set { _AllPlatforms = value; }
		}

		internal int LoadAtDirectory(string directoryPath, string ignorePattern)
		{
			InputDir = new DirectoryInfo(directoryPath);
			// The ignore pattern can contain more than one entry, delimted by comma's:
			String[] splits = ignorePattern.Split(',');
			var csprojects = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
			var global_props = GetGlobalProperties();
			// There are 4 of these
			// var vcprojects = Directory.GetFiles(directoryPath, "*.vcxproj", SearchOption.AllDirectories);
			foreach (var file in csprojects)
			{
				if (!string.IsNullOrEmpty(ignorePattern))
				{
					bool do_ignore = false;
					foreach (var ignore in splits)
					{
						if (file.ToLower().Contains(ignore.ToLower()))
						{
							do_ignore = true;
							break;
						}
					}
					if (do_ignore)
					{
						continue;
					}
				}

				IterateFile(file, global_props);
			}
			return csprojects.Count();
		}

		internal void RemoveFoundProp(string key)
		{
			ReferencedProperty removal = FoundProperties[key];
			foreach (Project proj in removal.Projects)
			{
				ProjectProperty p = proj.GetProperty(key);
				if (p == null || p.IsImported || p.IsGlobalProperty || p.IsEnvironmentProperty || p.IsReservedProperty)
					continue;
				proj.RemoveProperty(p);
			}
			bool removed = FoundProperties.Remove(key);
			Debug.Assert(removed);

			var selected = (ICollection<KeyValuePair<String, ReferencedValues>>)SelectedValues;
			selected.Clear();
		}

		private void IterateFile(string file, IDictionary<string, string> props)
		{
			CSProject project = null;
			try
			{
				project = new CSProject(file, props, "14.0");
			}
			catch (Exception e)
			{
				Debug.Print("Exception opening file: {0}", file);
				Debug.Print(e.Message);
				return;
			}

			_allProjects.Add(project);
			FindConfigAndPlatform(project);

			GetPropertiesFrom(project);
		}

		private void GetPropertiesFrom(CSProject project)
		{
			foreach (ProjectProperty prop in project.AllEvaluatedProperties)
			{
				if (!prop.IsImported && !prop.IsEnvironmentProperty && !prop.IsReservedProperty)
				{
					string key = prop.Name;
					if (FoundProperties.ContainsKey(key))
					{
						FoundProperties[key].Add(project);
					}
					else
					{
						FoundProperties[key] = new ReferencedProperty(prop, project) { UsedCount = 1 };
					}
				}
			}
		}

		public void ResetGlobalProperties()
		{
			var selected = (ICollection<KeyValuePair<String, ReferencedProperty>>)FoundProperties;
			selected.Clear();
			var values = (ICollection<KeyValuePair<String, ReferencedValues>>)SelectedValues;
			values.Clear();
			foreach(CSProject proj in _allProjects)
			{
				proj.SetGlobalProperty("Configuration", SelectedConfiguration);
				proj.SetGlobalProperty("Platform", SelectedPlatform);
				proj.ReevaluateIfNecessary();
				GetPropertiesFrom(proj);
			}
			_propSheet.SetGlobalProperty("Configuration", SelectedConfiguration);
			_propSheet.SetGlobalProperty("Platform", SelectedPlatform);
			_propSheet.ReevaluateIfNecessary();
			_commonProps.Clear();
			//var common = PropSheetProperties;
		}

		private void FindConfigAndPlatform(CSProject project)
		{
			IDictionary<string, List<string>> conProps = project.ConditionedProperties;
			List<String> configs = conProps["Configuration"];
			List<String> platforms = conProps["Platform"];
			foreach (var config in configs)
			{
				if (!AllConfigurations.Contains(config))
					AllConfigurations.Add(config);
			}
			foreach (var platform in platforms)
			{
				if (!AllPlatforms.Contains(platform))
					AllPlatforms.Add(platform);
			}
		}

		public ObservableConcurrentDictionary<String, ReferencedValues> SelectedValues = new ObservableConcurrentDictionary<string, ReferencedValues>();

		internal void GetPropertyValues(ReferencedProperty item)
		{
			var selected = (ICollection<KeyValuePair<String, ReferencedValues>>)SelectedValues;
			selected.Clear();
			foreach (var project in item.Projects)
			{
				ProjectProperty itemprop = project.GetProperty(item.Name);
				if (itemprop != null)
				{
					string key = itemprop.EvaluatedValue.ToLower();
					if (item.Name == "OutputPath")
					{
						key = project.OutputPath; 
					}
					
					if (SelectedValues.ContainsKey(key))
					{
						SelectedValues[key].Count++;
					}
					else
					{
						SelectedValues[key] = new ReferencedValues() { EvaluatedValue = key, Count = 1, Owner = item };
					}
				}
			}
		}

		internal void SaveAllProjects()
		{
			foreach(ReferencedProperty prop in FoundProperties.Values)
			{
				foreach(Project proj in prop.Projects)
				{
					if (proj.IsDirty)
					{
						proj.Save();
						AttachImportIfNecessary(proj);
						proj.ReevaluateIfNecessary();
					}
				}
			}
		}

		internal void SavePropertySheet()
		{
			_propSheet.Save();
			_propSheet.ReevaluateIfNecessary();
		}
	}
}
