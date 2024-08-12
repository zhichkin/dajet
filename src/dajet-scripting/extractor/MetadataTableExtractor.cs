using DaJet.Scripting.Model;

namespace DaJet.Scripting
{
    public sealed class MetadataTableExtractor : IScriptWalker
    {
        private readonly Dictionary<string, TableReference> _result = new();
        public List<TableReference> Extract(in SyntaxNode node)
        {
            ScriptWalker.Walk(in node, this);

            List<TableReference> list = _result.Values.ToList();

            return list;
        }
        public void SayHello(in SyntaxNode node)
        {
            if (node is TableReference table)
            {
                if (table.Identifier == "Метаданные.Объекты" ||
                    table.Identifier == "Метаданные.Свойства")
                {
                    _ = _result.TryAdd(table.Identifier, table);
                }
            }
        }
        public void SayGoodbye(in SyntaxNode node)
        {
            // do nothing
        }
    }
}