using System.Reflection;

[assembly: AssemblyProduct("BSS Platform Services")]
[assembly: AssemblyCompany("Luxoft")]
[assembly: AssemblyCopyright("Copyright © Luxoft 2024")]

[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: AssemblyInformationalVersion("1.0.1.0")]

#if DEBUG
[assembly: AssemblyConfiguration("Debug")]
#else
[assembly: AssemblyConfiguration("Release")]
#endif
