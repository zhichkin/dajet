using DaJet.Scripting.Model;
using System.Collections;
using System.Reflection;

namespace DaJet.Scripting
{
    public static class ScriptWalker
    {
        public static void Walk(in SyntaxNode node, in IScriptVisitor visitor)
        {
            if (node == null || visitor == null)
            {
                return;
            }

            Visit(in node, in visitor);
        }
        private static bool IsSyntaxNode(Type type)
        {
            return type == typeof(SyntaxNode)
                || type.IsSubclassOf(typeof(SyntaxNode));
        }
        private static bool IsSyntaxNodeList(Type type)
        {
            return type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(List<>)
                && IsSyntaxNode(type.GetGenericArguments()[0]);
        }
        private static void Visit(in SyntaxNode node, in IScriptVisitor visitor)
        {
            visitor?.SayHello(node);

            VisitChildren(in node, in visitor);

            visitor?.SayGoodbye(node);
        }
        private static void VisitChildren(in SyntaxNode parent, in IScriptVisitor visitor)
        {
            Type type = parent.GetType();

            foreach (PropertyInfo property in type.GetProperties())
            {
                Type propertyType = property.PropertyType;

                object value = property.GetValue(parent)!;

                if (value == null)
                {
                    continue;
                }

                if (IsSyntaxNode(propertyType))
                {
                    Visit((value as SyntaxNode), in visitor);
                }
                else if (IsSyntaxNodeList(propertyType))
                {
                    if (value is IList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            Visit((list[i] as SyntaxNode), in visitor);
                        }
                    }
                }
            }
        }
    }
}