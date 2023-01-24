using DaJet.Studio.Model;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaJet.Studio
{
    public sealed class AppState : INotifyPropertyChanged
    {
        private InfoBaseModel _database;
        public Action RefreshInfoBaseCommand;
        public InfoBaseModel CurrentDatabase
        {
            get { return _database; }
            set { ChangeState(ref _database, value); }
        }
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

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        private void ChangeState<T>(ref T target, T value, [CallerMemberName] string propertyName = null)
        {
            if (value is InfoBaseModel && propertyName == nameof(CurrentDatabase))
            {
                target = value;
                OnPropertyChanged(propertyName);
            }
        }
    }
}