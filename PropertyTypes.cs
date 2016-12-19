using System.Collections.Generic;
using Microsoft.Build.Evaluation;

namespace msbuildrefactor
{
	/// <summary>
	/// Represents a property that has exists in a single common property sheet
	/// that many csproj or many msbuild files can reference. There is probably 
	/// a little too many fields on this class, but I just needed something quick
	/// to get this up and running.
	/// </summary>
	public class CommonProperty : BaseProperty
	{
		public CommonProperty(ProjectProperty property)
		{
			this.Name = property.Name;
			this.EvaluatedValue = property.EvaluatedValue;
			this.UnEvaluatedValue = property.UnevaluatedValue;
			if (property.Xml != null)
			{
				this.Condition = property.Xml.Condition;
			}
		}

		public string Name { get; set; }
		public string EvaluatedValue { get; set; }
		public string UnEvaluatedValue { get; set; }
		public string Condition { get; set; }
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
		public ReferencedProperty(ProjectProperty prop)
		{
			this.Name = prop.Name;
			_projects.Add(prop.Project);
			_originalProp = prop;
		}

		private ProjectProperty _originalProp;

		public ProjectProperty OriginalProperty { get { return _originalProp; } }
		public string Name { get; set; }

		/// <summary>
		/// How many times it's used in all files
		/// </summary>
		public int UsedCount { get; set; }

		private List<Project> _projects = new List<Project>();
		public Project[] Projects { get { return _projects.ToArray(); } }

		public void RemoveProjects(List<Project> projects)
		{
			foreach(var removed in projects)
			{
				_projects.Remove(removed);
				UsedCount--;
			}
			OnPropertyChanged("UsedCount");
		}

		public void Add(Project proj)
		{
			_projects.Add(proj);
			UsedCount++;
		}
	}

	/// <summary>
	/// Represents the possibly many different values that a ReferencedProperty can have
	/// as defined across many different project files. Hence it has a count to indicate 
	/// how many different values it has. It also has a field pointing back to the
	/// ReferencedProperty that 'owns' it.
	/// </summary>
	public class ReferencedValues : BaseProperty
	{
		public ReferencedValues() { }
		public string Value { get; set; }
		public int Count { get; set; }
		public ReferencedProperty Owner;
	}
}
