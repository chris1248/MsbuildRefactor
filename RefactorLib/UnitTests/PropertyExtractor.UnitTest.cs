using System;
using System.ComponentModel;
using System.Xml.Linq;
using System.Linq;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Refactor;

namespace UnitTests
{
	[TestClass]
	public class ExtractorTests
	{
		private PropertyExtractor extractor;
		/// <summary>
		/// Called before each test function
		/// </summary>
		[TestInitialize]
		public void Setup()
		{
			bool verbose = true;
			extractor = new PropertyExtractor("TestData", "Debug", "AnyCPU", verbose);
		}

		[TestCleanup]
		public void Teardown()
		{
			ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
		}

		[TestMethod]
		public void TestExtractorInit()
		{
			Assert.AreEqual(3, extractor.Count);
			Assert.AreEqual(3, extractor.CountFoundFiles);
		}

		[TestMethod]
		public void TestFoundFileNames()
		{
			Assert.AreEqual(3, extractor.AllProjects.Count);
			var query = (from p in extractor.AllProjects
						 let name = Path.GetFileName(p.FullPath)
						 orderby name ascending 
						 select name).ToList();
			Assert.AreEqual("A.csproj", query[0]);
			Assert.AreEqual("B.csproj", query[1]);
			Assert.AreEqual("C.csproj", query[2]);
		}

		[TestMethod]
		public void TestConfigurations()
		{
			Assert.AreEqual(3, extractor.AllConfigurations.Count);
			var configs = (from p in extractor.AllConfigurations
						  let config = p.Key
						  orderby config ascending
						  select config).ToList();
			Assert.AreEqual("Debug", configs[0]);
			Assert.AreEqual("Release", configs[1]);
			Assert.AreEqual("Scooby", configs[2]);
		}
		[TestMethod]
		public void TestPlatforms()
		{
			Assert.AreEqual(2, extractor.AllPlatforms.Count);
			var platforms = (from p in extractor.AllPlatforms
						   let pl = p.Key
						   orderby pl ascending
						   select pl).ToList();
			Assert.AreEqual("AnyCPU", platforms[0]);
			Assert.AreEqual("x86", platforms[1]);
		}

		[TestMethod]
		public void TestAllFoundProperties()
		{
			Assert.AreEqual(6, extractor.AllFoundProperties.Count());
			var allprops = (from p in extractor.AllFoundProperties
							 let pl = p.Key
							 orderby pl ascending
							 select pl).ToList();
			Assert.AreEqual("AssemblyName"  , allprops[0]);
			Assert.AreEqual("Configuration" , allprops[1]);
			Assert.AreEqual("OutputPath"    , allprops[2]);
			Assert.AreEqual("Platform"      , allprops[3]);
			Assert.AreEqual("TheType"       , allprops[4]);
			Assert.AreEqual("UniqueforDebug", allprops[5]);
			
			// Assert counts too
			var props = extractor.AllFoundProperties;
			Assert.AreEqual(3, props["AssemblyName"].UsedCount);
			Assert.AreEqual(3, props["Configuration"].UsedCount);
			Assert.AreEqual(3, props["OutputPath"].UsedCount);
			Assert.AreEqual(3, props["Platform"].UsedCount);
			Assert.AreEqual(3, props["TheType"].UsedCount);
			Assert.AreEqual(1, props["UniqueforDebug"].UsedCount);

			var configVals = props["Configuration"].PropertyValues;
			Assert.AreEqual(1, configVals.Keys.Count);
			Assert.IsTrue(configVals.ContainsKey("debug"));
			Assert.AreEqual(3, configVals["debug"].Count);

			var typeVals = props["TheType"].PropertyValues;
			Assert.AreEqual(1, typeVals.Keys.Count);
			Assert.IsTrue(typeVals.ContainsKey("debug"));
			Assert.AreEqual(3, typeVals["debug"].Count);
		}

		[TestMethod]
		public void TestPropertySheet()
		{
			extractor.PropertySheetPath = "TestData\\test.props";
			MSBProject file = extractor.PropertySheet;
			Assert.AreEqual("TestData\\test.props", extractor.PropertySheetPath);
			Assert.IsNotNull(file);
			Assert.AreEqual("test.props", Path.GetFileName(file.FullPath));
		}

		[TestMethod]
		public void TestVerbose()
		{
			extractor.Verbose = true;
			Assert.IsTrue(extractor.Verbose);
		}

		[TestMethod]
		public void TestSetGlobalProperties()
		{
			extractor.SetGlobalProperty("Configuration", "Release");

			Assert.AreEqual(6, extractor.AllFoundProperties.Count());
			var allprops = (from p in extractor.AllFoundProperties
							let pl = p.Key
							orderby pl ascending
							select pl).ToList();
			Assert.AreEqual("AssemblyName", allprops[0]);
			Assert.AreEqual("Configuration", allprops[1]);
			Assert.AreEqual("OutputPath", allprops[2]);
			Assert.AreEqual("Platform", allprops[3]);
			Assert.AreEqual("TheType", allprops[4]);
			Assert.AreEqual("UniqueforRelease", allprops[5]);

			// Assert counts too
			var props = extractor.AllFoundProperties;
			Assert.AreEqual(3, props["AssemblyName"].UsedCount);
			Assert.AreEqual(3, props["Configuration"].UsedCount);
			Assert.AreEqual(3, props["OutputPath"].UsedCount);
			Assert.AreEqual(3, props["Platform"].UsedCount);
			Assert.AreEqual(3, props["TheType"].UsedCount);
			Assert.AreEqual(1, props["UniqueforRelease"].UsedCount);

			Assert.AreEqual("Configuration", props["Configuration"].Name);
			Assert.AreEqual("OutputPath", props["OutputPath"].Name);
			Assert.AreEqual("Platform", props["Platform"].Name);
			Assert.AreEqual("TheType", props["TheType"].Name);
			Assert.AreEqual("UniqueforRelease", props["UniqueforRelease"].Name);

			var configVals = props["Configuration"].PropertyValues;
			Assert.AreEqual(1, configVals.Keys.Count);
			Assert.IsTrue(configVals.ContainsKey("release"));
			Assert.AreEqual(3, configVals["release"].Count);

			var typeVals = props["TheType"].PropertyValues;
			Assert.AreEqual(1, typeVals.Keys.Count);
			Assert.IsTrue(typeVals.ContainsKey("release"));
			Assert.AreEqual(3, typeVals["release"].Count);

			var assemblyName = props["AssemblyName"].PropertyValues;
			Assert.AreEqual(3, assemblyName.Keys.Count);
			Assert.IsTrue(assemblyName.ContainsKey("a"));
			Assert.IsTrue(assemblyName.ContainsKey("b"));
			Assert.IsTrue(assemblyName.ContainsKey("c"));
			Assert.AreEqual(1, assemblyName["a"].Count);
			Assert.AreEqual(1, assemblyName["b"].Count);
			Assert.AreEqual(1, assemblyName["c"].Count);
		}

		[TestMethod]
		public void TestPrintFoundProperties()
		{
			extractor.PrintFoundProperties();
		}

		[TestMethod]
		public void TestReferencedPropToString()
		{
			ReferencedProperty p = extractor.AllFoundProperties["Configuration"];
			Assert.AreEqual("ReferenceProperty Configuration, usedby: 3 projects", p.ToString());

			var propVals = p.PropertyValues;
			Assert.AreEqual("ReferencedValues debug, Count: 3 for Configuration", propVals["debug"].ToString());
		}

		[TestMethod]
		public void TestRemoveOneProject()
		{
			// Remove one property that exists in one project only
			extractor.Remove("UniqueforDebug");
			Assert.AreEqual(5, extractor.AllFoundProperties.Count());
			var allprops = (from p in extractor.AllFoundProperties
							let pl = p.Key
							orderby pl ascending
							select pl).ToList();
			Assert.AreEqual("AssemblyName", allprops[0]);
			Assert.AreEqual("Configuration", allprops[1]);
			Assert.AreEqual("OutputPath", allprops[2]);
			Assert.AreEqual("Platform", allprops[3]);
			Assert.AreEqual("TheType", allprops[4]);
		}

		[TestMethod]
		public void TestRemoveAllProjects()
		{
			// Remove a property that exists in all projects
			extractor.Remove("TheType");
			Assert.AreEqual(5, extractor.AllFoundProperties.Count());
			var allprops = (from p in extractor.AllFoundProperties
							let pl = p.Key
							orderby pl ascending
							select pl).ToList();
			Assert.AreEqual("AssemblyName", allprops[0]);
			Assert.AreEqual("Configuration", allprops[1]);
			Assert.AreEqual("OutputPath", allprops[2]);
			Assert.AreEqual("Platform", allprops[3]);
			Assert.AreEqual("UniqueforDebug", allprops[4]);
		}

		[TestMethod]
		public void TestMoveProperty()
		{
			extractor.PropertySheetPath = "TestData\\test.props";

			int prev = extractor.PropertySheet.Properties.Count;
			extractor.Move("TheType", "foobar");
			int after = extractor.PropertySheet.Properties.Count;
			Assert.AreEqual(prev + 1, after);

			var prop = extractor.PropertySheet.GetProperty("TheType");
			Assert.AreEqual("foobar", prop.EvaluatedValue);

			Assert.AreEqual(5, extractor.AllFoundProperties.Count());
			var allprops = (from p in extractor.AllFoundProperties
							let pl = p.Key
							orderby pl ascending
							select pl).ToList();
			Assert.AreEqual("AssemblyName", allprops[0]);
			Assert.AreEqual("Configuration", allprops[1]);
			Assert.AreEqual("OutputPath", allprops[2]);
			Assert.AreEqual("Platform", allprops[3]);
			Assert.AreEqual("UniqueforDebug", allprops[4]);

			extractor.Move("OutputPath", @"C:\builds\debug\");
			prop = extractor.PropertySheet.GetProperty("OutputPath");
			Assert.AreEqual(@"C:\builds\debug\", prop.EvaluatedValue);

			Assert.AreEqual(4, extractor.AllFoundProperties.Count());
			allprops = (from p in extractor.AllFoundProperties
							let pl = p.Key
							orderby pl ascending
							select pl).ToList();
			Assert.AreEqual("AssemblyName", allprops[0]);
			Assert.AreEqual("Configuration", allprops[1]);
			Assert.AreEqual("Platform", allprops[2]);
			Assert.AreEqual("UniqueforDebug", allprops[3]);
		}

		[TestMethod]
		public void TestMoveValue()
		{
			extractor.PropertySheetPath = "TestData\\test.props";
			ReferencedProperty refProp = extractor.AllFoundProperties["AssemblyName"];
			var vals = refProp.PropertyValues;
			ReferencedValues val = vals["a"];
			int before = refProp.UsedCount;
			extractor.MoveValue(val);
			int after = refProp.UsedCount;
			Assert.AreEqual(before - 1, after);
			Assert.AreEqual(2, refProp.Projects.Count());
			var projs = (from p in refProp.Projects
						 let name = Path.GetFileName(p.FullPath)
						 orderby name ascending
						 select name).ToList();
			Assert.AreEqual("B.csproj", projs[0]);
			Assert.AreEqual("C.csproj", projs[1]);
			Assert.AreEqual(2, refProp.PropertyValues.Count());
		}

		[TestMethod]
		public void TestSaveFiles()
		{
			// Make backups
			File.Copy(@"TestData\test.props", @"TestData\test_backup.props", true);
			File.Copy(@"TestData\A.csproj", @"TestData\A_backup.csproj", true);
			File.Copy(@"TestData\B.csproj", @"TestData\B_backup.csproj", true);
			File.Copy(@"TestData\C.csproj", @"TestData\C_backup.csproj", true);

			extractor.PropertySheetPath = @"TestData\test.props";
			extractor.Move("TheType", "foobar");
			extractor.SaveAll();

			// DO it a second time to make sure that the import doesn't get attached twice
			extractor.SaveAll();

			// Restore the files
			File.Delete(@"TestData\A.csproj");
			File.Delete(@"TestData\B.csproj");
			File.Delete(@"TestData\C.csproj");
			File.Delete(@"TestData\test.props");
			File.Move(@"TestData\A_backup.csproj", @"TestData\A.csproj");
			File.Move(@"TestData\B_backup.csproj", @"TestData\B.csproj");
			File.Move(@"TestData\C_backup.csproj", @"TestData\C.csproj");
			File.Move(@"TestData\test_backup.props", @"TestData\test.props");
		}

		[TestMethod]
		public void TestCommonProperty()
		{
			var prop = new CommonProperty("platform", "anycpu");
			Assert.AreEqual("platform", prop.Name);
			Assert.AreEqual("anycpu", prop.EvaluatedValue);
			Assert.AreEqual("Property platform = anycpu", prop.ToString());

			prop.PropertyChanged += PropOnPropertyChanged;
			prop.Name = "Foobar";
			prop.EvaluatedValue = "eggs";
		}

		private void PropOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
		{
			// NoOp
		}
	}
}
