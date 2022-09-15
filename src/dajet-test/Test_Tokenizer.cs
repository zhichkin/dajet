using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Text;

namespace DaJet.Scripting.Test
{
    [TestClass] public class Test_Tokenizer
    {
        [TestMethod] public void Tokenize()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_With_Cte()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\cte"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_Destructive_Read()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\destructive-read"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_Expression()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\expression"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_OrderBy_Offset_Fetch()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\paging"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_GroupBy_Having()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\group-having"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_Window_Function()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\over-window"))
            {
                TokenizeFile(in filePath);
            }
        }
        [TestMethod] public void Tokenize_Case_When_Then_Else()
        {
            foreach (string filePath in Directory.GetFiles("C:\\temp\\scripting-test\\case-when-then-else"))
            {
                TokenizeFile(in filePath);
            }
        }
        private void TokenizeFile(in string filePath)
        {
            ScriptTokenizer scanner = new();

            Console.WriteLine("***");
            Console.WriteLine(filePath);

            using (StreamReader reader = new(filePath, Encoding.UTF8))
            {
                string script = reader.ReadToEnd();

                if (!scanner.TryTokenize(in script, out List<ScriptToken> tokens, out string error))
                {
                    Console.WriteLine(error);
                    return;
                }

                foreach (ScriptToken token in tokens)
                {
                    Console.WriteLine(token);
                }
            }
        }
    }
}