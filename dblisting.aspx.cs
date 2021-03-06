using System;
using System.Configuration;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.WebControls;
using mjjames.AdminSystem.classes;
using mjjames.core;
using System.Collections.Specialized;
using mjjames.AdminSystem.dataentities;
using mjjames.AdminSystem.dataControls;
using mjjames.ControlLibrary.WebControls;
using System.Web;
using System.Web.UI.HtmlControls;
using mjjames.ControlLibrary.AdminWebControls;

namespace mjjames.AdminSystem
{
    public partial class ListPage : Page
    {
        private string _sType = String.Empty;
        private XmlDBBase _xmldb;
        private readonly ILogger _logger = new Logger("DBListing");


        protected override void OnInit(EventArgs e)
        {

            base.OnInit(e);

            if (Page.RouteData.Values.ContainsKey("type"))
            {
                _sType = Page.RouteData.Values["type"].ToString();
            }

            var activator = Activator.CreateInstance(null, "mjjames.AdminSystem.XmlDB" + _sType);
            _xmldb = activator != null ? activator.Unwrap() as XmlDBBase : null;
            if (_xmldb == null)
            {
                var exception = new InvalidCastException(String.Format("Can not cast: {0} to {1}", "XmlDB" + _sType, "XmlDBBase"));
                _logger.LogError("DBListing Unable To Cast", exception);
                throw exception;
            }

            var id = "" + Page.RouteData.Values["filter"]; // != null ? Page.RouteData.Values["filter"].ToString() : "";
            //we don't allow pages to use filter as a table id name , this is because for pages filter is the fkey
            if (_sType == "pages")
            {
                id = "";
            }

            _xmldb.TableName = String.IsNullOrWhiteSpace(id) ? _sType : id;
            _xmldb.ConnectionString = ConfigurationManager.ConnectionStrings["ourDatabase"].ConnectionString;
            _xmldb.ArchiveDataEnabled = Convert.ToBoolean(ConfigurationManager.AppSettings["Synthesis:ArchiveDataEnabled"]);
            //if we have no site key we have an error - assume this is because of an expired session so log the user out
            if (Session["userSiteKey"] == null)
            {
                FormsAuthentication.SignOut();
                var authUrl = GetRouteUrl("Login", new
                {
                    ReturnUrl = Server.UrlEncode(Page.Request.Url.PathAndQuery)
                });
                Response.Redirect(authUrl, true);
            }

            _xmldb.SiteKey = int.Parse(Session["userSiteKey"].ToString());


        }

        protected void Page_Load(object sender, EventArgs e)
        {
            Title = _xmldb.TableLabel + " Editor: Listing View";
        }

        protected void SetupTable(object sender, EventArgs e)
        {

            var datasource = _xmldb.DataSource(true, true, false, false, true);
            sdsData.SelectCommand = datasource.SelectCommand;
            sdsData.UpdateCommand = datasource.UpdateCommand;
            sdsData.InsertCommand = datasource.InsertCommand;
            sdsData.DeleteCommand = datasource.DeleteCommand;
            pageListing.DataKeyNames = new[] { _xmldb.TablePrimaryKeyField };
            sdsData.Deleting += SdsDataDeleting;
            sdsData.Deleted += SdsDataDeleted;

            if (_xmldb.TableDefaults == null)
            {
                throw new Exception("XMLDB Table Defaults not configured");
            }
            var cfSelect = new CommandField
            {
                ShowSelectButton = true,
                SelectText = "Edit",
            };

            cfSelect.ControlStyle.CssClass = "btn btn-primary btn-small";

            pageListing.Columns.Add(cfSelect);
            var defaultFilterSet = false;
            foreach (AdminField field in _xmldb.TableDefaults.FindAll(t => t.Attributes.ContainsKey("list")))
            {
                var dcf = new BoundField();
                string type;
                var bType = field.Attributes.TryGetValue("type", out type);
                var bRenderField = true;

                if (!Page.IsPostBack && field.Attributes.ContainsKey("listfilter"))
                {
                    //we allow one route paramter to act as the default filter key
                    //however as we allow more than one these will then be querystring parameters
                    Parameter p;
                    if (defaultFilterSet)
                    {
                        p = new QueryStringParameter(field.ID, field.ID);
                    }
                    else
                    {
                        p = new RouteParameter(field.ID, "filter");
                        defaultFilterSet = true;
                    }
                    if (!sdsData.SelectParameters.Contains(p))
                    { //if post back this should already be in the params so dont add another one
                        sdsData.SelectParameters.Add(p);
                    }
                }

                dcf.HeaderText = field.Label;
                dcf.SortExpression = field.ID;
                dcf.DataField = field.ID;

                if (!bType) continue;
                // Specific Functionality Depends on Type
                switch (type)
                {
                    case "hidden":
                        bRenderField = false;
                        break;
                    case "datetime":
                        dcf.DataFormatString = "{0:dd/MM/yyyy}";
                        break;
                    case "dropdown":
                        var ddc = new DropdownControl();
                        var ddl = (DropDownList)ddc.GenerateControl(field, new object());
                        var tcf = new TemplateField
                                                {
                                                    ItemTemplate =
                                                        new GridViewDropDownListTemplate(DataControlRowType.DataRow, field.Label, field.ID, ddl,
                                                                                         true),
                                                    HeaderText = field.Label,
                                                    SortExpression = field.ID
                                                };

                        bRenderField = false;
                        pageListing.Columns.Add(tcf);
                        break;
                    default:
                        break;
                }
                if (bRenderField)
                {
                    pageListing.Columns.Add(dcf);
                }

            }

            var listfilter = _xmldb.TableDefaults.Find(t => t.Attributes.ContainsKey("listfilter"));
            if (listfilter != null)
            {
                pageListing.Columns.Add(new TemplateField{
                    ItemTemplate = new ChildEntityActionsTemplate(DataControlRowType.DataRow, _xmldb.TableLabel, _xmldb.TablePrimaryKeyField, listfilter.ID, _sType),
                    HeaderText = "Child Pages"
                });
            }

          
            var cfDelete = new CommandField
            {
                ShowDeleteButton = true
            };

            cfDelete.ControlStyle.CssClass = "btn btn-danger btn-small buttonDelete";
            pageListing.Columns.Add(cfDelete);

            UpdateLabels(_xmldb.TableLabel, _xmldb);

        }

        void SdsDataDeleting(object sender, SqlDataSourceCommandEventArgs e)
        {
            if (_xmldb.ArchiveDataEnabled)
            {
                _xmldb.ArchiveData((int)e.Command.Parameters["@" + _xmldb.TablePrimaryKeyField].Value);
            }
        }

        //upon deletion of a page reset the sitemap
        void SdsDataDeleted(object sender, SqlDataSourceStatusEventArgs e)
        {
            // if we dont have a quick edit drop out
            if (!_xmldb.TableQuickEdit)
            {
                return;
            }
            //following a deletion you have to reset the sitemap and then reload the tree listing
            GenericFunctions.ResetSiteMap();
            var control = treePanel.ContentTemplateContainer.FindControl("treeListing") as TreeView;
            treePanel.ContentTemplateContainer.Controls.Remove(control);
            System.Diagnostics.Debug.WriteLine("Reload Page Tree Listing");
            LoadListing(sender, e);


        }

        protected void UpdateLabels(string label, XmlDBBase xmldb)
        {
            dbEditorLabel.Text = label;
            buttonAddPage.Text = String.Format("Add {0}", label);
            buttonAddPage.ToolTip = String.Format("Add a new {0}", label);
            levelLabel.InnerText = String.Format("{0}s at this navigation level", label);
            linkbuttonBack.Text = String.Format("View parent {0}", label.ToLower());
            linkbuttonBack.ToolTip = String.Format("View this {0}s immediate parent", label.ToLower());

            string sForeignKeys = String.Empty;
            var listfilter = xmldb.TableDefaults.Find(t => t.Attributes.ContainsKey("listfilter"));

            if (listfilter == null)
            {
               //if we have no filter then add is always available
                buttonAddPage.NavigateUrl = GetRouteUrl("DBEditor", new
                {
                    Type = _sType,
                    Key = 0
                });
                buttonAddPage.Visible = true;
                linkbuttonBack.Visible = false;
                return;
            }
            var foreignKeyData = Page.RouteData.Values.ContainsKey("filter") ?  Page.RouteData.Values["filter"].ToString()  : "";
            if (!String.IsNullOrWhiteSpace(foreignKeyData))
            {
                
                buttonAddPage.NavigateUrl = GetRouteUrl("DBEditor", new
                {
                    Type = _sType,
                    Filter = foreignKeyData,
                    Key = 0
                });
                buttonAddPage.Visible = true;

                linkbuttonBack.NavigateUrl = GetRouteUrl("DBEditor", new
                {
                    Type = _sType,
                    Key = foreignKeyData,
                    Filter = ""
                });

                if (foreignKeyData.Equals("0"))
                {
                    linkbuttonBack.Visible = false;
                    buttonAddPage.Visible = false;
                }
            }
            else
            {
                linkbuttonBack.Visible = false;
            }
            //var id = Page.RouteData.Values.ContainsKey("id") ? Page.RouteData.Values["id"].ToString() : "";
            //if (!String.IsNullOrWhiteSpace(id))
            //{
            //    buttonAddPage.NavigateUrl += "&id=" + id;
            //}

        }

        //on select load up DBEditor
        protected void GridView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var strURL = GetRouteUrl("DBEditor", new
            {
                Type = _sType,
                Key = pageListing.SelectedValue
            });
            Response.Redirect(strURL);
        }

        //loads the quickedit listing
        protected void LoadListing(object sender, EventArgs e)
        {
            var strParent = String.Empty;
            var strTitle = String.Empty;
            var bQuickEdit = false;

            var config = new NameValueCollection();

            if (_xmldb.TableDefaults != null)
            {
                strParent = _xmldb.TableDefaults.Find(d => d.Attributes.ContainsKey("foreignkey")) != null ? _xmldb.TableDefaults.Find(d => d.Attributes.ContainsKey("foreignkey")).ID : "0";
                strTitle = _xmldb.TableDefaults.Find(d => d.Attributes.ContainsKey("treelistingtitle")) != null ? _xmldb.TableDefaults.Find(d => d.Attributes.ContainsKey("treelistingtitle")).ID : String.Empty;
                bQuickEdit = _xmldb.TableQuickEdit;
            }

            if (bQuickEdit)
            {


                if (_sType.Equals("newsletters", StringComparison.CurrentCultureIgnoreCase))
                {
                    navigationSiteMap.SiteMapProvider = "newsletterNavigation";
                    navigationSiteMap.ShowStartingNode = false;
                }
                else
                {
                    Page.Trace.Write("Site Key:" + Session["userSiteKey"]);
                    var strQuery = _xmldb.GetQuickEditSiteMapQuery();
                    //generate a url prefix based on the route
                    var strURLPrefix = GetRouteUrl("DBEditor", new
                    {
                        Type = _sType,
                        Key = 0,
                        Filter = 0
                    });
                    //but as we pass key and fkey of 0 it will end in /0/0 remove this for the sitemap
                    //to add the key
                    strURLPrefix = strURLPrefix.Replace("/0/0", "/");


                    config.Add("query", strQuery);
                    config.Add("urlprefix", strURLPrefix);
                    config.Add("connectionStringName", "ourDatabase");
                    config.Add("DisableRootURLFix", "true");

                    var cssmp = new CustomSqlSiteMapProvider
                                    {
                                        SiteKey = int.Parse(Session["userSiteKey"].ToString())
                                    };
                    cssmp.Initialize("Admin Navigation SiteMap", config);
                    cssmp.ParentProvider = SiteMap.Provider;
                    System.Diagnostics.Debug.WriteLine("Admin Navigation SiteMap");
                    navigationSiteMap.Provider = cssmp;
                    Page.Trace.Write("CUSTOM SITEMAP QUERY: " + strQuery);
                }
                var treeview = new TreeView
                                        {
                                            ID = "treeListing",
                                            PopulateNodesFromClient = true,
                                            EnableClientScript = true,
                                            ShowExpandCollapse = true,
                                            ShowLines = true,
                                            ExpandDepth = 1,
                                            CssClass = "treeView",
                                            EnableViewState = false,
                                            CollapseImageToolTip = "hide child items",
                                            ExpandImageToolTip = "show child items",
                                            DataSource = navigationSiteMap
                                        };

                treeview.DataBind();

                treePanel.ContentTemplateContainer.Controls.Add(treeview);
            }
            else
            {
                var notavail = new LiteralControl("<p class=\"notavail\">Not Available</p>");
                treePanel.ContentTemplateContainer.Controls.Add(notavail);

                leftCol.Visible = false; //just not render quick edit
            }

        }

    }
}