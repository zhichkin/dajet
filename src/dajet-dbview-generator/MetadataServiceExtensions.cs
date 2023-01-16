using DaJet.Metadata.Model;

namespace DaJet.Metadata.Services
{
    public static class MetadataServiceDbViewExtensions
    {
        private const string INFOBASE_KEY_IS_NOT_FOUND = "InfoBase key [{0}] is not found.";
        public static bool TryGetDbViewGenerator(this IMetadataService service, string key, out IDbViewGenerator generator, out string error)
        {
            generator = null!;

            if (!service.TryGetInfoBase(key, out InfoBase _, out error))
            {
                return false;
            }

            DbViewGeneratorOptions options = null!;

            foreach (InfoBaseOptions option in service.Options)
            {
                if (option.Key == key)
                {
                    options = new()
                    {
                        DatabaseProvider = option.DatabaseProvider,
                        ConnectionString = option.ConnectionString
                    };

                    break;
                }
            }

            if (options == null)
            {
                error = string.Format(INFOBASE_KEY_IS_NOT_FOUND, key);
                return false;
            }

            try
            {
                generator = DbViewGenerator.Create(options);
            }
            catch (Exception exception)
            {
                error = ExceptionHelper.GetErrorMessage(exception);
            }

            return (generator != null);
        }
    }
}