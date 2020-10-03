using Plugin.FilePicker.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;
using VisiPlacement;
using Xamarin.Forms;

namespace TopicFilterer.View
{
    class ImportPreferences_Layout : ContainerLayout
    {
        public event Import RequestImport;
        public delegate void Import(string fileContent);
        public ImportPreferences_Layout()
        {
            Button importButton = new Button();
            importButton.Clicked += ImportButton_Clicked;
            this.SubLayout = new ButtonLayout(importButton, "Import");
        }

        private void ImportButton_Clicked(object sender, EventArgs e)
        {
            this.doImport();
        }

        private async void doImport()
        {
            FileData fileData = await this.publicFileIo.PromptUserForFile();
            if (this.RequestImport != null)
            {
                byte[] data = fileData.DataArray;
                string content = System.Text.Encoding.UTF8.GetString(fileData.DataArray, 0, fileData.DataArray.Length);
                this.RequestImport.Invoke(content);
            }
        }

        PublicFileIo publicFileIo = new PublicFileIo();
    }
}
