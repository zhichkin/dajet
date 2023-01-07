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
        protected string FooterText { get; set; } = string.Empty;
        protected List<string> InfoBaseList { get; set; } = new();
        protected override async Task OnInitializedAsync()
        {
            await IntializeInfoBaseList();
        }
        private async Task IntializeInfoBaseList()
        {
            try
            {
                FooterText = "Загрузка списка баз данных...";

                InfoBaseList.Clear();

                HttpResponseMessage response = await Http.GetAsync("/md");

                List<InfoBaseModel> list = await response.Content.ReadFromJsonAsync<List<InfoBaseModel>>();

                foreach (InfoBaseModel model in list)
                {
                    InfoBaseList.Add(model.Name);
                }

                if (list != null && list.Count > 0)
                {
                    AppState.CurrentInfoBase = list[0]?.Name;
                }

                FooterText = string.Empty;
            }
            catch (Exception error)
            {
                FooterText = "Ошибка загрузки списка баз данных!";
            }
        }
        protected async Task RefreshInfoBaseList(MouseEventArgs args)
        {
            await IntializeInfoBaseList();

            AppState.RefreshInfoBaseCommand?.Invoke();
        }
        protected async Task RegisterNewInfoBase(MouseEventArgs args)
        {
            DialogOptions options = new() { CloseButton = true };
            var dialog = DialogService.Show<InfoBaseDialog>("DaJet Studio", options);
            var result = await dialog.Result;
            if (result.Cancelled) { return; }

            if (result.Data is not InfoBaseModel model)
            {
                return;
            }

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/md", model);

                if (response.StatusCode != HttpStatusCode.Created)
                {
                    throw new Exception(response.ReasonPhrase); // No Content
                }

                InfoBaseList.Add(model.Name);

                StateHasChanged();

                Snackbar.Add($"База данных [{model.Name}] добавлена успешно.", Severity.Success);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
        protected async Task UnRegisterInfoBase(MouseEventArgs args)
        {
            string database = AppState.CurrentInfoBase;

            bool? result = await DialogService.ShowMessageBox(
                "DaJet Studio",
                $"Удалить базу данных [{database}] из списка ?",
                yesText: "Удалить", cancelText: "Отмена");

            if (result == null) { return; }

            try
            {
                HttpRequestMessage request = new HttpRequestMessage
                {
                    Method = HttpMethod.Delete,
                    RequestUri = new Uri("/md", UriKind.Relative),
                    Content = JsonContent.Create(new InfoBaseModel()
                    {
                        Name = database
                    })
                };
                HttpResponseMessage response = await Http.SendAsync(request);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new Exception(response.ReasonPhrase);
                }

                InfoBaseList.Remove(database);

                AppState.CurrentInfoBase = InfoBaseList.Count == 0 ? string.Empty : InfoBaseList[0];

                StateHasChanged();

                Snackbar.Add($"База данных [{database}] удалена успешно.", Severity.Info);
            }
            catch (Exception error)
            {
                Snackbar.Add(error.Message, Severity.Error);
            }
        }
    }
}