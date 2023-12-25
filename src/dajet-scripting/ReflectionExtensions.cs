using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public static class ReflectionExtensions
    {
        public static bool IsSyntaxNode(this Type type)
        {
            return type == typeof(SyntaxNode)
                || type.IsSubclassOf(typeof(SyntaxNode));
        }
        public static bool IsListOfSyntaxNodes(this Type type)
        {
            return type.IsGenericType
                && type.GetGenericTypeDefinition() == typeof(List<>)
                && IsSyntaxNode(type.GetGenericArguments()[0]);
        }
    }
}