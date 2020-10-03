using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class ExportPreferences_Layout : ContainerLayout
    {
        public ExportPreferences_Layout(UserPreferences_Database database, LayoutStack layoutStack)
        {
            this.database = database;
            this.layoutStack = layoutStack;
            Button exportButton = new Button();
            exportButton.Clicked += ExportButton_Clicked;
            this.SubLayout = new ButtonLayout(exportButton, "Export");
        }

        private void ExportButton_Clicked(object sender, EventArgs e)
        {
            this.doExport();
        }

        private async void doExport()
        {
            string text = this.textConverter.ConvertToString(this.database);
            DateTime when = DateTime.Now;
            String dateText = when.ToString("yyyy-MM-dd-HH-mm-ss");
            string filepath = "TopicPreferences-" + dateText + ".txt";
            bool successful = await this.publicFileIo.ExportFile(filepath, text);

            string message;
            if (successful)
            {
                message = "Exported " + filepath + " successfully";
            }
            else
            {
                message = "Failed to export " + filepath + ".";
            }
            this.layoutStack.AddLayout(new TextblockLayout(message), "Export Results");
        }

        UserPreferences_Database database;
        TextConverter textConverter = new TextConverter();
        PublicFileIo publicFileIo = new PublicFileIo();
        LayoutStack layoutStack;
    }
}
