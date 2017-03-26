using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using System.Collections.Concurrent;
using System;
using System.Windows.Data;
using System.Globalization;
using System.Diagnostics;

namespace Refactor
{
	/// <summary>
	/// Represents a property that has exists in a single common property sheet
	/// that many csproj or many msbuild files can reference.
	/// </summary>
	public class CommonProperty : BaseProperty
	{
		public CommonProperty(string name, string evaluated_value)
		{
			_name = name;
			_value = evaluated_value;
		}

		private string _name;
		private string _value;
		public string Name {
			get { return _name; }
			set
			{
				_name = value; 
				OnPropertyChanged("Name");
			}
		}

		public string EvaluatedValue
		{
			get { return _value; }
			set
			{
				_value = value;
				OnPropertyChanged("EvaluatedValue");
			}
		}
		public override string ToString()
		{
			return string.Format("Property {0} = {1}", Name, EvaluatedValue);
		}
	}

	/// <summary>
	/// Represents a property that is duplicated many times across many project files
	/// It holds references to all the project files that define this property
	/// </summary>
	public class ReferencedProperty : BaseProperty
	{
		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="prop">A property instance from the project</param>
		public ReferencedProperty(ProjectProperty prop, MSBProject proj)
		{
			this.Name = prop.Name;
			_projects.Add(proj);
		}

		/// <summary>
		/// The name of the property
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// How many times it's used in all project files
		/// </summary>
		public int UsedCount { get; set; }

		public override string ToString()
		{
			return string.Format("ReferenceProperty {0}, usedby: {1} projects", Name, UsedCount);
		}
		private List<MSBProject> _projects = new List<MSBProject>();
		/// <summary>
		/// The array of Projects that use this property
		/// </summary>
		public MSBProject[] Projects { get { return _projects.ToArray(); } }

		/// <summary>
		/// Removes a whole bunch of projects from the list at once
		/// Updates any listeners at the end.
		/// </summary>
		/// <param name="projects"></param>
		public void RemoveProjects(List<MSBProject> projects)
		{
			foreach (var removed in projects)
			{
				_projects.Remove(removed);
				if (UsedCount > 0)
					UsedCount--;
			}
			OnPropertyChanged("UsedCount");
		}

		public void Add(MSBProject proj)
		{
			_projects.Add(proj);
			UsedCount++;
		}

		public void Remove(ReferencedValues val)
		{
			foreach (MSBProject proj in val.Projects)
			{
				ProjectProperty p = proj.GetProperty(this.Name);
				if (p != null && (!p.IsImported))
				{
					bool removed = proj.RemoveProperty(p);
					Debug.Assert(removed);
					proj.MarkDirty();
				}
			}

			RemoveProjects(val.Projects);
			string key = val.EvaluatedValue;
			if (_PropertyValues.ContainsKey(key))
			{
				_PropertyValues.Remove(key);
			}
			OnPropertyChanged("PropertyValues");
		}

		private ObservableConcurrentDictionary<String, ReferencedValues> _PropertyValues = new ObservableConcurrentDictionary<string, ReferencedValues>();
		public ObservableConcurrentDictionary<String, ReferencedValues> PropertyValues
		{
			get { return _PropertyValues; }
		}

		public void GetPropertyValues()
		{
			foreach (MSBProject project in this.Projects)
			{
				ProjectProperty itemprop = project.GetProperty(this.Name);
				if (itemprop != null)
				{
					string key = itemprop.EvaluatedValue.ToLower();
					if (this.Name == "OutputPath")
					{
						key = project.OutputPath;
					}

					if (_PropertyValues.ContainsKey(key))
					{
						_PropertyValues[key].AddProject(project);
					}
					else
					{
						_PropertyValues[key] = new ReferencedValues(project) { EvaluatedValue = key, Owner = this };
					}
				}
			}
		}
	}

	/// <summary>
	/// Used as a markup extension to convert from
	/// ObservableConcurrentDictionary<String, ReferencedValues>
	/// to something that can be bound to a listbox. In this case a 
	/// KeyValuePair<String, ReferencedValues>
	/// which is used in the details view ListView.
	/// </summary>
	public class KeyPairToRefPropConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool valid = value is KeyValuePair<String, ReferencedProperty>;
			if (valid)
			{
				var pair = (KeyValuePair<String, ReferencedProperty>)value;
				return pair.Value.PropertyValues;
			}
			return null;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			bool valid = value is ObservableConcurrentDictionary<String, ReferencedValues>;
			if (valid)
			{
				var dict = (ObservableConcurrentDictionary<String, ReferencedValues>)value;
				ReferencedValues refVal;
				foreach(var pair in dict)
				{
					refVal = pair.Value;
					ReferencedProperty prop = refVal.Owner;
					return new KeyValuePair<String, ReferencedProperty>(prop.Name, prop);
				}
				
			}
			return null;
		}
	}
	/// <summary>
	/// Represents a value that a ReferencedProperty can have
	/// as defined across many different project files. So a ReferenceProperty can hold many 
	/// of these instances. 
	/// This also has a count to indicate how many of the values it has. 
	/// It also has a field pointing back to the ReferencedProperty that 'owns' it.
	/// </summary>
	public class ReferencedValues : BaseProperty
	{
		public ReferencedValues(MSBProject proj)
		{
			_projects.Add(proj);
			Count++;
		}

		private List<MSBProject> _projects = new List<MSBProject>();
		public List<MSBProject> Projects { get { return _projects; } }

		public void AddProject(MSBProject p)
		{
			Count++;
			_projects.Add(p);
		}
		/// <summary>
		/// The evaluated value of the property
		/// </summary>
		public string EvaluatedValue { get; set; }
		
		/// <summary>
		/// The number of occurances of the Value
		/// </summary>
		public int Count { get; private set; }
		public ReferencedProperty Owner;

		public override string ToString()
		{
			return string.Format("ReferencedValues {0}, Count: {1} for {2}", EvaluatedValue, Count, Owner.Name);
		}
	}
}
