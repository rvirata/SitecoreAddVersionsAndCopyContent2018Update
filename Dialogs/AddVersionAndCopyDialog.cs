namespace Sitecore.SharedSource.SmartTools.Dialogs
{
    using Sitecore;
    using Configuration;
    using Data;
    using Data.Fields;
    using Data.Items;
    using Data.Managers;
    using Diagnostics;
    using Globalization;
    using Shell.Applications.ContentEditor;
    using Shell.Applications.Dialogs.ProgressBoxes;
    using Web.UI.HtmlControls;
    using Web.UI.Pages;
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Data;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Xml;
    using Web;

    public class AddVersionAndCopyDialog : DialogForm
    {
        protected Language sourceLanguage;
        protected bool CopySubItems;

        protected Dictionary<string, string> langNames;
        protected Combobox Source;
        protected Literal TargetLanguages;
        protected Literal Options;
        protected TreeList TreeListOfItems;
        protected List<Language> targetLanguagesList;
        const string SUPPORTED_LANGUAGES_ITEM = "{53127F56-EA44-4B04-9898-93597CBF3D27}";
        const string SUPPORTED_LANGUAGES_FIELD = "{055E7D18-CDCE-4021-BBA3-CCEC0754B565}";

        private void fillLanguageDictionary()
        {
            this.langNames = new Dictionary<string, string>();

            Item currentItem = GetCurrentItem();

            var db = Factory.GetDatabase("master");
            var supportedLangsItem = db.GetItem(SUPPORTED_LANGUAGES_ITEM);
            var supportedLangString = supportedLangsItem?.Fields[SUPPORTED_LANGUAGES_FIELD]?.Value;
            var supportedLangIDList = supportedLangsItem?.Fields[SUPPORTED_LANGUAGES_FIELD]?.Value?.Split('|');

            List<OrderedLanguages> supportedLangList = new List<OrderedLanguages>();

            foreach (var langID in supportedLangIDList)
            {
                var supportedLanguage = LanguageManager.GetLanguage(db.GetItem(langID)?.Name);
                if (!string.IsNullOrEmpty(supportedLanguage.CultureInfo.Name))
                    supportedLangList.Add(
                        new OrderedLanguages
                        {
                            Key = supportedLanguage.CultureInfo.Name,
                            Value = supportedLanguage.CultureInfo.EnglishName
                        }
                    );
            }

            Item itemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);

            foreach (OrderedLanguages pair in supportedLangList)
            {
                Language selectedLanguage = LanguageManager.GetLanguage(pair.Key.ToString());
                try
                {

                    if (selectedLanguage != null && selectedLanguage.CultureInfo != null)
                    {
                        int versionCount = GetVersion(itemFromQueryString.ID, selectedLanguage);
                        string elementBegin = string.Empty;
                        string elementEnd = string.Empty;
                        if (versionCount > 0)
                        {
                            elementBegin = "<span style='color:blue;font-weight:bold'>";
                            elementEnd = "</span>";
                        }
                        this.langNames.Add($"{elementBegin}{selectedLanguage.CultureInfo?.EnglishName} <i>{versionCount} version(s)</i> {elementEnd}", selectedLanguage.Name);//Danish - Key, da - Value
                    }
                }
                catch (ArgumentException ae)
                {
                    Sitecore.Diagnostics.Log.Error(ae.Message, this);
                }
            }
        }

        private int GetVersion(ID itemID, Language language)
        {
            var versionCount = Context.ContentDatabase.GetItem(itemID, language).Versions.Count;
            return versionCount;
        }

        private static Item GetCurrentItem()
        {
            string queryString1 = WebUtil.GetQueryString("db");
            string queryString2 = WebUtil.GetQueryString("id");
            Language language = Language.Parse(WebUtil.GetQueryString("la"));
            Sitecore.Data.Version version = Sitecore.Data.Version.Parse(WebUtil.GetQueryString("vs"));
            Database database = Factory.GetDatabase("master");
            Assert.IsNotNull((object)database, queryString1);
            return database.GetItem(queryString2, language, version);
        }

        private IEnumerable<OrderedLanguages> SortLanguages(Hashtable langHT)
        {
            var orderedKeys = langHT.Keys.Cast<string>().OrderBy(c => c);
            var allKvp = from x in orderedKeys select new OrderedLanguages { Key = x, Value = langHT[x] as string };
            return (allKvp);
        }

        protected override void OnLoad(EventArgs e)
        {
            try
            {
                Assert.ArgumentNotNull(e, "e");
                base.OnLoad(e);
                if (!Context.ClientPage.IsEvent)
                {
                    Item itemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);

                    ListItem child = new ListItem();

                    this.Source.Controls.Add(child);

                    CultureInfo info = new CultureInfo(Context.Request.QueryString["ci"]);
                    child.Header = info.EnglishName;
                    child.Value = info.EnglishName;
                    child.ID = Control.GetUniqueID("I");

                    if (itemFromQueryString == null)
                        throw new Exception();

                    string str = "<script type='text/javascript'>function toggleChkBoxMethod2(formName){alert('checked');var form=$(formName);var i=form.getElements('checkbox'); i.each(function(item){item.checked = !item.checked;});$('togglechkbox').checked = !$('togglechkbox').checked;}</script>";
                    str = str + "<form id=\"ctl15\"><br/><div  style=\"height:500px; overflow:auto;\"><table style=\"width:430px\"><tr></tr><tr></tr>";
                    this.fillLanguageDictionary();
                    foreach (KeyValuePair<string, string> pair in this.langNames)
                    {
                        if (itemFromQueryString.Language.Name != pair.Value)
                        {
                            string str2 = "chk_" + pair.Value;
                            string str4 = str;
                            str = str4 + "<tr><td>" + pair.Key + "</td><td>" + pair.Value + "</td><td><input class='reviewerCheckbox' type='checkbox' value='1' name='" + str2 + "'/></td></tr>";
                        }
                    }
                    str = str + "</table></div></form>";
                    this.TargetLanguages.Text = str;

                    //Options
                    str = "";
                    str += "<table>";
                    str += "<tr><td>Include SubItems:</td><td><input class='optionsCheckbox' type='checkbox' value='1' name='chk_IncludeSubItems'/></td></tr>";
                    str += "</table>";
                    this.Options.Text = str;
                }
            }
            catch (Exception exception)
            {
                Sitecore.Diagnostics.Log.Error(exception.Message, this);
            }
        }

        protected override void OnOK(object sender, EventArgs args)
        {
            Exception exception;
            Item itemFromQueryString = UIUtil.GetItemFromQueryString(Context.ContentDatabase);
            this.fillLanguageDictionary();
            targetLanguagesList = new List<Language>();
            try
            {
                //Get the source language
                if (itemFromQueryString == null)
                    throw new Exception();
                sourceLanguage = itemFromQueryString.Language;
                Sitecore.Diagnostics.Log.Debug("Smart Tools: OnOK-sourceLanguage-" + sourceLanguage.Name, this);

                //Get the target languages
                foreach (KeyValuePair<string, string> pair in this.langNames)
                {
                    if (!string.IsNullOrEmpty(Context.ClientPage.Request.Params.Get("chk_" + pair.Value)))
                    {
                        targetLanguagesList.Add(Sitecore.Data.Managers.LanguageManager.GetLanguage(pair.Value));
                    }
                }

                //Include SubItems?
                if (!string.IsNullOrEmpty(Context.ClientPage.Request.Params.Get("chk_IncludeSubItems")))
                {
                    CopySubItems = true;
                }
                Sitecore.Diagnostics.Log.Debug("Smart Tools: OnOK-CopySubItems-" + CopySubItems.ToString(), this);

                //Execute the process
                if (itemFromQueryString != null && targetLanguagesList.Count > 0 && sourceLanguage != null)
                {
                    //Execute the Job
                    Sitecore.Shell.Applications.Dialogs.ProgressBoxes.ProgressBox.Execute("Add Version and Copy", "Smart Tools", new ProgressBoxMethod(ExecuteOperation), itemFromQueryString);
                }
                else
                {
                    //Show the alert
                    Context.ClientPage.ClientResponse.Alert("Context Item and Target Languages are empty.");
                    Context.ClientPage.ClientResponse.CloseWindow();
                }

                Context.ClientPage.ClientResponse.Alert("Process has been completed.");
                Context.ClientPage.ClientResponse.CloseWindow();
            }
            catch (Exception exception8)
            {
                exception = exception8;
                Sitecore.Diagnostics.Log.Error(exception.Message, this);
                Context.ClientPage.ClientResponse.Alert("Exception Occured. Please check the logs.");
                Context.ClientPage.ClientResponse.CloseWindow();
            }
        }

        protected void ExecuteOperation(params object[] parameters)
        {
            Sitecore.Diagnostics.Log.Debug("Smart Tools: Job Executed.", this);

            if (parameters == null || parameters.Length == 0)
                return;

            Item item = (Item)parameters[0];
            IterateItems(item, targetLanguagesList, sourceLanguage);
        }

        private void IterateItems(Item item, List<Language> targetLanguages, Language sourceLang)
        {
            AddVersionAndCopyItems(item, targetLanguages, sourceLang);

            if (CopySubItems && item.HasChildren)
            {
                foreach (Item childItem in item.Children)
                {
                    IterateItems(childItem, targetLanguages, sourceLang);
                }
            }
        }

        private void AddVersionAndCopyItems(Item item, List<Language> targetLanguages, Language sourceLang)
        {
            foreach (Language language in targetLanguages)
            {
                Item source = Context.ContentDatabase.GetItem(item.ID, sourceLang);
                Item target = Context.ContentDatabase.GetItem(item.ID, language);

                if (source == null || target == null) return;

                Sitecore.Diagnostics.Log.Debug("Smart Tools: AddVersionAndCopyItems-SourcePath-" + source.Paths.Path, this);
                Sitecore.Diagnostics.Log.Debug("Smart Tools: AddVersionAndCopyItemsSourceLanguage-" + sourceLang.Name, this);
                Sitecore.Diagnostics.Log.Debug("Smart Tools: AddVersionAndCopyItems-TargetLanguage-" + language.Name, this);

                source = source.Versions.GetLatestVersion();
                target.Versions.AddVersion();
                target.Editing.BeginEdit();


                source.Fields.ReadAll();
                target.Fields["__Display name"].Value = source.Fields["__Display name"].Value;
                foreach (Field field in source.Fields)
                {
                    if (!field.Name.StartsWith("_")) //(!field.Shared)
                        target[field.ID] = source[field.ID];
                }
                //Copy the data source for the page
                target.Fields["__Final Renderings"].Value = source.Fields["__Final Renderings"].Value;
                Sitecore.Diagnostics.Log.Info($"Source Final renderings: {source.Fields["__Final Renderings"]}", this);
                Sitecore.Diagnostics.Log.Info($"Target Final renderings: {target.Fields["__Final Renderings"]}", this);
                target.Editing.EndEdit();

                Sitecore.Diagnostics.Log.Debug("Smart Tools: AddVersionAndCopyItems-Completed.", this);
            }
        }
    }
    public class OrderedLanguages
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}

