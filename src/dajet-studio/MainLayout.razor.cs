using DaJet.Studio.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using System.Net.Http.Json;

namespace DaJet.Studio
{
    public partial class MainLayout : LayoutComponentBase
    {
        protected string FooterText { get; set; } = string.Empty;
        //private string _current = string.Empty;
        //protected string CurrentInfoBase
        //{
        //    get { return _current; }
        //    set
        //    {
        //        _current = value;
        //    }
        //}
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
        protected void RegisterInfoBase(MouseEventArgs args)
        {

        }
    }
}