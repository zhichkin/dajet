using DaJet.Http.Client;
using DaJet.Model;
using Microsoft.AspNetCore.Components;

namespace DaJet.Studio.Pages
{
    public partial class ScriptEditor : ComponentBase
    {
        [Parameter] public Guid Uuid { get; set; } = Guid.Empty;
        protected ScriptRecord Model { get; set; }
        protected string DatabaseName { get; set; } = string.Empty;
        protected string ScriptUrl { get; set; } = string.Empty;
        protected bool ScriptIsChanged { get; set; } = false;
        protected string ErrorText { get; set; } = string.Empty;
        protected string ResultText { get; set; } = string.Empty;
        protected List<Dictionary<string, object>> ResultTable { get; set; }
        protected void NavigateToHomePage() { Navigator.NavigateTo("/"); }
        protected override async Task OnParametersSetAsync()
        {
            if (Uuid != Guid.Empty)
            {
                Model = await DataSource.SelectAsync<ScriptRecord>(Uuid);
                ScriptUrl = await DataSource.GetScriptUrl(Uuid);
            }
            else
            {
                Model = DataSource.Model.New<ScriptRecord>();
                Model.Name = "Скрипт 1QL";
                Model.IsFolder = false;
                Model.Owner = DataSource.Model.GetEntity<InfoBaseRecord>(AppState.CurrentDatabase.Identity);
                ScriptUrl = Model.Name;
            }

            InfoBaseRecord database = await DataSource.SelectAsync<InfoBaseRecord>(Model.Owner);
            
            DatabaseName = (database is null ? "База данных не определена" : database.Name);

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
        private async Task SaveScriptCommand()
        {
            try
            {
                if (Model.IsNew())
                {
                    await DataSource.CreateAsync(Model);
                }
                else if (Model.IsChanged())
                {
                    await DataSource.UpdateAsync(Model);
                }
                
                ScriptIsChanged = !Model.IsOriginal();
            }
            catch (Exception error)
            {
                AppState.FooterText = error.Message;
            }
        }
        protected async Task ExecuteNonQuery()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            QueryResponse result = await DataSource.ExecuteNonQuery(Model);

            if (result.Success)
            {
                ResultText = result.Script;
            }
            else
            {
                ErrorText = result.Error;
            }
        }
        protected async Task ExecuteScriptSql()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            QueryResponse result = await DataSource.ExecuteScriptSql(Model);

            if (result.Success)
            {
                ResultText = result.Script;
            }
            else
            {
                ErrorText = result.Error;
            }
        }
        protected async Task ExecuteScriptJson()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            QueryResponse response = await DataSource.ExecuteScriptJson(Model);

            if (response.Success)
            {
                ResultText = response.Script;
            }
            else
            {
                ErrorText = response.Error;
            }
        }
        protected async Task ExecuteScriptTable()
        {
            ErrorText = string.Empty;
            ResultText = string.Empty;
            ResultTable = null;

            QueryResponse response = await DataSource.ExecuteScriptTable(Model);

            if (response.Success)
            {
                ResultTable = response.Result as List<Dictionary<string, object>>;
            }
            else
            {
                ErrorText = response.Error;
            }
        }

        
    }
}