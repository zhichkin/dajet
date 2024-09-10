using System;
using System.Collections.Generic;
using System.Reflection;

namespace DaJet
{
    public interface IAssemblyManager
    {
        Type Resolve(string name);
        void Register(string path);
        void Register(Assembly assembly);
        IEnumerable<Assembly> Assemblies { get; }
    }
}