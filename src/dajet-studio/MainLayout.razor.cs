using DaJet.Studio.Model;
using DaJet.Studio.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio
{
    public partial class MainLayout : LayoutComponentBase
    {
        protected void NavigateToHomePage()
        {
            Navigator.NavigateTo("/");
        }
        protected override async Task OnInitializedAsync()
        {
            await IntializeInfoBaseList();
        }
        private async Task IntializeInfoBaseList()
        {
            try
            {
                AppState.FooterText = "Загрузка списка баз данных...";

                AppState.DatabaseList.Clear();

                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseModel> list = await response.Content.ReadFromJsonAsync<List<InfoBaseModel>>();

                foreach (InfoBaseModel model in list)
                {
                    AppState.DatabaseList.Add(model);
                }

                if (list != null && list.Count > 0)
                {
                    AppState.CurrentDatabase = list[0];
                }

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

            AppState.RefreshInfoBaseCommand?.Invoke();
        }
        protected async Task RegisterNewInfoBase(MouseEventArgs args)
        {
            InfoBaseModel model = new();
            DialogOptions options = new() { CloseButton = true };
            DialogParameters parameters = new()
            {
                { "Model", model }
            };
            var dialog = DialogService.Show<InfoBaseDialog>("DaJet Studio", parameters, options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not InfoBaseModel entity)
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

                AppState.DatabaseList.Add(entity);

                StateHasChanged();

                Snackbar.Add($"База данных [{entity}] добавлена успешно.", Severity.Success);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
        protected async Task UnRegisterInfoBase(MouseEventArgs args)
        {
            InfoBaseModel database = AppState.CurrentDatabase;
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

                AppState.DatabaseList.Remove(database);

                AppState.CurrentDatabase = AppState.DatabaseList.Count == 0 ? null : AppState.DatabaseList[0];

                StateHasChanged();

                Snackbar.Add($"База данных [{database}] удалена успешно.", Severity.Info);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
        protected void CreateNewScript(MouseEventArgs args)
        {
            Navigator.NavigateTo("/script-editor");
        }
    }
}