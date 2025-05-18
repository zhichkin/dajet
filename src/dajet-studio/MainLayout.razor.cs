﻿using DaJet.Model;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio
{
    public partial class MainLayout : LayoutComponentBase, IDisposable
    {
        protected void NavigateToHomePage()
        {
            Navigator.NavigateTo("/");
        }
        protected override async Task OnInitializedAsync()
        {
            if (AppState != null)
            {
                AppState.PropertyChanged += AppStateChangedHandler;
            }

            await IntializeInfoBaseList();
        }
        public void Dispose()
        {
            if (AppState != null)
            {
                AppState.PropertyChanged -= AppStateChangedHandler;
            }
        }
        private void AppStateChangedHandler(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(AppState.CurrentDatabase))
            {
                StateHasChanged();
            }
        }
        private async Task IntializeInfoBaseList()
        {
            try
            {
                AppState.FooterText = "Загрузка списка баз данных...";

                AppState.DatabaseList.Clear();

                HttpResponseMessage response = await Http.GetAsync("/md");

                IEnumerable<InfoBaseRecord> list = await DataSource.QueryAsync<InfoBaseRecord>();

                foreach (InfoBaseRecord model in list)
                {
                    AppState.DatabaseList.Add(model);
                }

                AppState.CurrentDatabase = list.FirstOrDefault();

                AppState.FooterText = string.Empty;
            }
            catch (Exception error)
            {
                AppState.FooterText = "Ошибка загрузки списка баз данных!";
            }
        }
        protected async Task RefreshInfoBaseList(MouseEventArgs args)
        {
            await IntializeInfoBaseList();
            //StateHasChanged();
            AppState.RefreshInfoBaseCommand?.Invoke();
        }
        protected async Task RegisterNewInfoBase(MouseEventArgs args)
        {
            InfoBaseRecord model = DataSource.Model.New<InfoBaseRecord>();
            DialogOptions options = new() { CloseButton = true };
            DialogParameters parameters = new()
            {
                { "Model", model }
            };
            var dialog = DialogService.Show<InfoBaseDialog>("DaJet Studio", parameters, options);
            var result = await dialog.Result;
            if (result.Canceled) { return; }

            if (result.Data is not InfoBaseRecord entity)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/md", entity);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception(response.ReasonPhrase); // No Content
                }

                Snackbar.Add($"База данных [{entity}] добавлена успешно.", Severity.Success);

                await RefreshInfoBaseList(null);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
        protected async Task UnRegisterInfoBase(MouseEventArgs args)
        {
            InfoBaseRecord database = AppState.CurrentDatabase;
            if (database is null) { return; }

            bool? result = await DialogService.ShowMessageBox(
                "DaJet Studio",
                $"Удалить базу данных [{database}] из списка ?",
                yesText: "Удалить", cancelText: "Отмена");
            if (result is null) { return; }

            try
            {
                HttpRequestMessage request = new()
                {
                    Method = HttpMethod.Delete,
                    Content = JsonContent.Create(database),
                    RequestUri = new Uri("/md", UriKind.Relative)
                };
                HttpResponseMessage response = await Http.SendAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(response.ReasonPhrase);
                }

                Snackbar.Add($"База данных [{database}] удалена успешно.", Severity.Info);

                await RefreshInfoBaseList(null);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
    }
}