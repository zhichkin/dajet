using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace DaJet.Scripting.Test
{
    [TestClass] public class Test_Tokenizer
    {
        [TestMethod] public void Tokenize()
        {
            ScriptTokenizer scanner = new();

            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test"))
            {
                Console.WriteLine("***");
                Console.WriteLine(filePath);

                using (StreamReader reader = new(filePath, Encoding.UTF8))
                {
                    string script = reader.ReadToEnd();

                    if (!scanner.TryTokenize(in script, out List<ScriptToken> tokens, out string error))
                    {
                        Console.WriteLine(error);
                        continue;
                    }

                    foreach (ScriptToken token in tokens)
                    {
                        Console.WriteLine(token);
                    }
                }
            }
        }
    }
}