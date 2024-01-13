using System.CommandLine;

namespace DaJet
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            args = new string[]
            {
                "script", "--file", "./test/test-declare-select.txt" //"./test/apply.txt"
            };

            var root = new RootCommand("dajet");
            
            var command = new Command("script", "Execute DaJet script");
            var option = new Option<string>("--file", "Script file path");
            command.Add(option);
            command.SetHandler(ExecuteScript, option);

            root.Add(command);

            return root.Invoke(args);
        }
        private static void ExecuteScript(string filePath)
        {
            FileInfo file = new(filePath);

            Console.WriteLine($"Execute script: {file.FullName}");

            ScriptEngine.Execute(in filePath);
        }
    }
}