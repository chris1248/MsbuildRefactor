using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Build.Evaluation;

namespace msbuildrefactor
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
	}
}
