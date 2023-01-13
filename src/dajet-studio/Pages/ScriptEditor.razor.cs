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
        protected bool ScriptIsChanged { get; set; } = false;
        protected string ErrorText { get; set; } = string.Empty;
        protected string ResultText { get; set; } = string.Empty;
        protected List<Dictionary<string, object>> ResultTable { get; set; }
        protected override async Task OnInitializedAsync()
        {
            if (Uuid != Guid.Empty)
            {
                Model = await SelectScript(Uuid);
            }
            else
            {
                Model.Uuid = Guid.NewGuid();
                Model.Name = "NewScript";
                Model.IsFolder = false;
                Model.Owner = AppState.CurrentInfoBase;
            }
        }
        protected void OnNameChanged(ChangeEventArgs args)
        {
            Model.Name = args.Value.ToString();
            ScriptIsChanged = true;
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

            QueryRequest request = new()
            {
                DbName = Model.Owner,
                Script = Model.Script
            };

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/1ql/prepare", request);

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

            QueryRequest request = new()
            {
                DbName = Model.Owner,
                Script = Model.Script
            };

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/1ql/execute", request);

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

            QueryRequest request = new()
            {
                DbName = Model.Owner,
                Script = Model.Script
            };

            try
            {
                HttpResponseMessage response = await Http.PostAsJsonAsync("/1ql/execute", request);

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
    }
}