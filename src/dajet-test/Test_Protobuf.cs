using DaJet.Data;
using DaJet.Metadata;
using DaJet.Metadata.Core;
using DaJet.Metadata.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaJet.Protobuf.Test
{
    [TestClass]
    public class Test_Protobuf
    {
        private const string IB_KEY = "cerberus";
        private readonly InfoBase _infoBase;
        private readonly MetadataCache _cache;
        private readonly MetadataService _service = new();
        private const string MS_CONNECTION_STRING = "Data Source=ZHICHKIN;Initial Catalog=cerberus;Integrated Security=True;Encrypt=False;";
        public Test_Protobuf()
        {
            _service.Add(new InfoBaseOptions()
            {
                Key = IB_KEY,
                ConnectionString = MS_CONNECTION_STRING,
                DatabaseProvider = DatabaseProvider.SqlServer
            });

            if (!_service.TryGetInfoBase(IB_KEY, out _infoBase, out string error))
            {
                throw new InvalidOperationException($"Failed to open info base: {error}");
            }

            if (!_service.TryGetMetadataCache(IB_KEY, out _cache, out error))
            {
                throw new InvalidOperationException($"Failed to get metadata cache: {error}");
            }
        }
        [TestMethod] public void Count_Objects()
        {
            int count = 0;

            Publication publication = _cache.GetMetadataObject<Publication>("ПланОбмена.ПланОбменаДанными");

            foreach (var article in publication.Articles)
            {
                foreach (Guid type in MetadataTypes.ApplicationObjectTypes)
                {
                    MetadataObject item = _cache.GetMetadataObject(type, article.Key);
                    
                    if (item != null)
                    {
                        count++;

                        if (item is ITablePartOwner owner)
                        {
                            count += owner.TableParts.Count;
                        }
                        
                        break;
                    }
                }
            }

            Console.WriteLine($"Count = {count}");
        }
    }
}