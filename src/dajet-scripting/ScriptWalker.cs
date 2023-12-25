using DaJet.Scripting.Model;
using System.Collections;
using System.Reflection;
using System.Text.Json.Serialization;

namespace DaJet.Scripting
{
    //TODO: make SayHello and SayGoodbye cancelable
    public interface IScriptWalker
    {
        void SayHello(in SyntaxNode node);
        void SayGoodbye(in SyntaxNode node);
    }
    public static class ScriptWalker
    {
        public static void Walk(in SyntaxNode node, in IScriptWalker walker)
        {
            if (node is null) { throw new ArgumentNullException(nameof(node)); }
            if (walker is null) { throw new ArgumentNullException(nameof(walker)); }

            Visit(in node, in walker);
        }
        private static void Visit(in SyntaxNode node, in IScriptWalker walker)
        {
            walker.SayHello(in node);

            VisitChildren(in node, in walker);

            walker.SayGoodbye(in node);
        }
        private static void VisitChildren(in SyntaxNode parent, in IScriptWalker walker)
        {
            Type type = parent.GetType();

            foreach (PropertyInfo property in type.GetProperties())
            {
                Type propertyType = property.PropertyType;

                if (property.GetCustomAttribute<JsonIgnoreAttribute>() is not null)
                {
                    continue;
                }

                object value = property.GetValue(parent);

                if (value == null)
                {
                    continue;
                }

                if (propertyType.IsSyntaxNode())
                {
                    Visit((value as SyntaxNode), in walker);
                }
                else if (propertyType.IsListOfSyntaxNodes())
                {
                    if (value is IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            Visit((list[i] as SyntaxNode), in walker);
                        }
                    }
                }
            }
        }
    }
}