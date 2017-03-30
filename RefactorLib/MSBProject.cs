using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Build.Evaluation;

namespace Refactor
{
	/// <summary>
	/// An override of Project that allows specific customizatoins
	/// </summary>
	public class MSBProject : Project
	{
		private string extension;
		public MSBProject(string file, IDictionary<string, string> props, string toolsVersion)
			: base(file, props, toolsVersion)
		{
			Included = true;
			extension = Path.GetExtension(file);
		}

		/// <summary>
		/// Unused property currently.
		/// I might use it in the future to implement some sort of selection mechanism perhaps.
		/// </summary>
		public bool Included { get; set; }
		/// <summary>
		/// The file extension of the project.
		/// </summary>
		public string Extension { get { return extension; } }

		public string OutputPath
		{
			get
			{
				if (String.Compare(".csproj", extension, StringComparison.OrdinalIgnoreCase) == 0)
					return ResolveValue(this.GetProperty("OutputPath"));
				else if (String.Compare(".vcxproj", extension, StringComparison.OrdinalIgnoreCase) == 0)
					return ResolveValue(this.GetProperty("TargetPath"));
				else
					return "Unsupported project type";
			}
		}
		
		private string ResolveValue(ProjectProperty p)
		{
			var relative = p.EvaluatedValue;
			var basepath = Path.GetDirectoryName(this.FullPath);
			var combined = Path.GetFullPath(Path.Combine(basepath, relative));
			return combined;
		}
	}
}
