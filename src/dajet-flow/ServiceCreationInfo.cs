using Microsoft.Extensions.DependencyInjection;

namespace DaJet.Flow
{
    internal class ServiceCreationInfo
    {
        internal Type Service { get; init; }
        internal Type[] Options { get; init; }
        internal ObjectFactory Factory { get; init; }
        internal Type Input { get; init; }
        internal Type Output { get; init; }
    }
}