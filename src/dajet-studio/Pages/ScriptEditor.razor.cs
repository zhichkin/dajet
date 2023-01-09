using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using System.Net.Http.Json;

namespace DaJet.Studio.Pages
{
    public partial class ScriptEditor : ComponentBase
    {
        protected string Database { get; set; } = string.Empty;
        protected string ScriptText { get; set; } = "Код скрипта ...";
        protected string ErrorText { get; set; } = string.Empty;
        protected string ResultText { get; set; } = string.Empty;
        protected List<Dictionary<string, object>> ResultTable { get; set; }
        protected override void OnInitialized()
        {
            Database = AppState.CurrentInfoBase;
        }
        protected void CloseScriptEditor()
        {
            Navigator.NavigateTo("/");
        }
        protected async Task ExecuteScriptSql()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            QueryRequest request = new()
            {
                DbName = Database,
                Script = ScriptText
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
                DbName = Database,
                Script = ScriptText
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
                DbName = Database,
                Script = ScriptText
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