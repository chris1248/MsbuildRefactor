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

namespace Refactor
{
	public class PropertyExtractor
	{
		private string config;
		private string inputDir;
		private string platform;
		private string propSheet;
		private readonly string toolsVersion = "14.0";

		public PropertyExtractor(string inputDir, string propSheet, string config, string platform)
		{
			this.inputDir = inputDir;
			this.propSheet = propSheet;
			this.config = config;
			this.platform = platform;

			defaultProperties.Add("Configuration", "Debug");
			defaultProperties.Add("Platform", "AnyCPU");
		}

		public bool Verbose { get; set; }

		private CSProject _propertySheet;
		private List<CSProject> _AllProjects;
		private Dictionary<String, String> defaultProperties = new Dictionary<string, string>();
		private Dictionary<String, int> AllConfigurations = new Dictionary<String, int>();
		private Dictionary<String, int> AllPlatforms = new Dictionary<String, int>();

		public int Init()
		{
			_AllProjects = GetProjects();
			GetAllConfigsAndPlatforms(_AllProjects);
			GetAllReferenceProperties(_AllProjects);
			_propertySheet = new CSProject(propSheet, defaultProperties, toolsVersion);
			return _AllProjects.Count;
		}

		public void Move(string name, string val)
		{
			Parallel.ForEach(_AllProjects, proj =>
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
			
			if (_AllFoundProperties.ContainsKey(name))
			{
				ReferencedProperty refp = _AllFoundProperties[name];
				refp.RemoveProjects(_AllProjects);
				if (refp.UsedCount == 0)
				{
					bool removed = _AllFoundProperties.Remove(name);
					Debug.Assert(removed, "Property was not removed from the list");
				}
			}

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
			}
		}

		public void Remove(string name)
		{
			Move(name, null);
		}

		public void SetGlobalProperty(string name, string val)
		{
			defaultProperties[name] = val;
			Parallel.ForEach(_AllProjects, proj =>
			{
				proj.SetGlobalProperty(name, val);
				proj.ReevaluateIfNecessary();
			});
		}

		private void GetAllReferenceProperties(List<CSProject> projects)
		{
			//Parallel.ForEach(projects, proj => // Unstable. Throws exceptions from deep in the microsoft layer
			foreach(CSProject proj in projects)
			{
				GetPropertiesFrom(proj);
			}
			// This can only be called after the properties are crossed referenced.
			foreach(var pair in _AllFoundProperties)
			{
				pair.Value.GetPropertyValues();
			}
		}

		public ObservableConcurrentDictionary<String, ReferencedProperty> _AllFoundProperties = new ObservableConcurrentDictionary<string, ReferencedProperty>();
		public ObservableConcurrentDictionary<String, ReferencedProperty> AllFoundProperties
		{
			get { return _AllFoundProperties; }
		}

		public void PrintFoundProperties()
		{
			Utils.WL(ConsoleColor.DarkCyan, String.Format("Global Properties: Configuration => {0}", defaultProperties["Configuration"]));
			Utils.WL(ConsoleColor.DarkCyan, String.Format("Global Properties: Platform => {0}", defaultProperties["Platform"]));
			var sorted = from p in _AllFoundProperties orderby p.Value.UsedCount descending select p;
			foreach(var pair in sorted)
			{
				ReferencedProperty prop = pair.Value;
				Utils.WL(ConsoleColor.Cyan, String.Format("{0} : {1}", prop.UsedCount, prop.Name));
				var sortedVals = from p in prop.PropertyValues orderby p.Value.Count descending select p;
				foreach(var vp in sortedVals)
				{
					ReferencedValues val = vp.Value;
					Utils.WL(ConsoleColor.DarkCyan, String.Format("\t{0} : {1}", val.Count, val.EvaluatedValue));
				}
			}
		}

		private void GetPropertiesFrom(CSProject project)
		{
			foreach (ProjectProperty prop in project.AllEvaluatedProperties)
			{
				if (!prop.IsImported && !prop.IsEnvironmentProperty && !prop.IsReservedProperty)
				{
					string key = prop.Name;
					if (_AllFoundProperties.ContainsKey(key))
					{
						_AllFoundProperties[key].Add(project);
					}
					else
					{
						_AllFoundProperties[key] = new ReferencedProperty(prop, project) { UsedCount = 1 };
					}
				}
			}
		}

		private void GetAllConfigsAndPlatforms(List<CSProject> projects)
		{
			foreach(CSProject project in projects)
			{
				IDictionary<string, List<string>> conProps = project.ConditionedProperties;
				List<String> configs = conProps["Configuration"];
				List<String> platforms = conProps["Platform"];
				foreach (var config in configs)
				{
					if (AllConfigurations.ContainsKey(config))
						AllConfigurations[config]++;
					else
						AllConfigurations.Add(config, 1);
				}
				foreach (var platform in platforms)
				{
					if (AllPlatforms.ContainsKey(platform))
						AllPlatforms[platform]++;
					else
						AllPlatforms.Add(platform, 1);
				}
			}
			var sortedConfigs = from p in AllConfigurations orderby p.Value descending select p;
			AllConfigurations = sortedConfigs.ToDictionary(p => p.Key, p => p.Value);
			if (Verbose)
			{
				Utils.WL(ConsoleColor.DarkCyan, "+----- All Configurations -----+");
				foreach (var p in AllConfigurations)
					Utils.WL(ConsoleColor.Cyan, String.Format("{0,20} : {1}", p.Key, p.Value));
			}
			var sortedPlatforms = from p in AllPlatforms orderby p.Value descending select p;
			AllPlatforms = sortedPlatforms.ToDictionary(p => p.Key, p => p.Value);
			if (Verbose)
			{
				Utils.WL(ConsoleColor.DarkCyan, "+----- All Platforms -----+");
				foreach (var p in AllPlatforms)
					Utils.WL(ConsoleColor.Cyan, String.Format("{0,20} : {1}", p.Key, p.Value));
			}
		}

		public List<CSProject> GetProjects()
		{
			var fileList = Directory.EnumerateFiles(inputDir, "*.csproj", SearchOption.AllDirectories).AsParallel();

			ConcurrentBag<CSProject> bag = new ConcurrentBag<CSProject>();
			Parallel.ForEach(fileList, (file) =>
			{
				try
				{
					var p = new CSProject(file, defaultProperties, toolsVersion);
					bag.Add(p);
				}
				catch (Exception e)
				{
					Utils.WL(ConsoleColor.Red, String.Format("Bad File: {0}", file));
					Utils.WL(ConsoleColor.DarkGray, e.Message);
				}
			});

			return bag.ToList();
		}

		private void AttachImportIfNecessary(Project proj)
		{
			bool isCommonPropAttached = false;
			string name = Path.GetFileName(_propertySheet.FullPath).ToLower();
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
				Uri uc = new Uri(_propertySheet.FullPath);
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

		public void SaveAll()
		{
			Parallel.ForEach(_AllProjects, proj =>
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
			_propertySheet.Save();
			_propertySheet.ReevaluateIfNecessary();
		}
	}
}
