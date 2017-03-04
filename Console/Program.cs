using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Refactor;

namespace RefactorConsole
{
	class Program
	{
		static void Main(string[] args)
		{
			var timer = new Stopwatch();
			timer.Start();
			if (args.Length != 5)
			{
				Console.WriteLine("RefactorConsole <input_dir> <property_sheet> <config> <platform> <property>");
				return;
			}

			string inputDir = args[0];
			if (!Directory.Exists(inputDir))
			{
				Console.WriteLine("Input directory doesn't exist: {0}", inputDir);
				return;
			}
			string propSheet = args[1];
			if (!File.Exists(propSheet))
			{
				Console.WriteLine("Input property sheet file doesn't exist: {0}", propSheet);
				return;
			}
			string config   = args[2];
			string platform = args[3];
			string property = args[4];

			var refactor = new PropertyExtractor(inputDir, propSheet, config, platform);
			refactor.Verbose = true;
			timer.Stop();
			Console.WriteLine("{0} files", refactor.Count);
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Change configuration property");
			timer.Restart();
			refactor.SetGlobalProperty("Configuration", "Release");
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Moving a property");
			timer.Restart();
			refactor.Move("OutputPath", "$(BuildDir)");
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Change configuration property");
			timer.Restart();
			refactor.SetGlobalProperty("Configuration", "Debug");
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Moving a property");
			timer.Restart();
			refactor.Move("OutputPath", "$(BuildDir)");
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Removing a random property");
			timer.Restart();
			refactor.Remove("Optimize");
			refactor.Move("ProjectType", "local");
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Saving the files");
			timer.Restart();
			refactor.SaveAll();
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));

			Console.WriteLine("Print Found Properties");
			timer.Restart();
			refactor.PrintFoundProperties();
			timer.Stop();
			Utils.WL(ConsoleColor.DarkYellow, String.Format("Elapsed Time: {0}\n", timer.Elapsed));
		}
	}
}
