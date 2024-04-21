using DaJet.Model;
using System.Reflection;
using System.Runtime.Loader;

namespace DaJet.Flow
{
    public sealed class AssemblyManager : IAssemblyManager
    {
        private readonly Dictionary<string, Assembly> _assemblies = new();
        public IEnumerable<Assembly> Assemblies { get { return _assemblies.Values; } }
        private void Load(string filePath)
        {
            try
            {
                Assembly assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(filePath);

                Register(assembly);
            }
            catch (Exception error)
            {
                //TODO: log exception
            }
        }
        public void Register(string path)
        {
            string location = Path.Combine(AppContext.BaseDirectory, path);

            if (File.Exists(location) && Path.GetExtension(path) == ".dll")
            {
                Load(location);
            }
            else // load from directory
            {
                foreach (string filePath in Directory.GetFiles(location, "*.dll", SearchOption.AllDirectories))
                {
                    Load(filePath);
                }
            }
        }
        public void Register(Assembly assembly)
        {
            if (assembly is null)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            string name = assembly.GetName().Name;

            _ = _assemblies.TryAdd(name, assembly);
        }
        public Type Resolve(string name)
        {
            Type type = Type.GetType(name);

            if (type is not null)
            {
                return type;
            }

            foreach (Assembly assembly in Assemblies)
            {
                type = assembly.GetType(name);

                if (type is not null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}