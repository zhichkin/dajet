using DaJet.Scripting.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Reflection;
using System.Text;

namespace DaJet.Scripting.Test
{
    [TestClass] public class Test_Parser
    {
        [TestMethod] public void Parse()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_Cte()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\cte"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_Destructive_Read()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\destructive-read"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_Expression()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\expression"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_OrderBy_Offset_Fetch()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\paging"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_GroupBy_Having()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\group-having"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_Window_Functions()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\over-window"))
            {
                ParseScriptFile(in filePath);
            }
        }
        [TestMethod] public void Parse_Case_When_Then_Else()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\case-when-then-else"))
            {
                ParseScriptFile(in filePath);
            }
        }
        private void ParseScriptFile(in string filePath)
        {
            Console.WriteLine("***");
            Console.WriteLine(filePath);

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                using (ScriptParser parser = new())
                {
                    if (!parser.TryParse(in script, out ScriptModel tree, out string error))
                    {
                        Console.WriteLine(error);
                        return;
                    }

                    foreach (SyntaxNode node in tree.Statements)
                    {
                        ShowSyntaxNode(node, 0);
                    }
                }
            }
        }
        private void ShowSyntaxNode(SyntaxNode node, int level)
        {
            if (node == null)
            {
                return;
            }

            string offset = "-".PadLeft(level * 2);

            Console.WriteLine($"{offset} {node}");

            foreach (PropertyInfo property in node.GetType().GetProperties())
            {
                if (property.Name == "Nodes")
                {
                    continue;
                }

                Type propertyType = property.PropertyType;

                bool isList = (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>));

                if (isList)
                {
                    propertyType = propertyType.GetGenericArguments()[0];
                }

                if (propertyType != typeof(SyntaxNode) && !propertyType.IsSubclassOf(typeof(SyntaxNode)))
                {
                    continue;
                }

                object value = property.GetValue(node);

                if (value == null)
                {
                    continue;
                }

                if (isList)
                {
                    IList list = (IList)value;
                    for (int i = 0; i < list.Count; i++)
                    {
                        ShowSyntaxNode(list[i] as SyntaxNode, level + 1);
                    }
                }
                else
                {
                    ShowSyntaxNode(value as SyntaxNode, level + 1);
                }
            }

            //foreach (SyntaxNode child in node.Nodes)
            //{
            //    ShowSyntaxNode(child, level + 1);
            //}
        }
        
        [TestMethod] public void Walker()
        {
            string filePath = "C:\\temp\\scripting-test\\script04.txt";

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                using (ScriptParser parser = new())
                {
                    if (!parser.TryParse(in script, out ScriptModel tree, out string error))
                    {
                        Console.WriteLine(error);
                        return;
                    }

                    ScopeBuilder builder = new();

                    if (!builder.TryBuild(in tree, out ScriptScope scope, out error))
                    {
                        Console.WriteLine(error);
                        return;
                    }

                    ShowScriptScope(scope, 0);
                }
            }
        }
        [TestMethod] public void Walker_Cte()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\cte"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        [TestMethod] public void Walker_Destructive_Read()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\destructive-read"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        [TestMethod] public void Walker_Expression()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\expression"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        [TestMethod] public void Walker_OrderBy_Offset_Fetch()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\paging"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        [TestMethod] public void Walker_GroupBy_Having()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\group-having"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        [TestMethod] public void Walker_Window_Functions()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\over-window"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        [TestMethod] public void Walker_Case_When_Then_Else()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\case-when-then-else"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                WalkScriptFile(in filePath);
            }
        }
        private void WalkScriptFile(in string filePath)
        {
            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                using (ScriptParser parser = new())
                {
                    if (!parser.TryParse(in script, out ScriptModel tree, out string error))
                    {
                        Console.WriteLine(error);
                        return;
                    }

                    ScopeBuilder builder = new();

                    if (!builder.TryBuild(in tree, out ScriptScope scope, out error))
                    {
                        Console.WriteLine(error);
                        return;
                    }

                    ShowScriptScope(scope, 0);
                }
            }
        }
        private void ShowScriptScope(ScriptScope scope, int level)
        {
            string offset = "-".PadLeft(level * 2);

            Console.WriteLine(offset + " [" + level.ToString() + "] " + scope.ToString());

            foreach (SyntaxNode item in scope.Identifiers)
            {
                if (item is SubqueryExpression query)
                {
                    Console.WriteLine(offset + " {" + query.Token + "}" + (string.IsNullOrWhiteSpace(query.Alias) ? string.Empty : " AS " + query.Alias));
                }
                else if (item is Identifier identifier)
                {
                    Console.WriteLine(offset + " {" + identifier.Token + "} " + identifier.Value
                        + (string.IsNullOrWhiteSpace(identifier.Alias) ? string.Empty : " AS " + identifier.Alias));
                }
                else if (item is DeclareStatement declare)
                {
                    string view = offset + " {" + declare.Token + "} " + declare.Name + " AS " + declare.Type;

                    if (declare.Initializer is ScalarExpression scalar)
                    {
                        view += " = " + scalar.Literal;
                    }

                    Console.WriteLine(view);
                }
            }

            foreach (ScriptScope child in scope.Children)
            {
                ShowScriptScope(child, level + 1);
            }
        }
    }
}