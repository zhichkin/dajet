using DaJet.Studio.Model;

namespace DaJet.Studio
{
    public sealed class AppState
    {
        public Action RefreshInfoBaseCommand;
        public InfoBaseModel CurrentDatabase { get; set; }
        public List<InfoBaseModel> DatabaseList { get; set; } = new();
        public InfoBaseModel GetDatabase(Guid uuid)
        {
            for (int i = 0; i < DatabaseList.Count; i++)
            {
                if (DatabaseList[i].Uuid == uuid)
                {
                    return DatabaseList[i];
                }
            }
            return null;
        }
        public InfoBaseModel GetDatabaseOrThrowException(Guid uuid)
        {
            InfoBaseModel database = GetDatabase(uuid);

            if (database is null)
            {
               throw new Exception($"База данных {{{uuid}}} не найдена!");
            }

            return database;
        }
        public string FooterText { get; set; } = string.Empty;
        private string _error = string.Empty;
        public string LastErrorText
        {
            get { return _error; }
            set
            {
                _error = value;
                AppErrorEventHandler?.Invoke(_error);
            }
        }
        public event Action<string> AppErrorEventHandler;
    }
}