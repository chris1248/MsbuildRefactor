# MsbuildRefactor
A Tool to refactor many msbuild files at once

Refactoring an existing code base of hundreds of projects can be a made easier using this tool. Using MSBuild .NET API's it loads msbuild project files (i.e. *.csproj) displaying their build properties. A common msbuild file used to store all global properties (Which I call a property sheet) can also be loaded. Then it is up to the user to choose which build properties to move to the property sheet.
Moving properties is simply done with a drag *drag and drop*. 
The tool then takes care of 
  1. removing the property from all the project files
  2. adding the property to the common property sheet
  3. Adding an import statement to all the projects

If this operation was done manually, it would be especially tedious, and fragile since usual text search/replace operations do not respect different build configurations manifest in the msbuild project files. This tool does respect build configurations thus limiting operations to specific configurations and platforms that you choose.

