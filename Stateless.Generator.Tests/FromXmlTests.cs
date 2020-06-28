using GeneratedNamespace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Stateless;
using Stateless.CodeGenerator;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Xml;
using Xunit;

namespace GeneratedNamespace
{
    public interface IStateMachineFactory
    {
        public StateMachine<string, string> Configure();
    }
}
namespace Stateless.Generator.Tests
{
    
    public class FromXmlTests
    {
        [Fact]
        public void Generator_Throws_ArgumentException_Given_NullInput()
        {
            Assert.Throws<ArgumentException>(() => FromXml.Generate(null));
        }

        [Fact]
        public void Generator_Throws_ArgumentException_Given_EmptyString()
        {
            Assert.Throws<ArgumentException>(() => FromXml.Generate(""));
        }

        [Fact]
        public void Generator_Throws_XmlException_Given_MalformedXml()
        {
            Assert.Throws<XmlException>(() => FromXml.Generate("<>"));
        }

        [Fact]
        public void Generator_CreatesCodeWithCorrectInitialState_Given_XmlWithInitialStateElement()
        {
            var nameOfInitialState = "InitState";
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>     <StateMachine>  <InitialState>" + nameOfInitialState + @"</InitialState><States /><Transitions /></StateMachine>";
            var text = FromXml.Generate(xml);

            Assert.Contains(nameOfInitialState, text);
        }

        [Fact]
        public void Generator_CreatesCompileableCode_Given_XmlWithInitialStateElement()
        {
            var nameOfInitialState = "InitState";
            var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>     <StateMachine>  <InitialState>" + nameOfInitialState + @"</InitialState><States /><Transitions /></StateMachine>";
            var text = FromXml.Generate(xml);

            var stateMachineFactory = Compile(text) as IStateMachineFactory;

            Assert.NotNull(stateMachineFactory);

            var stateMachine = stateMachineFactory.Configure();

            Assert.NotNull(stateMachine);
            Assert.True(stateMachine.IsInState(nameOfInitialState));
        }

        [Fact]
        public void Generator_CreateCodeWithTwoStates_Given_ValidXmlWithTwoStates()
        {
            var nameOfInitialState = "State1";
            var xml =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<StateMachine>
  <StartState>" + nameOfInitialState + @"</InitialState>
  <States>
    <State Name=""State1"" /> 
    <State Name=""State2"" /> 
  </States >
  <Transitions />
</StateMachine>";

            var text = FromXml.Generate(xml);

            var stateMachineFactory = Compile(text) as IStateMachineFactory;

            Assert.NotNull(stateMachineFactory);

            var stateMachine = stateMachineFactory.Configure();


            var stateMachineInfo = stateMachine.GetInfo();
            Assert.Equal(2, stateMachineInfo.States.Count());
        }

        [Fact]
        public void Generator_CreateCodeWithTwoStatesAndTransitions_Given_ValidXmlWithTwoStatesAndTransitions()
        {
            var nameOfInitialState = "State1";
            var xml =
@"<?xml version=""1.0"" encoding=""utf-8""?>
<StateMachine>
  <StartState>" + nameOfInitialState + @"</InitialState>
  <States>
    <State Name=""State1"" /> 
    <State Name=""State2"" /> 
  </States >
  <Transitions>
    <Transition Name=""Transition1"" From=""State1"" To=""State2"" />
    <Transition Name = ""Transition2"" From = ""State2"" To = ""State1"" />
  </Transitions>
</StateMachine>";

            var text = FromXml.Generate(xml);

            var stateMachineFactory = Compile(text) as IStateMachineFactory;

            Assert.NotNull(stateMachineFactory);

            var stateMachine = stateMachineFactory.Configure();
            var stateMachineInfo = stateMachine.GetInfo();
            Assert.Equal(2, stateMachineInfo.States.Count());

            Assert.Single(stateMachineInfo.States.ElementAt(0).FixedTransitions);
            Assert.Single(stateMachineInfo.States.ElementAt(1).FixedTransitions);
        }

        private object Compile(string code)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var files = Directory.GetFiles(folder, "*.dll").ToList();
            files.Add(typeof(System.Object).GetTypeInfo().Assembly.Location);
            files.Add(typeof(Console).GetTypeInfo().Assembly.Location);
            files.Add(Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Core.dll"));
            files.Add(Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.dll"));
            files.Add(Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "System.Runtime.dll"));
            files.Add(Path.Combine(Path.GetDirectoryName(typeof(System.Runtime.GCSettings).GetTypeInfo().Assembly.Location), "mscorlib.dll"));
            files.Add(@"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\3.1.0\ref\netcoreapp3.1\netstandard.dll");

            //var assemblyPath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            //files.Add(new MetadataFileReference(Path.Combine(assemblyPath, "mscorlib.dll")));
            //files.Add(new MetadataFileReference(Path.Combine(assemblyPath, "System.dll")));
            //files.Add(new MetadataFileReference(Path.Combine(assemblyPath, "System.Core.dll")));
            //files.Add(new MetadataFileReference(Path.Combine(assemblyPath, "System.Runtime.dll")));

            var references = files.Select(r => MetadataReference.CreateFromFile(r)).ToArray();

            var syntaxTree = CSharpSyntaxTree.ParseText(code);
            CSharpCompilation compilation = CSharpCompilation.Create("test" + DateTime.Now.Ticks, new[] { syntaxTree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using var ms = new MemoryStream();

            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                Debug.WriteLine("Compilation failed!");
                IEnumerable<Diagnostic> failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (Diagnostic diagnostic in failures)
                {
                    Debug.WriteLine("\t{0}: {1}", diagnostic.Id, diagnostic.GetMessage());
                }

                return null;
            }

            Debug.WriteLine("Compilation successful! Now instantiating and executing the code ...");
            ms.Seek(0, SeekOrigin.Begin);

            Assembly assembly = AssemblyLoadContext.Default.LoadFromStream(ms);
            var type = assembly.GetType("GeneratedNamespace.StatemachineFactory");
            var instance = assembly.CreateInstance("GeneratedNamespace.StatemachineFactory");

            return instance;
        }


    }
}

