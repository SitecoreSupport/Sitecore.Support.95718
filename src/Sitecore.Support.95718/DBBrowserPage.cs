// Sitecore.Support.Admin.DBBrowser.DBBrowserPage
using Sitecore;
using Sitecore.Collections;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.Diagnostics;
using Sitecore.Globalization;
using Sitecore.Publishing;
using Sitecore.Resources;
using Sitecore.Security.Accounts;
using Sitecore.Sites;
using Sitecore.StringExtensions;
using Sitecore.Tasks;
using Sitecore.Web;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;

namespace Sitecore.Support.Admin.DBBrowser
{
  public class DBBrowserPage : Page
  {
    private Database _database;

    private string _db;

    private int _displayXml = -1;

    private string _displayXmlKey = "DBBROWSER_XML";

    private Item _item;

    private Language _language;

    private LanguageCollection _languages;

    private string _protectedBgColor = "#F0F0F0";

    private string _protectedFgColor = "#FF0000";

    private bool _showIcons = true;

    private Sitecore.Data.Version _version = Sitecore.Data.Version.Latest;

    protected HtmlGenericControl contentEditor;

    protected HtmlGenericControl dataBases;

    protected HtmlGenericControl tree;

    private bool DisplayXml
    {
      get
      {
        if (_displayXml < 0)
        {
          string text = Session[_displayXmlKey] as string;
          if (text != null)
          {
            _displayXml = int.Parse(text);
          }
          else
          {
            _displayXml = 0;
          }
        }
        return _displayXml == 1;
      }
      set
      {
        _displayXml = (value ? 1 : 0);
        Session[_displayXmlKey] = _displayXml.ToString();
      }
    }

    private LanguageCollection Languages
    {
      get
      {
        if (_languages == null)
        {
          _languages = LanguageManager.GetLanguages(_database);
          if (_languages == null)
          {
            _languages = new LanguageCollection();
          }
        }
        return _languages;
      }
    }

    private void AddBaseTemplateList(HtmlGenericControl div)
    {
      if (!Sitecore.Data.ID.IsNullOrEmpty(_item.TemplateID))
      {
        Template template = TemplateManager.GetTemplate(_item);
        if (template != null)
        {
          div.Controls.Add(GetSpan("&nbsp;&nbsp;:&nbsp;&nbsp;"));
          IEnumerable<Template> enumerator = MainUtil.GetEnumerator(template, template.GetBaseTemplates());
          bool flag = true;
          foreach (Template item in enumerator)
          {
            if (!flag)
            {
              div.Controls.Add(GetSpan(",&nbsp;"));
            }
            flag = false;
            HtmlAnchor anchor = GetAnchor(item.Name, item.ID, _language);
            anchor.Attributes["class"] = "ItemPathTemplate";
            div.Controls.Add(anchor);
            if (item.ID == TemplateIDs.StandardTemplate)
            {
              break;
            }
          }
        }
      }
    }

    private void AddBranches(Item root, ItemList result)
    {
      foreach (Item child in root.GetChildren(ChildListOptions.None))
      {
        if (child.TemplateID == TemplateIDs.BranchTemplate)
        {
          result.Add(child);
        }
        else if (child.TemplateID == TemplateIDs.BranchTemplateFolder)
        {
          AddBranches(child, result);
        }
      }
    }

    private void AddFromBranch(string name, string branch)
    {
      Sitecore.Diagnostics.Error.Assert(name != null && name.Length > 0, "Please enter a name for the new item before pressing the 'Add child' button");
      Sitecore.Diagnostics.Error.Assert(branch != null && branch.Length > 0, "Please select a branch from the list before pressing the 'Add child' button");
      ID templateId = Sitecore.Data.ID.Parse(branch);
      _item = ItemManager.AddFromTemplate(name, templateId, _item);
    }

    private void AddFromTemplate(string name, string template)
    {
      Sitecore.Diagnostics.Error.Assert(name != null && name.Length > 0, "Please enter a name for the new item before pressing the 'Add child' button");
      Sitecore.Diagnostics.Error.Assert(template != null && template.Length > 0, "Please select a template from the list before pressing the 'Add child' button");
      ID templateId = Sitecore.Data.ID.Parse(template);
      _item = ItemManager.AddFromTemplate(name, templateId, _item);
    }

    private void AddLine(HtmlTable table)
    {
      HtmlUtil.AddRow(table, "<div class=\"Edge\">" + Images.GetSpacer(1, 1) + "</div>").Cells[0].ColSpan = 2;
    }

    private void AddRow(HtmlTable table, string label, string value)
    {
      HtmlTableRow htmlTableRow = HtmlUtil.AddRow(table, label, value);
      htmlTableRow.Cells[0].Attributes["class"] = "FieldReadOnlyLabel";
      htmlTableRow.Cells[1].Attributes["class"] = "FieldReadOnlyInput";
      htmlTableRow.Cells[1].Attributes["colspan"] = "2";
    }

    private void AddRow(HtmlTable table, HtmlControl label, HtmlControl value)
    {
      HtmlTableRow htmlTableRow = HtmlUtil.AddRow(table, label, value);
      htmlTableRow.Cells[0].Attributes["class"] = "FieldReadOnlyLabel";
      htmlTableRow.Cells[1].Attributes["class"] = "FieldReadOnlyInput";
      htmlTableRow.Cells[1].Attributes["colspan"] = "2";
    }

    private static HtmlInputButton AddToolbutton(HtmlGenericControl div, string name, string caption, string accesskey)
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      div.Controls.Add(htmlInputButton);
      htmlInputButton.ID = name;
      htmlInputButton.Name = name;
      htmlInputButton.Attributes["class"] = "Toolbutton";
      htmlInputButton.Value = caption;
      if (accesskey.Length > 0)
      {
        htmlInputButton.Attributes["accesskey"] = accesskey;
      }
      return htmlInputButton;
    }

    private void AddTreeChildren(Item current, Item item, HtmlGenericControl cell, int level, SafeDictionary<ID, string> ancestors)
    {
      foreach (Item child in current.Children)
      {
        AddTreeNode(child, cell, item, level);
        if (ancestors.ContainsKey(child.ID))
        {
          AddTreeChildren(child, item, cell, level + 1, ancestors);
        }
      }
    }

    private void AddTreeNode(Item child, HtmlGenericControl cell, Item item, int level)
    {
      HtmlGenericControl htmlGenericControl = new HtmlGenericControl("div");
      htmlGenericControl.Attributes["class"] = "TreeNodeRow";
      htmlGenericControl.Style["margin-left"] = level * 16 + 4 + "px";
      HtmlAnchor anchor = GetAnchor(child.Name, child.ID, _db);
      if (item.ID == child.ID)
      {
        anchor.Attributes["class"] = "TreeNodeSelected";
      }
      else
      {
        anchor.Attributes["class"] = "TreeNode";
      }
      if (child.RuntimeSettings.IsVirtual)
      {
        anchor.Attributes["style"] = "color:blue;";
      }
      if (_showIcons)
      {
        string iconImage = ThemeManager.GetIconImage(child, 16, 16, "absmiddle", "0px 2px 0px 0px");
        htmlGenericControl.Controls.Add(new LiteralControl(iconImage));
      }
      htmlGenericControl.Controls.Add(anchor);
      cell.Controls.Add(htmlGenericControl);
    }

    private void AddVersion()
    {
      _item.Versions.AddVersion();
    }

    private bool CheckSecurity()
    {
      User user = Sitecore.Context.User;
      if ((Account)user != (Account)null && user.IsAdministrator)
      {
        return true;
      }
      SiteContext site = Sitecore.Context.Site;
      string text = (site != null) ? site.LoginPage : "";
      if (text.Length > 0)
      {
        base.Response.Redirect(text, true);
      }
      return false;
    }

    private void CopyItem(string targetPath)
    {
      Sitecore.Diagnostics.Error.Assert(targetPath != null && targetPath.Length > 0, "Please enter a target path before pressing the 'Copy to' button");
      Item item = (!(targetPath == ".")) ? _database.Items[targetPath] : _item.Parent;
      Sitecore.Diagnostics.Error.Assert(item != null, "Could not find target item: " + targetPath);
      _item = _item.CopyTo(item, "Copy of " + _item.Name);
    }

    public static HtmlAnchor CreateAnchor(string text, string href)
    {
      HtmlAnchor htmlAnchor = new HtmlAnchor();
      htmlAnchor.InnerHtml = text;
      htmlAnchor.HRef = href;
      return htmlAnchor;
    }

    private void DeleteChildren()
    {
      _item.DeleteChildren();
    }

    private void DeleteItem()
    {
      Item parent = _item.Parent;
      _item.Delete();
      _item = parent;
    }

    private void DuplicateItem()
    {
      _item = _item.Duplicate();
    }

    private HtmlInputButton GetAddButton()
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_addFromTemplate";
      htmlInputButton.Value = "Add child";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      return htmlInputButton2;
    }

    private HtmlInputText GetAddName()
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_name";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      return htmlInputText2;
    }

    private HtmlAnchor GetAnchor(string text, ID itemID)
    {
      return GetAnchor(text, itemID, _language);
    }

    private HtmlAnchor GetAnchor(string text, string database)
    {
      return CreateAnchor(text, "?db=" + database + "&lang=" + _language);
    }

    private HtmlAnchor GetAnchor(string text, ID itemID, Language language)
    {
      return CreateAnchor(text, "?db=" + _db + "&lang=" + language + "&id=" + itemID);
    }

    private HtmlAnchor GetAnchor(string text, ID itemID, string database)
    {
      return CreateAnchor(text, "?db=" + database + "&lang=" + _language + "&id=" + itemID);
    }

    private HtmlAnchor GetAnchor(string text, ID itemID, Language language, Sitecore.Data.Version version)
    {
      return CreateAnchor(text, "?db=" + _db + "&lang=" + language + "&ver=" + version + "&id=" + itemID);
    }

    private ItemList GetBranches(Item root)
    {
      ItemList result = new ItemList();
      AddBranches(root, result);
      return result;
    }

    private HtmlSelect GetBranchSelector()
    {
      HtmlSelect htmlSelect = new HtmlSelect();
      htmlSelect.ID = "c_branch";
      HtmlSelect htmlSelect2 = htmlSelect;
      htmlSelect2.Style["width"] = "100%";
      htmlSelect2.Attributes["class"] = "TaskInput";
      htmlSelect2.Items.Add(new ListItem("[select a branch]", ""));
      Item item = ItemManager.GetItem(ItemIDs.BranchesRoot, Language.Invariant, Sitecore.Data.Version.Latest, _database);
      if (item != null)
      {
        SortedList sortedList = new SortedList(StringComparer.Ordinal);
        ItemList branches = GetBranches(item);
        int num = 0;
        foreach (Item item3 in branches)
        {
          string text = item3.Name + num;
          if (text[0] == '_')
          {
            text = "ZZ" + text;
          }
          int num2 = 1;
          string text2 = text;
          while (sortedList.ContainsKey(text))
          {
            text = text2 + '(' + num2 + ')';
            num2++;
          }
          sortedList[text] = item3;
          num++;
        }
        ID branchId = _item.BranchId;
        {
          foreach (Item value in sortedList.Values)
          {
            ListItem listItem = new ListItem(StringUtil.RemovePrefix("/sitecore/templates/branches/", value.Paths.Path), value.ID.ToString());
            if (value.ID == branchId)
            {
              listItem.Selected = true;
            }
            htmlSelect2.Items.Add(listItem);
          }
          return htmlSelect2;
        }
      }
      return htmlSelect2;
    }

    private string GetBranchText(Item item)
    {
      ItemDefinition itemDefinition = _database.DataManager.DataSource.GetItemDefinition(item.BranchId);
      if (itemDefinition != null)
      {
        return itemDefinition.Name + " (" + itemDefinition.ID + ")";
      }
      return item.BranchId.ToString();
    }

    private HtmlControl GetButtonBar()
    {
      string caption = (Sitecore.Context.ProxiesActive ? "Deactivate" : "Activate") + " proxies";
      HtmlGenericControl div = GetDiv();
      AddToolbutton(div, "btn_save", "Save", "s");
      AddToolbutton(div, "btn_duplicate", "Duplicate", "");
      AddToolbutton(div, "btn_delete", "Delete", "");
      AddToolbutton(div, "btn_deleteChildren", "Delete children", "");
      AddToolbutton(div, "btn_addVersion", "Add version", "");
      AddToolbutton(div, "btn_removeVersion", "Delete version", "");
      AddToolbutton(div, "btn_shadows", caption, "");
      return div;
    }

    private HtmlInputButton GetCopyButton()
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_copy";
      htmlInputButton.Value = "Copy to";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      return htmlInputButton2;
    }

    private HtmlInputText GetCopyTarget(string path)
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_copyTarget";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      htmlInputText2.Value = path;
      return htmlInputText2;
    }

    private HtmlGenericControl GetDiv()
    {
      return new HtmlGenericControl("div");
    }

    private HtmlGenericControl GetDiv(Control child)
    {
      HtmlGenericControl htmlGenericControl = new HtmlGenericControl("div");
      htmlGenericControl.Style["width"] = "100%";
      htmlGenericControl.Controls.Add(child);
      return htmlGenericControl;
    }

    private HtmlContainerControl GetFieldCaption(string fieldTitle, string fieldValue, ID fieldId)
    {
      Item item = null;
      ID iD = null;//C# 6.0 out declaration not available
      if (Sitecore.Data.ID.TryParse(fieldValue, out iD))
      {
        item = _database.GetItem(iD, _language);
      }
      bool flag = !Sitecore.Data.ID.IsNullOrEmpty(fieldId);
      string text = flag ? "" : fieldTitle;
      HtmlGenericControl span = GetSpan(text, false);
      if (flag)
      {
        HtmlAnchor anchor = GetAnchor(fieldTitle, fieldId);
        anchor.Attributes["class"] = "ItemPathTemplate";
        anchor.Style["color"] = "black";
        anchor.Style["font-weight"] = "normal";
        span.Controls.Add(anchor);
      }
      if (item != null)
      {
        string text2 = "<span style='color:#6375D6'>(" + item.Name + ")</span>";
        HtmlAnchor anchor2 = GetAnchor(text2, iD);
        span.Controls.Add(anchor2);
      }
      return span;
    }

    private HtmlInputHidden GetHiddenValue(string fieldValue, string formFieldId)
    {
      HtmlInputHidden htmlInputHidden = new HtmlInputHidden();
      htmlInputHidden.Value = fieldValue;
      htmlInputHidden.ID = "org_" + formFieldId;
      return htmlInputHidden;
    }

    private HtmlControl GetItemPath()
    {
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      HtmlTableRow htmlTableRow = HtmlUtil.AddRow(htmlTable2, "", "", "");
      htmlTableRow.Cells[0].VAlign = "bottom";
      htmlTableRow.Cells[1].Attributes["class"] = "ItemPathDivider";
      htmlTableRow.Cells[1].NoWrap = true;
      htmlTableRow.Cells[1].Align = "right";
      HtmlGenericControl div = GetDiv();
      htmlTableRow.Cells[0].Controls.Add(div);
      Item item = _item;
      string iconImage = ThemeManager.GetIconImage(item, 24, 24, "absmiddle", "0px 4px 0px 0px");
      div.Controls.Add(new LiteralControl(iconImage));
      while (item != null)
      {
        div.Controls.AddAt(1, GetSpan("/"));
        HtmlAnchor anchor = GetAnchor(item.Name, item.ID, _language);
        anchor.Attributes["class"] = "ItemPathFragment";
        div.Controls.AddAt(2, anchor);
        item = ItemManager.GetParent(item);
      }
      AddBaseTemplateList(div);
      if (_item.Appearance.ReadOnly)
      {
        HtmlGenericControl span = GetSpan("&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;[protected]");
        span.Style["color"] = _protectedFgColor;
        div.Controls.Add(span);
      }
      LanguageCollection languages = Languages;
      HtmlTableCell htmlTableCell = htmlTableRow.Cells[1];
      foreach (Language item2 in languages)
      {
        htmlTableCell.Controls.Add(GetSpan("&nbsp;"));
        HtmlAnchor anchor2 = GetAnchor(item2.Name, _item.ID, item2);
        anchor2.Attributes["class"] = ((item2 == _item.Language) ? "AnchorSelected" : "Anchor");
        htmlTableCell.Controls.Add(anchor2);
      }
      VersionCollection versions = ItemManager.GetVersions(_item);
      if (versions != null)
      {
        htmlTableCell = htmlTableRow.Cells[1];
        {
          foreach (Sitecore.Data.Version item3 in versions)
          {
            htmlTableCell.Controls.Add(GetSpan("&nbsp;"));
            HtmlAnchor anchor3 = GetAnchor(item3.ToString(), _item.ID, _language, item3);
            anchor3.Attributes["class"] = ((item3 == _item.Version) ? "AnchorSelected" : "Anchor");
            htmlTableCell.Controls.Add(anchor3);
          }
          return htmlTable2;
        }
      }
      return htmlTable2;
    }

    private HtmlControl GetLanguageControl()
    {
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Border = 0;
      htmlTable.Width = "100%";
      htmlTable.CellPadding = 0;
      htmlTable.CellSpacing = 0;
      HtmlTable htmlTable2 = htmlTable;
      string languageImage = ThemeManager.GetLanguageImage(_language, _database, 16, 16);
      HtmlTableCell htmlTableCell = HtmlUtil.AddRow(htmlTable2, languageImage, "").Cells[1];
      htmlTableCell.Align = "right";
      foreach (Language language in Languages)
      {
        htmlTableCell.Controls.Add(GetSpan("&nbsp;"));
        HtmlAnchor anchor = GetAnchor(language.Name, _item.ID, language);
        anchor.Style["color"] = "blue";
        anchor.Style["text-decoration"] = "underline";
        htmlTableCell.Controls.Add(anchor);
      }
      return htmlTable2;
    }

    private HtmlTextArea GetMemoBox(string fieldValue, string shortId)
    {
      HtmlTextArea htmlTextArea = new HtmlTextArea();
      htmlTextArea.Value = fieldValue;
      htmlTextArea.ID = "fld_" + shortId;
      HtmlTextArea htmlTextArea2 = htmlTextArea;
      htmlTextArea2.Attributes["class"] = "FieldTextBox";
      htmlTextArea2.Style["width"] = "100%";
      htmlTextArea2.Style["height"] = "80";
      htmlTextArea2.Attributes["Wrap"] = "soft";
      if (_item.Appearance.ReadOnly)
      {
        htmlTextArea2.Style["background-color"] = _protectedBgColor;
      }
      return htmlTextArea2;
    }

    private SafeDictionary<string, string> GetMemoTypes()
    {
      SafeDictionary<string, string> safeDictionary = new SafeDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
      safeDictionary.Add("html", string.Empty);
      safeDictionary.Add("memo", string.Empty);
      safeDictionary.Add("rich text", string.Empty);
      safeDictionary.Add("security", string.Empty);
      safeDictionary.Add("layout", string.Empty);
      safeDictionary.Add("multilist", string.Empty);
      return safeDictionary;
    }

    private HtmlInputButton GetMoveButton()
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_move";
      htmlInputButton.Value = "Move to";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      return htmlInputButton2;
    }

    private HtmlInputText GetMoveTarget(string path)
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_moveTarget";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      htmlInputText2.Value = path;
      return htmlInputText2;
    }

    private HtmlInputText GetPasteXmlBox()
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_pasteXmlBox";
      htmlInputText.Value = "";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      htmlInputText2.Style["width"] = "150";
      return htmlInputText2;
    }

    private HtmlInputButton GetPasteXmlButton()
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_pasteXml";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      htmlInputButton2.Value = "Paste xml";
      return htmlInputButton2;
    }

    private HtmlInputButton GetRenameButton()
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_rename";
      htmlInputButton.Value = "Rename";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      return htmlInputButton2;
    }

    private HtmlInputText GetRenameName()
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_renameName";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      htmlInputText2.Value = _item.Name;
      return htmlInputText2;
    }

    private HtmlGenericControl GetSpan(string text)
    {
      return GetSpan(text, false);
    }

    private HtmlGenericControl GetSpan(string text, bool encode)
    {
      HtmlGenericControl htmlGenericControl = new HtmlGenericControl("span");
      if (encode)
      {
        text = HttpUtility.HtmlEncode(text);
      }
      htmlGenericControl.InnerHtml = text;
      return htmlGenericControl;
    }

    private HtmlContainerControl GetTask(string caption, out HtmlContainerControl panel)
    {
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Attributes["class"] = "Taskbox";
      HtmlUtil.AddRow(htmlTable, caption).Cells[0].Attributes["class"] = "TaskCaption";
      HtmlTableRow htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].Attributes["class"] = "TaskBody";
      panel = htmlTableRow.Cells[0];
      HtmlUtil.AddRow(htmlTable, Images.GetSpacer(1, 1)).Cells[0].Attributes["class"] = "TaskFooter";
      return htmlTable;
    }

    private HtmlSelect GetTemplateSelector()
    {
      HtmlSelect htmlSelect = new HtmlSelect();
      htmlSelect.ID = "c_template";
      HtmlSelect htmlSelect2 = htmlSelect;
      htmlSelect2.Style["width"] = "100%";
      htmlSelect2.Attributes["class"] = "TaskInput";
      htmlSelect2.Items.Add(new ListItem("[select a template]", ""));
      SortedList sortedList = new SortedList(StringComparer.Ordinal);
      TemplateDictionary templates = TemplateManager.GetTemplates(_database);
      int num = 0;
      lock (templates.SyncRoot)
      {
        foreach (Template value in templates.Values)
        {
          string text = value.FullName + num;
          if (text[0] == '_')
          {
            text = "ZZ" + text;
          }
          int num2 = 1;
          string text2 = text;
          while (sortedList.ContainsKey(text))
          {
            text = text2 + '(' + num2 + ')';
            num2++;
          }
          sortedList[text] = value;
          num++;
        }
      }
      ID templateID = _item.TemplateID;
      Item item = _item.Children[0];
      if (item != null)
      {
        templateID = item.TemplateID;
      }
      foreach (Template value2 in sortedList.Values)
      {
        ListItem listItem = new ListItem(value2.FullName, value2.ID.ToString());
        if (value2.ID == templateID)
        {
          listItem.Selected = true;
        }
        htmlSelect2.Items.Add(listItem);
      }
      return htmlSelect2;
    }

    private string GetTemplateText(Item item)
    {
      Template template = TemplateManager.GetTemplate(item);
      if (template != null)
      {
        return template.Name + " (" + template.ID + ")";
      }
      return item.TemplateID.ToString();
    }

    private HtmlInputText GetTextBox(string fieldValue, string shortId)
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.Value = fieldValue;
      htmlInputText.ID = "fld_" + shortId;
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "FieldTextBox";
      htmlInputText2.Style["width"] = "100%";
      if (_item.Appearance.ReadOnly)
      {
        htmlInputText2.Style["background-color"] = _protectedBgColor;
      }
      return htmlInputText2;
    }

    private HtmlControl GetVersionControl(Item item)
    {
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Border = 0;
      htmlTable.Width = "100%";
      htmlTable.CellPadding = 0;
      htmlTable.CellSpacing = 0;
      HtmlTable htmlTable2 = htmlTable;
      HtmlTableCell htmlTableCell = HtmlUtil.AddRow(htmlTable2, item.Version.ToString(), "").Cells[1];
      htmlTableCell.Align = "right";
      VersionCollection versions = ItemManager.GetVersions(item);
      if (versions != null)
      {
        {
          foreach (Sitecore.Data.Version item2 in versions)
          {
            htmlTableCell.Controls.Add(GetSpan("&nbsp;"));
            HtmlAnchor anchor = GetAnchor(item2.ToString(), item.ID, _language, item2);
            anchor.Style["color"] = "blue";
            anchor.Style["text-decoration"] = "underline";
            htmlTableCell.Controls.Add(anchor);
          }
          return htmlTable2;
        }
      }
      return htmlTable2;
    }

    private HtmlInputText GetXmlBox()
    {
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_xmlBox";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      htmlInputText2.Style["width"] = "150";
      htmlInputText2.Value = string.Empty;
      if (DisplayXml)
      {
        ItemSerializerOptions defaultOptions = ItemSerializerOptions.GetDefaultOptions();
        defaultOptions.ProcessChildren = true;
        htmlInputText2.Value = _item.GetOuterXml(defaultOptions);
      }
      return htmlInputText2;
    }

    private HtmlInputButton GetXmlButton()
    {
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_xml";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      if (DisplayXml)
      {
        htmlInputButton2.Value = "Hide xml";
        return htmlInputButton2;
      }
      htmlInputButton2.Value = "Show xml";
      return htmlInputButton2;
    }

    private void HandleFormPost()
    {
      NameValueCollection form = base.Request.Form;
      if (form["btn_save"] != null)
      {
        SaveItem();
      }
      else if (form["c_addFromTemplate"] != null)
      {
        AddFromTemplate(form["c_name"], form["c_template"]);
      }
      else if (form["c_addFromBranch"] != null)
      {
        AddFromBranch(form["c_fromBranchName"], form["c_branch"]);
      }
      else if (form["btn_delete"] != null)
      {
        DeleteItem();
      }
      else if (form["btn_deleteChildren"] != null)
      {
        DeleteChildren();
      }
      else if (form["btn_addVersion"] != null)
      {
        AddVersion();
      }
      else if (form["btn_removeVersion"] != null)
      {
        RemoveVersion();
        _version = Sitecore.Data.Version.Latest;
      }
      else if (form["btn_duplicate"] != null)
      {
        DuplicateItem();
      }
      else if (form["btn_shadows"] != null)
      {
        Sitecore.Context.ProxiesActive = !Sitecore.Context.ProxiesActive;
      }
      else if (form["c_move"] != null)
      {
        MoveItem(form["c_moveTarget"]);
      }
      else if (form["c_copy"] != null)
      {
        CopyItem(form["c_copyTarget"]);
      }
      else if (form["c_rename"] != null)
      {
        RenameItem(form["c_renameName"]);
      }
      else if (form["c_xml"] != null)
      {
        DisplayXml = !DisplayXml;
      }
      else if (form["c_pasteXml"] != null)
      {
        PasteXml(form["c_pasteXmlBox"], IsChecked(form["c_changeIDs"]));
      }
      else if (form["c_publishAll"] != null)
      {
        Publish(PublishMode.Full, form["c_publishTarget"]);
      }
      else if (form["c_publishChanged"] != null)
      {
        Publish(PublishMode.Incremental, form["c_publishTarget"]);
      }
      else
      {
        foreach (string key in base.Request.Form.Keys)
        {
          string text2 = "btn_resetField";
          if (key.StartsWith(text2, StringComparison.InvariantCulture))
          {
            ResetField(key.Substring(text2.Length));
          }
        }
      }
      Reload();
    }

    private void InitializeComponent()
    {
      base.Load += Page_Load;
    }

    private bool IsChecked(string checkboxValue)
    {
      return StringUtil.GetString(checkboxValue) == "on";
    }

    private void MoveItem(string targetPath)
    {
      Sitecore.Diagnostics.Error.Assert(targetPath != null && targetPath.Length > 0, "Please enter a target path before pressing the 'Move to' button");
      Item item = null;
      if (targetPath == "..")
      {
        if (_item.Parent != null)
        {
          item = _item.Parent.Parent;
        }
      }
      else
      {
        item = _database.Items[targetPath];
      }
      Sitecore.Diagnostics.Error.Assert(item != null, "Could not find target item: " + targetPath);
      _item.MoveTo(item);
    }

    protected override void OnInit(EventArgs e)
    {
      InitializeComponent();
      base.OnInit(e);
    }

    private void Page_Load(object sender, EventArgs e)
    {
      if (CheckSecurity())
      {
        new HighResTimer().Start();
        using (new TaskContext("DBBrowser"))
        {
          string queryString = WebUtil.GetQueryString("id");
          _db = WebUtil.GetQueryString("db", "master");
          _language = Language.Parse(WebUtil.GetQueryString("lang", LanguageManager.DefaultLanguage.ToString()));
          string queryString2 = WebUtil.GetQueryString("ver");
          if (queryString2.Length > 0)
          {
            _version = Sitecore.Data.Version.Parse(queryString2);
          }
          _database = Factory.GetDatabase(_db);
          if (queryString.Length > 0)
          {
            _item = _database.Items[Sitecore.Data.ID.Parse(queryString), _language, _version];
          }
          if (_item == null)
          {
            _item = (_database.GetItem("/sitecore/content/home", _language) ?? _database.GetRootItem(_language));
          }
          if (base.Request.Form.Count > 0)
          {
            HandleFormPost();
          }
          string[] databaseNames = Factory.GetDatabaseNames();
          foreach (string text in databaseNames)
          {
            string queryString3 = WebUtil.GetQueryString("id");
            ID itemID = queryString3.IsNullOrEmpty() ? _item.ID : Sitecore.Data.ID.Parse(queryString3);
            HtmlAnchor htmlAnchor = (_item != null) ? GetAnchor(text, itemID, text) : GetAnchor(text, text);
            htmlAnchor.Attributes["class"] = ((text == _database.Name) ? "DatabaseLinkSelected" : "DatabaseLink");
            dataBases.Controls.Add(htmlAnchor);
          }
          if (_item != null)
          {
            ShowTree(_item, tree);
            ShowFields(_item, contentEditor);
          }
        }
      }
    }

    private void PasteXml(string xml, bool changeIDs)
    {
      _item.Paste(xml, changeIDs, PasteMode.Overwrite);
    }

    private void Publish(PublishMode mode, string target)
    {
      if (Settings.Publishing.Enabled)
      {
        foreach (Language language in Languages)
        {
          Database database = _item.Database;
          Database database2 = Factory.GetDatabase(target);
          Assert.IsNotNull(database2, "Unknown datbase: {0}", target);
          PublishOptions options = new PublishOptions(database, database2, mode, language, DateTime.Now);
          new Publisher(options).Publish();
        }
      }
    }

    private void Reload()
    {
      string text = (_item != null) ? ("&id=" + _item.ID) : "";
      string url = base.Request.FilePath + "?db=" + _db + "&lang=" + _language + "&ver=" + _version + text;
      base.Response.Redirect(url);
    }

    private void RemoveVersion()
    {
      _item.Versions.RemoveVersion();
    }

    private void RenameItem(string name)
    {
      Sitecore.Diagnostics.Error.Assert(name != null && name.Length > 0, "Please enter a new name before pressing the 'Rename' button");
      _item.Editing.BeginEdit();
      _item.Name = name;
      _item.Editing.EndEdit();
    }

    private void ResetField(string shortId)
    {
      ID fieldID = ShortID.DecodeID(shortId);
      Field field = _item.Fields[fieldID];
      if (field != null)
      {
        using (new EditContext(_item))
        {
          field.Reset();
        }
      }
    }

    private void SaveItem()
    {
      _item.Editing.BeginEdit();
      foreach (string key in base.Request.Form.Keys)
      {
        if (key.StartsWith("fld_", StringComparison.InvariantCulture))
        {
          string str = key.Substring(4);
          ID fieldID = ShortID.DecodeID(key.Substring(4));
          string text2 = base.Request.Form[key];
          string b = base.Request.Form["org_" + str];
          if (text2 != b)
          {
            ((BaseItem)_item)[fieldID] = text2;
          }
        }
      }
      _item.Editing.EndEdit();
    }

    private void ShowActions(HtmlGenericControl control)
    {
      HtmlGenericControl htmlGenericControl = new HtmlGenericControl("div");
      htmlGenericControl.Attributes["class"] = "TaskScroller";
      control.Controls.Add(htmlGenericControl);
      HtmlTable htmlTable = new HtmlTable();
      htmlGenericControl.Controls.Add(htmlTable);
      htmlTable.Border = 0;
      htmlTable.Width = "100%";
      htmlTable.Height = "100%";
      htmlTable.Attributes["class"] = "Tasks";
      htmlTable.CellSpacing = 0;
      htmlTable.CellPadding = 0;
      HtmlTableRow htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowRename(htmlTableRow.Cells[0]);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowAddFromTemplate(htmlTableRow.Cells[0]);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowAddFromBranch(htmlTableRow.Cells[0]);
      string path = _item.Paths.Path;
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowMove(htmlTableRow.Cells[0], path);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowCopy(htmlTableRow.Cells[0], path);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowXml(htmlTableRow.Cells[0]);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowPasteXml(htmlTableRow.Cells[0]);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, "");
      htmlTableRow.Cells[0].NoWrap = true;
      ShowPublish(htmlTableRow.Cells[0]);
    }

    private void ShowAddFromBranch(HtmlTableCell owner)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Add from Branch", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Add item to \"" + _item.Name + "\"?").Cells[0].NoWrap = true;
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_fromBranchName";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      HtmlUtil.AddRow(htmlTable2, GetDiv(htmlInputText2));
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetDiv(GetBranchSelector())));
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_addFromBranch";
      htmlInputButton.Value = "Add child";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      HtmlGenericControl div = GetDiv(htmlInputButton2);
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowAddFromTemplate(HtmlTableCell owner)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Add from Template", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Add item to \"" + _item.Name + "\"?").Cells[0].NoWrap = true;
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetAddName()));
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetDiv(GetTemplateSelector())));
      HtmlGenericControl div = GetDiv(GetAddButton());
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowCopy(HtmlTableCell owner, string path)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Copy", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Copy \"" + _item.Name + "\"?").Cells[0].NoWrap = true;
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetCopyTarget(path)));
      HtmlGenericControl div = GetDiv(GetCopyButton());
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowFields(Item item, HtmlGenericControl control)
    {
      HtmlTable htmlTable = new HtmlTable();
      control.Controls.Add(htmlTable);
      htmlTable.Border = 0;
      htmlTable.CellPadding = 0;
      htmlTable.CellSpacing = 0;
      htmlTable.Width = "100%";
      HtmlTableRow htmlTableRow = HtmlUtil.AddRow(htmlTable, GetButtonBar());
      htmlTableRow.Cells[0].ColSpan = 2;
      htmlTableRow.Cells[0].VAlign = "bottom";
      htmlTableRow.Cells[0].Attributes["class"] = "ButtonBar";
      AddLine(htmlTable);
      htmlTableRow = HtmlUtil.AddRow(htmlTable, GetItemPath());
      htmlTableRow.Cells[0].ColSpan = 2;
      htmlTableRow.Cells[0].VAlign = "bottom";
      htmlTableRow.Cells[0].Attributes["class"] = "ItemPath";
      AddLine(htmlTable);
      HtmlGenericControl htmlGenericControl = new HtmlGenericControl("div");
      control.Controls.Add(htmlGenericControl);
      htmlGenericControl.Attributes["class"] = "fields";
      HtmlGenericControl htmlGenericControl2 = new HtmlGenericControl("div");
      htmlGenericControl2.Attributes["class"] = "FieldsScroller";
      htmlGenericControl.Controls.Add(htmlGenericControl2);
      HtmlTable htmlTable2 = new HtmlTable();
      htmlGenericControl2.Controls.Add(htmlTable2);
      htmlTable2.Border = 0;
      htmlTable2.CellPadding = 0;
      htmlTable2.CellSpacing = 0;
      htmlTable2.Width = "100%";
      ShowActions(htmlGenericControl);
      item.Fields.ReadAll();
      item.Fields.Sort();
      string b = null;
      SafeDictionary<string, string> memoTypes = GetMemoTypes();
      foreach (Field field in item.Fields)
      {
        if (field.Name.Length != 0)
        {
          string value = field.Value;
          string section = field.Section;
          string type = field.Type;
          string fieldTitle = HttpUtility.HtmlEncode(field.DisplayName);
          string text = ShortID.Encode(field.ID);
          bool containsStandardValue = field.ContainsStandardValue;
          bool flag = field.GetValue(false, false) == null;
          if (section != b)
          {
            htmlTableRow = HtmlUtil.AddRow(htmlTable2, section);
            htmlTableRow.Cells[0].Attributes["class"] = "FieldSection";
            htmlTableRow.Cells[0].ColSpan = 3;
            b = section;
          }
          HtmlControl fieldCaption = GetFieldCaption(fieldTitle, value, field.ID);
          HtmlGenericControl div = GetDiv();
          if (flag)
          {
            div.InnerHtml = "null&nbsp;";
            div.Style["color"] = "#716F64";
            div.Style["font-size"] = "10px";
          }
          else
          {
            div.Attributes["title"] = "Reset field";
            AddToolbutton(div, "btn_resetField" + text, "x", "").Style["color"] = "#6375D6";
          }
          HtmlControl htmlControl;
          if (memoTypes.ContainsKey(type))
          {
            htmlControl = GetMemoBox(value, text);
            htmlTableRow = HtmlUtil.AddRow(htmlTable2, fieldCaption, htmlControl, div);
          }
          else
          {
            htmlControl = GetTextBox(value, text);
            htmlTableRow = HtmlUtil.AddRow(htmlTable2, fieldCaption, htmlControl, div);
          }
          if (containsStandardValue)
          {
            AttributeCollection attributes;
            (attributes = htmlControl.Attributes)["style"] = attributes["style"] + ";color:#95B8E1;";
          }
          htmlTableRow.Cells[0].Attributes["class"] = "FieldLabel";
          htmlTableRow.Cells[0].VAlign = "top";
          htmlTableRow.Cells[1].Attributes["class"] = "FieldInput";
          htmlTableRow.Cells[1].VAlign = "top";
          htmlTableRow.Cells[1].Controls.Add(GetHiddenValue(value, text));
          if (_item.Appearance.ReadOnly)
          {
            htmlTableRow.Cells[1].Style["background-color"] = _protectedBgColor;
          }
          htmlTableRow.Cells[2].Attributes["class"] = "FieldLabel";
          htmlTableRow.Cells[2].VAlign = "top";
          htmlTableRow.Cells[2].Width = "4px";
        }
      }
      htmlTableRow = HtmlUtil.AddRow(htmlTable2, "System");
      htmlTableRow.Cells[0].Attributes["class"] = "FieldSection";
      htmlTableRow.Cells[0].ColSpan = 3;
      AddRow(htmlTable2, "Item ID", item.ID.ToString());
      AddRow(htmlTable2, GetFieldCaption("Template", item.TemplateID.ToString(), null), GetSpan(item.TemplateID.ToString(), true));
      AddRow(htmlTable2, GetFieldCaption("Branch", item.BranchId.ToString(), null), GetSpan(item.BranchId.ToString(), true));
      AddRow(htmlTable2, GetSpan("Language"), GetLanguageControl());
      AddRow(htmlTable2, GetSpan("Version"), GetVersionControl(item));
      AddRow(htmlTable2, GetAnchor("Real ID", item.InnerData.Definition.ID), GetSpan(item.InnerData.Definition.ID.ToString(), true));
    }

    private void ShowMove(HtmlTableCell owner, string path)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Move", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Move \"" + _item.Name + "\"?").Cells[0].NoWrap = true;
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetMoveTarget(path)));
      HtmlGenericControl div = GetDiv(GetMoveButton());
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowPasteXml(HtmlTableCell owner)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Paste xml", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Paste xml below '" + _item.Name + "'").Cells[0].NoWrap = true;
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetPasteXmlBox()));
      HtmlInputCheckBox htmlInputCheckBox = new HtmlInputCheckBox();
      htmlInputCheckBox.ID = "c_changeIDs";
      htmlInputCheckBox.Checked = true;
      HtmlInputCheckBox htmlInputCheckBox2 = htmlInputCheckBox;
      HtmlUtil.AddRow(htmlTable2, htmlInputCheckBox2).Cells[0].Controls.Add(GetSpan("Change IDs"));
      HtmlGenericControl div = GetDiv(GetPasteXmlButton());
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowPublish(HtmlTableCell owner)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Publish", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Target").Cells[0].NoWrap = true;
      HtmlInputText htmlInputText = new HtmlInputText();
      htmlInputText.ID = "c_publishTarget";
      htmlInputText.Value = "web";
      HtmlInputText htmlInputText2 = htmlInputText;
      htmlInputText2.Attributes["class"] = "TaskInput";
      htmlInputText2.Style["width"] = "150";
      HtmlUtil.AddRow(htmlTable2, GetDiv(htmlInputText2));
      HtmlInputButton htmlInputButton = new HtmlInputButton("submit");
      htmlInputButton.ID = "c_publishChanged";
      HtmlInputButton htmlInputButton2 = htmlInputButton;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      htmlInputButton2.Value = "Publish changed";
      HtmlGenericControl div = GetDiv(htmlInputButton2);
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
      HtmlInputButton htmlInputButton3 = new HtmlInputButton("submit");
      htmlInputButton3.ID = "c_publishAll";
      htmlInputButton2 = htmlInputButton3;
      htmlInputButton2.Attributes["class"] = "TaskButton";
      htmlInputButton2.Value = "Publish all";
      div = GetDiv(htmlInputButton2);
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowRename(HtmlTableCell owner)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("Rename", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Rename \"" + _item.Name + "\"?").Cells[0].NoWrap = true;
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetRenameName()));
      HtmlGenericControl div = GetDiv(GetRenameButton());
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }

    private void ShowTree(Item item, HtmlGenericControl cell)
    {
      Item rootItem = item.Database.GetRootItem(_language);
      HtmlGenericControl htmlGenericControl = new HtmlGenericControl("div");
      htmlGenericControl.Attributes["class"] = "Scroller";
      cell.Controls.Add(htmlGenericControl);
      SafeDictionary<ID, string> safeDictionary = new SafeDictionary<ID, string>();
      for (Item item2 = item; item2 != null; item2 = item2.Parent)
      {
        safeDictionary.Add(item2.ID, string.Empty);
      }
      AddTreeNode(rootItem, htmlGenericControl, item, 0);
      AddTreeChildren(rootItem, item, htmlGenericControl, 1, safeDictionary);
    }

    private void ShowXml(HtmlTableCell owner)
    {
      HtmlContainerControl htmlContainerControl;
      HtmlContainerControl task = GetTask("View xml", out htmlContainerControl);
      owner.Controls.Add(task);
      HtmlTable htmlTable = new HtmlTable();
      htmlTable.Width = "100%";
      HtmlTable htmlTable2 = htmlTable;
      htmlContainerControl.Controls.Add(htmlTable2);
      HtmlUtil.AddRow(htmlTable2, "Xml for '" + _item.Name + "'").Cells[0].NoWrap = true;
      HtmlUtil.AddRow(htmlTable2, GetDiv(GetXmlBox()));
      HtmlGenericControl div = GetDiv(GetXmlButton());
      div.Style["text-align"] = "right";
      HtmlUtil.AddRow(htmlTable2, div);
    }
  }

}
