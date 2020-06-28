using System;
using System.Text;
using System.Xml;

namespace Stateless.CodeGenerator
{

    public class FromXml
    {
        // Creates code to create a new state machine, based on the provided xml.
        public static string Generate(string xml)
        {
            if (string.IsNullOrEmpty(xml))
                throw new ArgumentException("The provided xml is null, or empty", "xml");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);
            var root = doc.DocumentElement;

            var initialStateNode = root.SelectSingleNode("//InitialState").FirstChild;
            var stateNodes = root.SelectSingleNode("//States");
            var transitionNodes = root.SelectSingleNode("//Transitions");

            if (initialStateNode == null) throw new ArgumentException("The provided xmldoes not have the required InitialState element", "xml");
            if (stateNodes == null) throw new ArgumentException("The provided xmldoes not have the required States element", "xml");
            if (transitionNodes == null) throw new ArgumentException("The provided xmldoes not have the required Transitions element", "xml");

            StringBuilder code = new StringBuilder(10000);
            var beginning = header.Replace("<initialState>", $"\"{initialStateNode.Value}\"" );
            code.AppendLine(beginning);
            // Add states
            foreach(XmlNode state in stateNodes.ChildNodes)
            {
                var stateName = state.Attributes["Name"].Value;
                code.AppendLine($"\t\t\tsm.Configure(\"{stateName}\")");
                // Add transitions
                foreach (XmlNode transition in root.SelectNodes($"//Transition[@From='{stateName}']"))
                {
                    var transitionName = transition.Attributes["Name"].Value;
                    var target = transition.Attributes["To"].Value;
                    code.AppendLine($"\t\t\t.Permit(\"{transitionName}\", \"{target}\")");
                }
                code.AppendLine(";");
            }

            code.Append(ending);
            return code.ToString();
        }


        private static readonly string header = @"
using Stateless;
namespace GeneratedNamespace
{
    public class StatemachineFactory : IStateMachineFactory
    {
        public StateMachine<string, string> Configure()
        {
            var sm = new StateMachine<string, string>(<initialState>);";


        private static readonly string ending = @"
            return sm;
        }
    }
}";
    }
}
