using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using Microsoft.Build.Evaluation;
using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;
using Microsoft.Build.Construction;

namespace Refactor
{
	public class PropertyExtractor
	{
		#region Fields
		#region Input Fields
		private string config;
		private string inputDir;
		private string platform;
		private string propSheet;
		private readonly string toolsVersion = "14.0";
		#endregion

		#region Data Fields
		private MSBProject _propertySheet;
		private List<MSBProject> _allProjects;
		private Dictionary<String, String> _globalProperties = new Dictionary<string, string>();
		private Dictionary<String, int> _allConfigurations = new Dictionary<String, int>();
		private Dictionary<String, int> _allPlatforms = new Dictionary<String, int>();
		private ObservableConcurrentDictionary<String, ReferencedProperty> _allFoundProperties = new ObservableConcurrentDictionary<string, ReferencedProperty>();
		#endregion

		#region Properties
		public List<MSBProject> AllProjects { get { return _allProjects; } }
		public Dictionary<String, int> AllConfigurations { get { return _allConfigurations; } }
		public Dictionary<String, int> AllPlatforms { get { return _allPlatforms; } }
		public ObservableConcurrentDictionary<String, ReferencedProperty> AllFoundProperties { get { return _allFoundProperties; } }
		public int Count { get { return _allProjects.Count; } }
		public bool Verbose { get; set; }
		public string PropertySheetPath
		{
			get { return propSheet; }
			set
			{
				propSheet = value;
				if (!File.Exists(propSheet))
				{
					var p = new Project();
					p.Save(propSheet);
				}
				_propertySheet = new MSBProject(propSheet, _globalProperties, toolsVersion);
			}
		}
		public MSBProject PropertySheet { get { return _propertySheet; } }
		public int CountFoundFiles { get; private set; }
		#endregion
		#endregion

		#region Constructors
		public PropertyExtractor(string config, string platform, bool verbose = false)
		{
			this.config = config;
			this.platform = platform;
			this.Verbose = verbose;
			_globalProperties.Add("Configuration", this.config);
			_globalProperties.Add("Platform", this.platform);
		}

		public PropertyExtractor(string inputDir, string config, string platform, bool verbose = false)
		{
			this.inputDir = inputDir;
			this.config = config;
			this.platform = platform;
			this.Verbose = verbose;

			_globalProperties.Add("Configuration", this.config);
			_globalProperties.Add("Platform", this.platform);

			Init();
		}

		public void SetInputDirectory(string inputDir)
		{
			this.inputDir = inputDir;
			Init();
		}

		private void Init()
		{
			_allProjects = GetProjects();
			GetAllConfigsAndPlatforms(_allProjects);
			GetAllReferenceProperties(_allProjects);
		}
		#endregion

		#region Public Methods
		/// <summary>
		/// Removes all values of a property from all projects 
		/// (for a given configuration/platform)
		/// </summary>
		/// <param name="name"></param>
		/// <param name="val"></param>
		public void Move(string name, string val)
		{
			Parallel.ForEach(_allProjects, proj =>
			{
				ProjectProperty p = proj.GetProperty(name);
				if (p != null &&
					!p.IsEnvironmentProperty &&
					!p.IsGlobalProperty &&
					!p.IsImported &&
					!p.IsReservedProperty)
				{
					proj.RemoveProperty(p);
				}
			});

			if (_allFoundProperties.ContainsKey(name))
			{
				ReferencedProperty refp = _allFoundProperties[name];
				refp.RemoveProjects(_allProjects);
				if (refp.UsedCount == 0)
				{
					bool removed = _allFoundProperties.Remove(name);
					Debug.Assert(removed, "Property was not removed from the list");
				}
			}

			UpdatePropertySheet(name, val);
		}

		public void Remove(List<string> propNames)
		{
			foreach (var name in propNames)
			{
				Remove(name);
			}
		}

		/// <summary>
		/// The bain of all old msbuild projects are empty property XML elements. 
		/// This function gets rid of them, in all build configs and in all the projects.
		/// Good riddance empty xml elements! We never knew ye!
		/// </summary>
		public void RemoveEmptyXMLElements()
		{
			Parallel.ForEach(_allProjects, project =>
			{
				ProjectRootElement root = project.Xml;

				RemoveEmptyProperties(root);
				RemoveEmptyElement<ProjectPropertyGroupElement>(root, root.PropertyGroups);
				RemoveEmptyElement<ProjectImportGroupElement>(root, root.ImportGroups);
				RemoveEmptyElement<ProjectItemDefinitionGroupElement>(root, root.ItemDefinitionGroups);
				RemoveEmptyElement<ProjectItemGroupElement>(root, root.ItemGroups);

				project.Save();
				project.ReevaluateIfNecessary();
			});
			GetAllReferenceProperties(_allProjects);
		}

		/// <summary>
		/// Removes properties from all configurations in all files.
		/// It actually ignores the global configuration and global platform
		/// and operates on just the raw XML elements.
		/// </summary>
		/// <param name="propNames"></param>
		public void RemoveXml(List<string> propNames)
		{
			foreach (var name in propNames)
			{
				foreach (var project in _allProjects)
				{
					ProjectRootElement root = project.Xml;
					RemoveEmptyProperties(root);
					RemoveEmptyElement<ProjectPropertyGroupElement>(root, root.PropertyGroups);
					project.Save();
				}

				if (_allFoundProperties.ContainsKey(name))
				{
					ReferencedProperty refp = _allFoundProperties[name];
					refp.RemoveProjects(_allProjects);
					if (refp.UsedCount == 0)
					{
						bool removed = _allFoundProperties.Remove(name);
						Debug.Assert(removed, "Property was not removed from the list");
					}
				}
			}
		}

		private static void RemoveEmptyElement<T>(ProjectRootElement root, ICollection<T> items)
			where T : ProjectElementContainer
		{
			var emptyGroups = new List<T>();
			foreach (var group in items)
			{
				if (group.Children.Count == 0)
				{
					emptyGroups.Add(group);
				}
			}

			foreach (var group in emptyGroups)
			{
				group.Parent.RemoveChild(group);
			}
		}

		private static void RemoveEmptyProperties(ProjectRootElement root)
		{
			var tobedeleted = new List<ProjectPropertyElement>();
			foreach (ProjectPropertyElement prop in root.Properties)
			{
				if (String.IsNullOrEmpty(prop.Value))
				{
					tobedeleted.Add(prop);
				}
			}
			foreach (var prop in tobedeleted)
			{
				prop.Parent.RemoveChild(prop);
			}
		}

		private void UpdatePropertySheet(string name, string val)
		{
			if (!String.IsNullOrEmpty(val))
			{
				ProjectProperty pr = _propertySheet.GetProperty(name);
				if (pr == null)
				{
					// Add the property
					_propertySheet.SetProperty(name, val);
				}
				else
				{
					pr.UnevaluatedValue = val;
				}
				_propertySheet.MarkDirty();
				_propertySheet.ReevaluateIfNecessary();
			}
		}

		/// <summary>
		/// Moves one particular value of a property to the property sheet
		/// By default it over writes the existing value in the property sheet
		/// </summary>
		/// <param name="val"></param>
		public void MoveValue(ReferencedValues val)
		{
			ReferencedProperty owner = val.Owner;
			owner.Remove(val);
			if (owner.UsedCount == 0)
			{
				_allFoundProperties.Remove(owner.Name);
			}
			UpdatePropertySheet(owner.Name, val.EvaluatedValue);
		}

		public void Remove(string name)
		{
			Move(name, null);
		}

		public void Remove(ReferencedValues val)
		{
			ReferencedProperty owner = val.Owner;
			owner.Remove(val);
			if (owner.UsedCount == 0)
			{
				_allFoundProperties.Remove(owner.Name);
			}
			// This is a delete, not a move. Do not update the property sheet here.
		}

		public void SetGlobalProperty(string name, string val)
		{
			_globalProperties[name] = val;
			Parallel.ForEach(_allProjects, proj =>
			{
				proj.SetGlobalProperty(name, val);
				proj.MarkDirty();
				proj.ReevaluateIfNecessary();
			});
			if (_propertySheet != null)
			{
				_propertySheet.SetGlobalProperty(name, val);
				_propertySheet.MarkDirty();
				_propertySheet.ReevaluateIfNecessary();
			}
			GetAllReferenceProperties(_allProjects);
		}

		public void PrintFoundProperties()
		{
			Utils.WL(ConsoleColor.DarkCyan, String.Format("Global Properties: Configuration => {0}", _globalProperties["Configuration"]));
			Utils.WL(ConsoleColor.DarkCyan, String.Format("Global Properties: Platform => {0}", _globalProperties["Platform"]));
			var sorted = from p in _allFoundProperties orderby p.Value.UsedCount descending select p;
			foreach (var pair in sorted)
			{
				ReferencedProperty prop = pair.Value;
				Utils.WL(ConsoleColor.Cyan, String.Format("{0} : {1}", prop.UsedCount, prop.Name));
				var sortedVals = from p in prop.PropertyValues orderby p.Value.Count descending select p;
				foreach (var vp in sortedVals)
				{
					ReferencedValues val = vp.Value;
					Utils.WL(ConsoleColor.DarkCyan, String.Format("\t{0} : {1}", val.Count, val.EvaluatedValue));
				}
			}
		}

		public List<MSBProject> GetProjects()
		{
			var csfileList = Directory.EnumerateFiles(inputDir, "*.csproj", SearchOption.AllDirectories).AsParallel();
			var vcfileList = Directory.EnumerateFiles(inputDir, "*.vcxproj", SearchOption.AllDirectories).AsParallel();
			var fileList = csfileList.Concat(vcfileList);
			CountFoundFiles = fileList.Count();
			ConcurrentBag<MSBProject> bag = new ConcurrentBag<MSBProject>();
			Parallel.ForEach(fileList, (file) =>
			{
				try
				{
					var p = new MSBProject(file, _globalProperties, toolsVersion);
					bag.Add(p);
				}
				catch (Exception e)
				{
					Utils.WL(ConsoleColor.Red, String.Format("Bad File: {0}", file));
					Utils.WL(ConsoleColor.DarkGray, e.Message);
				}
			});

			var sorted = from proj in bag
						 orderby proj.FullPath
						 select proj;

			return sorted.ToList();
		}

		public void SaveAll()
		{
			Parallel.ForEach(_allProjects, proj =>
			{
				try
				{
					proj.Save();
					AttachImportIfNecessary(proj);
					proj.ReevaluateIfNecessary();
				}
				catch (Exception e)
				{
					Utils.WL(ConsoleColor.Red, String.Format("Error saving file: {0}", proj.FullPath));
					Utils.WL(ConsoleColor.DarkGray, e.Message);
				}
			});
			if (_propertySheet != null)
			{
				_propertySheet.Save();
				_propertySheet.ReevaluateIfNecessary();
			}
		}
		#endregion

		#region Private Methods
		private void GetAllReferenceProperties(List<MSBProject> projects)
		{
			if (_allFoundProperties.Count() > 0)
			{
				var props = (ICollection<KeyValuePair<String, ReferencedProperty>>)_allFoundProperties;
				props.Clear();
			}
			//Parallel.ForEach(projects, proj => // Unstable. Throws exceptions from deep in the microsoft layer
			foreach (MSBProject proj in projects)
			{
				GetPropertiesFor(proj);
			}
			// This can only be called after the properties are crossed referenced.
			foreach (var pair in _allFoundProperties)
			{
				pair.Value.GetPropertyValues();
			}
		}

		private void GetPropertiesFor(MSBProject project)
		{
			foreach (ProjectProperty prop in project.AllEvaluatedProperties)
			{
				if (!prop.IsImported && !prop.IsEnvironmentProperty && !prop.IsReservedProperty)
				{
					string key = prop.Name;
					if (_allFoundProperties.ContainsKey(key))
					{
						_allFoundProperties[key].Add(project);
					}
					else
					{
						_allFoundProperties[key] = new ReferencedProperty(prop, project) { UsedCount = 1 };
					}
				}
			}
		}

		private void AttachImportIfNecessary(Project proj)
		{
			bool isCommonPropAttached = false;
			string name = Path.GetFileName(_propertySheet.FullPath);

			XDocument doc = XDocument.Load(proj.FullPath);
			XNamespace ns = doc.Root.Name.Namespace;
			var imports = doc.Descendants(ns + "Import");
			foreach(XElement import in imports)
			{
				XAttribute attr = import.Attribute("Project");
				if (attr != null)
				{
					string importedName = Path.GetFileName(attr.Value);
					if (String.Compare(importedName, name, StringComparison.OrdinalIgnoreCase) == 0)
					{
						isCommonPropAttached = true;
						break;
					}
				}
			}

			if (!isCommonPropAttached)
			{
				// Method one
				Uri uc = new Uri(_propertySheet.FullPath);
				Uri ui = new Uri(proj.FullPath);
				Uri dif = ui.MakeRelativeUri(uc);
				string relative = dif.OriginalString;
				
				XElement import = new XElement(ns + "Import", new XAttribute("Project", relative));
				IXmlLineInfo info = import as IXmlLineInfo;
				doc.Root.AddFirst(import);
				doc.Save(proj.FullPath);
				proj.MarkDirty();
				proj.ReevaluateIfNecessary();
			}
		}

		private void GetAllConfigsAndPlatforms(List<MSBProject> projects)
		{
			if (_allConfigurations.Count != 0)
				_allConfigurations.Clear();
			if (_allPlatforms.Count != 0)
				_allPlatforms.Clear();

			foreach (MSBProject project in projects)
			{
				IDictionary<string, List<string>> conProps = project.ConditionedProperties;
				List<String> configs = conProps["Configuration"];
				List<String> platforms = conProps["Platform"];
				foreach (var config in configs)
				{
					if (_allConfigurations.ContainsKey(config))
						_allConfigurations[config]++;
					else
						_allConfigurations.Add(config, 1);
				}
				foreach (var platform in platforms)
				{
					if (_allPlatforms.ContainsKey(platform))
						_allPlatforms[platform]++;
					else
						_allPlatforms.Add(platform, 1);
				}
			}
			PrintConfigsAndPlatforms();
		}

		[Conditional("DEBUG")]
		public void PrintConfigsAndPlatforms()
		{
			var sortedConfigs = from p in _allConfigurations orderby p.Value descending select p;
			_allConfigurations = sortedConfigs.ToDictionary(p => p.Key, p => p.Value);
			if (Verbose)
			{
				Utils.WL(ConsoleColor.DarkCyan, "+----- All Configurations -----+");
				foreach (var p in _allConfigurations)
					Utils.WL(ConsoleColor.Cyan, String.Format("{0,20} : {1}", p.Key, p.Value));
			}
			var sortedPlatforms = from p in _allPlatforms orderby p.Value descending select p;
			_allPlatforms = sortedPlatforms.ToDictionary(p => p.Key, p => p.Value);
			if (Verbose)
			{
				Utils.WL(ConsoleColor.DarkCyan, "+----- All Platforms -----+");
				foreach (var p in _allPlatforms)
					Utils.WL(ConsoleColor.Cyan, String.Format("{0,20} : {1}", p.Key, p.Value));
			}
		}
		#endregion
	}
}
