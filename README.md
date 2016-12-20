# MsbuildRefactor
A Tool to refactor many msbuild files at once

This helps to manage a property sheet for *.csproj files. Years ago, Visual Studio added property sheets for Visual C/C++ projects. 
There were tools to refactor build settings out to a common property sheet. The effort involves 
  1. Removing an msbuild property from a given project file. 
  2. Moving the property to a property sheet (commonly called *.props). 
  3. Importing the property sheet into the project file.

Visual studio provided tools to operate on one project at a time. _Which was not enough_. 
This can be especially tedious for hundreds of project files. Something even visual studio never even attempted to solve. 

This tool performs all of the above tasks on multiple files at once. It requires as input:
  1. An existing, common, msbuild property sheet file
  2. A directory containing the projects you want to modify.
  3. The global configuration property
  4. The global platform property
  5. An ignore pattern
  
