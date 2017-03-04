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
	public class CSProject : Project
	{
		public CSProject()
			: base()
		{
		}

		public CSProject(string file, IDictionary<string, string> props, string toolsVersion)
			: base(file, props, toolsVersion)
		{
			Included = true;
		}

		/// <summary>
		///  Use this file in any sort of calculation that we so desire
		/// </summary>
		public bool Included { get; set; }

		private string _outputPath;
		public string OutputPath
		{
			get
			{
				if (String.IsNullOrEmpty(_outputPath))
				{
					var relative = this.GetProperty("OutputPath").EvaluatedValue;
					var basepath = Path.GetDirectoryName(this.FullPath);
					var combined = Path.GetFullPath(Path.Combine(basepath, relative));
					_outputPath = combined.ToLower();
				}
				return _outputPath;
			}
		}
		
		public List<String> AllConfigs
		{
			get
			{
				IDictionary<string, List<string>> props = this.ConditionedProperties;
				return props["Configuration"];
			}
		}
		public List<String> AllPlatforms
		{
			get
			{
				IDictionary<string, List<string>> props = this.ConditionedProperties;
				return props["Platform"];
			}
		}
	}
}
