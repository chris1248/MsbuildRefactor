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

namespace msbuildrefactor
{
	class ViewModel
	{
		public DirectoryInfo InputDir { get; set; }
		private Project _propSheet;
		public Project PropSheet { get { return _propSheet; } }

		private ObservableCollection<CommonProperty> _commonProps;

		public ObservableCollection<CommonProperty> PropSheetProperties
		{
			get {
				// Lazy Initialize
				if (_commonProps == null)
				{
					_commonProps = new ObservableCollection<CommonProperty>();
					foreach (ProjectProperty prop in _propSheet.AllEvaluatedProperties)
					{
						if (prop.Xml != null)
							_commonProps.Add(new CommonProperty(prop.Name, prop.EvaluatedValue));
					}
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
			var toBeRemoved = new List<Project>();
			foreach (Project proj in prop.Owner.Projects)
			{
				var local = proj.GetProperty(moved_name);
				if (local != null) // && String.Compare(prop.EvaluatedValue, local.EvaluatedValue, StringComparison.InvariantCultureIgnoreCase) == 0)
				{
					if (local.Xml.Location.File.Contains(InputDir.FullName))
					{
						string local_value = local.EvaluatedValue;
						if (String.Compare(moved_name, "OutputPath", StringComparison.OrdinalIgnoreCase) == 0)
						{
							local_value = ConcatenatePathProp(proj.FullPath, local.UnevaluatedValue);
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
			prop.Owner.RemoveProjects(toBeRemoved);

			// Modify the Values in the details List View
			_selectedVals.Remove(moved_value);
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

		public void LoadPropertySheet(string prop_sheet_path, IDictionary<string,string> props)
		{
			_propSheet = new Project(prop_sheet_path, props, "14.0");
		}


		private Dictionary<string, ReferencedProperty> refs = new Dictionary<string, ReferencedProperty>();
		internal int LoadAtDirectory(string directoryPath, IDictionary<string, string> props, string ignorePattern)
		{
			InputDir = new DirectoryInfo(directoryPath);
			// The ignore pattern can contain more than one entry, delimted by comma's:
			String[] splits = ignorePattern.Split(',');
			var csprojects = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
			// There are 4 of these
			// var vcprojects = Directory.GetFiles(directoryPath, "*.vcxproj", SearchOption.AllDirectories);
			foreach (var file in csprojects)
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

				IterateFile(file, props);
			}
			return csprojects.Count();
		}

		private void IterateFile(string file, IDictionary<string, string> props)
		{
			Project project = null;
			try
			{
				project = new Project(file, props, "14.0");
			}
			catch(Exception e)
			{
				Debug.Print("Exception opening file: {0}", file);
				Debug.Print(e.Message);
				return;
			}

			foreach (ProjectProperty prop in project.AllEvaluatedProperties)
			{
				if (!prop.IsImported && !prop.IsEnvironmentProperty && !prop.IsReservedProperty)
				{
					string key = prop.Name;
					if (refs.ContainsKey(key))
					{
						refs[key].Add(project);
					}
					else
					{
						refs[key] = new ReferencedProperty(prop) { UsedCount = 1 };
					}
				}
			}
		}

		public List<ReferencedProperty> FoundProperties => refs.Values.ToList();
		
		private Dictionary<String, ReferencedValues> _selectedVals;

		public List<ReferencedValues> SelectedValues => _selectedVals.Values.ToList();

		internal string ConcatenatePathProp(string projectPath , string property_value )
		{
			var relative = property_value;
			var basepath = Path.GetDirectoryName(projectPath);
			var combined = Path.GetFullPath(Path.Combine(basepath, relative));
			return combined.ToLower();
		}

		internal void GetPropertyValues(ReferencedProperty item)
		{
			_selectedVals = new Dictionary<String, ReferencedValues>();
			foreach (var project in item.Projects)
			{
				ProjectProperty itemprop = project.GetProperty(item.Name);
				if (itemprop != null)
				{
					string key = itemprop.EvaluatedValue.ToLower();
					if (item.Name == "OutputPath")
					{
						key = ConcatenatePathProp(project.FullPath, itemprop.EvaluatedValue);
					}
					
					if (_selectedVals.ContainsKey(key))
					{
						_selectedVals[key].Count++;
					}
					else
					{
						_selectedVals[key] = new ReferencedValues() { EvaluatedValue = key, Count = 1, Owner = item };
					}
				}
			}
		}

		internal void SaveAllProjects()
		{
			foreach(ReferencedProperty prop in refs.Values)
			{
				foreach(Project proj in prop.Projects)
				{
					if (proj.IsDirty)
					{
						proj.Save();
						proj.ReevaluateIfNecessary();
						AttachImportIfNecessary(proj);
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
