using DaJet.Model;
using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using System.Net;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class ScriptEditor : ComponentBase
    {
        [Parameter] public Guid Uuid { get; set; } = Guid.Empty;
        protected ScriptModel Model { get; set; } = new ScriptModel();
        protected string DatabaseName { get; set; } = string.Empty;
        protected string ScriptUrl { get; set; } = string.Empty;
        protected bool ScriptIsChanged { get; set; } = false;
        protected string ErrorText { get; set; } = string.Empty;
        protected string ResultText { get; set; } = string.Empty;
        protected List<Dictionary<string, object>> ResultTable { get; set; }
        protected override async Task OnParametersSetAsync()
        {
            if (Uuid != Guid.Empty)
            {
                Model = await SelectScript(Uuid);
                ScriptUrl = await SelectScriptUrl(Model.Uuid);
            }
            else
            {
                Model.Name = "Скрипт 1QL";
                Model.IsFolder = false;
                Model.Owner = AppState.CurrentDatabase.Identity;
                ScriptUrl = Model.Name;
            }

            InfoBaseRecord database = AppState.GetDatabase(Model.Owner);
            DatabaseName = (database == null ? "База данных не определена" : database.Name);

            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;
            ScriptIsChanged = false;
        }
        protected void OnScriptChanged(ChangeEventArgs args)
        {
            Model.Script = args.Value.ToString();
            ScriptIsChanged = true;
        }
        protected void CloseScriptEditor()
        {
            Navigator.NavigateTo("/");
        }
        private async Task<string> SelectScriptUrl(Guid uuid)
        {
            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/api/url/{uuid}");

                string result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    return result;
                }

                ErrorText = response.ReasonPhrase
                    + (string.IsNullOrEmpty(result)
                    ? string.Empty
                    : Environment.NewLine + result);
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }

            return string.Empty;
        }
        private async Task<ScriptModel> SelectScript(Guid uuid)
        {
            ScriptModel script = null;

            try
            {
                HttpResponseMessage response = await Http.GetAsync($"/api/{uuid}");

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
                else
                {
                    script = await response.Content.ReadFromJsonAsync<ScriptModel>();
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }

            return script;
        }
        private async Task SaveScriptCommand()
        {
            try
            {
                HttpResponseMessage response = await Http.PutAsJsonAsync($"/api", Model);

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    AppState.FooterText = response.ReasonPhrase;
                }
                else
                {
                    ScriptIsChanged = false;
                }
            }
            catch (Exception error)
            {
                AppState.FooterText = error.Message;
            }
        }
        protected async Task ExecuteScriptSql()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            try
            {
                InfoBaseRecord database = AppState.GetDatabaseOrThrowException(Model.Owner);

                QueryRequest request = new()
                {
                    DbName = database.Name,
                    Script = Model.Script
                };

                HttpResponseMessage response = await Http.PostAsJsonAsync("/query/prepare", request);

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
                else
                {
                    QueryResponse result = await response.Content.ReadFromJsonAsync<QueryResponse>();

                    if (result.Success)
                    {
                        ResultText = result.Script;
                    }
                    else
                    {
                        ErrorText = result.Error;
                    }
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }
        }
        protected async Task ExecuteScriptJson()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            try
            {
                InfoBaseRecord database = AppState.GetDatabaseOrThrowException(Model.Owner);

                Dictionary<string, object> parameters = new();

                HttpResponseMessage response = await Http.PostAsJsonAsync(ScriptUrl, parameters);

                string result = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    ResultText = result;
                }
                else
                {
                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }
        }
        protected async Task ExecuteScriptTable()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            try
            {
                InfoBaseRecord database = AppState.GetDatabaseOrThrowException(Model.Owner);

                Dictionary<string, object> parameters = new();

                HttpResponseMessage response = await Http.PostAsJsonAsync(ScriptUrl, parameters);

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
                else
                {
                    ResultTable = await response.Content.ReadFromJsonAsync<List<Dictionary<string, object>>>();
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }
        }

        protected async Task ExecuteNonQuery()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            try
            {
                InfoBaseRecord database = AppState.GetDatabaseOrThrowException(Model.Owner);

                QueryRequest request = new()
                {
                    DbName = database.Name,
                    Script = Model.Script
                };

                HttpResponseMessage response = await Http.PostAsJsonAsync("/query/ddl", request);

                if (!response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync();

                    ErrorText = response.ReasonPhrase
                        + (string.IsNullOrEmpty(result)
                        ? string.Empty
                        : Environment.NewLine + result);
                }
                else
                {
                    QueryResponse result = await response.Content.ReadFromJsonAsync<QueryResponse>();

                    if (result.Success)
                    {
                        ResultText = result.Script;
                    }
                    else
                    {
                        ErrorText = result.Error;
                    }
                }
            }
            catch (Exception error)
            {
                ErrorText = error.Message;
            }
        }
    }
}