using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using System.Collections.ObjectModel;
using System.IO;
using System.Diagnostics;

namespace msbuildrefactor
{
	class ViewModel
	{
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
							_commonProps.Add(new CommonProperty(prop));
					}
				}
				return _commonProps;
			}
		}

		public void MoveProperty(ReferencedValues prop)
		{
			ReferencedProperty owner = prop.Owner;
			ProjectProperty moved = owner.OriginalProperty;
			CommonProperty newprop = new CommonProperty(moved);
			_propSheet.SetProperty(moved.Name, moved.UnevaluatedValue);
			_propSheet.Save();
			_commonProps.Add(newprop);

			// Remove properties from the old files
			var toBeRemoved = new List<Project>();
			foreach(Project proj in owner.Projects)
			{
				var local = proj.GetProperty(moved.Name);
				if (local != null && String.Compare(moved.EvaluatedValue, local.EvaluatedValue) == 0)
				{
					if (proj.RemoveProperty(local))
					{
						toBeRemoved.Add(proj);
						proj.Save();
					}
				}
			}

			// Remove property from the reference List
			owner.RemoveProjects(toBeRemoved);

			// Modify the Values in the details List View
			_selectedVals.Remove(moved.EvaluatedValue);
		}

		public void LoadPropertySheet(string prop_sheet_path, IDictionary<string,string> props)
		{
			_propSheet = new Project(prop_sheet_path, props, "14.0");
		}


		private Dictionary<string, ReferencedProperty> refs = new Dictionary<string, ReferencedProperty>();
		internal int LoadAtDirectory(string directoryPath, IDictionary<string, string> props, string ignorePattern)
		{
			var csprojects = Directory.GetFiles(directoryPath, "*.csproj", SearchOption.AllDirectories);
			// There are 4 of these
			// var vcprojects = Directory.GetFiles(directoryPath, "*.vcxproj", SearchOption.AllDirectories);
			foreach (var file in csprojects)
			{
				if (file.ToLower().Contains(ignorePattern.ToLower()))
					continue;

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

		internal void GetPropertyValues(ReferencedProperty item)
		{
			_selectedVals = new Dictionary<String, ReferencedValues>();
			foreach (var project in item.Projects)
			{
				ProjectProperty itemprop = project.GetProperty(item.Name);
				if (itemprop != null)
				{
					string key = itemprop.EvaluatedValue.ToLower();
					if (_selectedVals.ContainsKey(key))
					{
						_selectedVals[key].Count++;
					}
					else
					{
						_selectedVals[key] = new ReferencedValues() { Value = key, Count = 1, Owner = item };
					}
				}
			}
		}
	}
}
