﻿using DaJet.Model;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DaJet.Studio
{
    public sealed class AppState : INotifyPropertyChanged
    {
        private InfoBaseRecord _database;
        public Action RefreshInfoBaseCommand;
        public InfoBaseRecord CurrentDatabase
        {
            get { return _database; }
            set { ChangeState(ref _database, value); }
        }
        public List<InfoBaseRecord> DatabaseList { get; set; } = new();
        public InfoBaseRecord GetDatabase(Guid uuid)
        {
            for (int i = 0; i < DatabaseList.Count; i++)
            {
                if (DatabaseList[i].Identity == uuid)
                {
                    return DatabaseList[i];
                }
            }
            return null;
        }
        public InfoBaseRecord GetDatabaseOrThrowException(Guid uuid)
        {
            InfoBaseRecord database = GetDatabase(uuid);

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
            if (value is InfoBaseRecord && propertyName == nameof(CurrentDatabase))
            {
                target = value;
                OnPropertyChanged(propertyName);
            }
        }
    }
}