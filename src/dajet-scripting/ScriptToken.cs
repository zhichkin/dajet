namespace DaJet.Scripting
{
    public sealed class ScriptToken
    {
        public ScriptToken(TokenType tokenType)
        {
            Type = tokenType;
        }
        public TokenType Type { get; }
        public string Lexeme { get; set; }
        public int Line { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
        public override string ToString()
        {
            return $"{Type} {{{Line}}} [{Offset}-{Offset + Length - 1}] {Lexeme}";
        }
    }
}