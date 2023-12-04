using System.Data;
using System.Data.Common;

namespace DaJet.Data.Client
{
    public sealed class OneDbParameter : DbParameter
    {
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override int Size { get; set; }
        public override bool IsNullable { get; set; }
        public override string SourceColumn { get; set; } = string.Empty;
        public override bool SourceColumnNullMapping { get; set; }
        public override void ResetDbType() { DbType = DbType.Object; }
        public override DbType DbType { get; set; } = DbType.Object;
        public override string ParameterName { get; set; } = string.Empty;
        public override object Value { get; set; }
    }
}