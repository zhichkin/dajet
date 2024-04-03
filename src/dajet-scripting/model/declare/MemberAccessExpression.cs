using System.Linq.Expressions;
using System.Text;

namespace DaJet.Scripting.Model
{
    public sealed class MemberAccessExpression : SyntaxNode
    {
        public MemberAccessExpression() { Token = TokenType.Variable; }
        public string Identifier { get; set; } = string.Empty; // @variable.member
        public object Binding { get; set; }
        public override string ToString()
        {
            return $"[{Token}: {Identifier}]";
        }
        public string GetVariableName()
        {
            List<string> members = ParserHelper.GetAccessMembers(Identifier);
            
            return members[0]; // @variable
        }
        public string GetDbParameterName()
        {
            // @variable.member -> @variable_member
            // @variable.member[0].member -> @variable_member_0_member
            // @variable.member[id=123].member -> @variable_member_id_member

            List<string> members = ParserHelper.GetAccessMembers(Identifier);

            StringBuilder name = new();

            for (int i = 0; i < members.Count; i++)
            {
                if (i > 0) { name.Append('_'); }
                
                if (members[i].StartsWith('['))
                {
                    name.Append(members[i].TrimStart('[').TrimEnd(']').Split('=')[0]);
                }
                else
                {
                    name.Append(members[i]);
                }
            }

            return name.ToString();
        }
    }
}