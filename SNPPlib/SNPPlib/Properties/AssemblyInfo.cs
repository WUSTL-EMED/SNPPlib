﻿using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("SNPPlib")]
[assembly: AssemblyDescription("A .NET library for handling SNPP client and server functionality.")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("SNPPlib")]
[assembly: AssemblyCopyright("Copyright © 2016 Aaron Papp")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("16ef0a23-9a90-4e06-b9ae-3aa8a5e862c0")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
[assembly: AssemblyVersion("1.0.*")]
// [assembly: AssemblyVersion("1.0.0.0")]
// [assembly: AssemblyFileVersion("1.0.0.0")]

// Allow the unit test project to see internals.
[assembly: InternalsVisibleTo("SNPPlib.Tests")]