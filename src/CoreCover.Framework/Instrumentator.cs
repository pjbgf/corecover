using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using CoreCover.Instrumentation;
using Mono.Cecil;
using OpenCover.Framework.Model;
using File = System.IO.File;

namespace CoreCover.Framework
{
    public class Instrumentator : IInstrumentator
    {
        private readonly bool _useShadowFile = false;
        private readonly IAssemblyInstrumentationHandler _assemblyInstrumentationHandler;

        public Instrumentator(IAssemblyInstrumentationHandler assemblyInstrumentationHandler)
        {
            _assemblyInstrumentationHandler = assemblyInstrumentationHandler;
        }

        private const string InstrumentationAssemblyName = "CoreCover.Instrumentation.dll";

        public void Process(CoverageSession coverageSession, string folderPath)
        {
            CopyDependenciesTo(folderPath);
            ProcessAssemblies(coverageSession, Directory.GetFiles(folderPath, "*.dll"));
            ReportTracker.WriteReport();
        }

        private void CopyDependenciesTo(string targetPath)
        {
            var directoryPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            CopyDependencyTo(Path.Combine(directoryPath, InstrumentationAssemblyName), targetPath);
        }

        private static void CopyDependencyTo(string dependencyFilePath, string targetDirectory)
        {
            var targetFilePath = Path.Combine(targetDirectory, Path.GetFileName(dependencyFilePath));

            if (!File.Exists(targetFilePath))
                File.Copy(dependencyFilePath, targetFilePath);
        }

        public void ProcessAssemblies(CoverageSession coverageSession, string[] assemblyPaths)
        {
            foreach (var assemblyPath in assemblyPaths)
            {
                var pdbFile = Path.ChangeExtension(assemblyPath, "pdb");
                if (!File.Exists(pdbFile))
                    continue;

                var assemblyPathLocal = assemblyPath;
                if (_useShadowFile)
                {
                    assemblyPathLocal = Path.ChangeExtension(assemblyPath, "orig.dll");
                    RenameOriginalAssembly(assemblyPath, assemblyPathLocal);
                }

                using (var assembly = LoadAssembly(assemblyPathLocal))
                {
                    _assemblyInstrumentationHandler.Handle(coverageSession, assembly);
                    if (_useShadowFile)
                        assembly.Write(assemblyPath, new WriterParameters { WriteSymbols = true });
                    else
                        assembly.Write(new WriterParameters { WriteSymbols = true });
                }

                if (_useShadowFile)
                    CleanTempFiles(Path.GetDirectoryName(assemblyPathLocal));
            }
        }

        private void CleanTempFiles(string folder)
        {
            foreach (var file in Directory.EnumerateFiles(folder, "*.orig.*"))
            {
                if (Regex.IsMatch(Path.GetExtension(file), "^\\.(pdb|dll)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled))
                    File.Delete(file);
            }
        }

        private void RenameOriginalAssembly(string assemblyPath, string newAssemblyPath)
        {
            var shadowPdbFilePath = Path.ChangeExtension(newAssemblyPath, "pdb");
            var originalPdbFilePath = Path.ChangeExtension(assemblyPath, "pdb");

            File.Move(assemblyPath, newAssemblyPath);
            File.Copy(originalPdbFilePath, shadowPdbFilePath);
        }

        private AssemblyDefinition LoadAssembly(string assemblyPath)
        {
            var readerParameters = new ReaderParameters { ReadSymbols = true, ReadWrite = !_useShadowFile };
            var assembly = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);

            return assembly;
        }
    }
}