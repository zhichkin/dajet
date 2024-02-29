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
        public string GetTargetName()
        {
            return Identifier.Split('.')[0]; // @variable
        }
        public string GetMemberName()
        {
            return Identifier.Split('.')[1]; // member
        }
        public string GetDbParameterName()
        {
            return Identifier.Replace('.', '_'); // @variable.member -> @variable_member
        }
        public MemberAccessDescriptor ToDescriptor()
        {
            string[] identifier = Identifier.Split('.');

            return new MemberAccessDescriptor()
            {
                Target = identifier[0],
                Member = identifier[1],
                MemberType = Binding as Type
            };
        }
    }
}