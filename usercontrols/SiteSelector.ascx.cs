﻿using System;
using System.Linq;
using System.Web.UI.WebControls;
using mjjames.AdminSystem.Repositories;

namespace mjjames.AdminSystem.usercontrols
{
	public partial class SiteSelector : System.Web.UI.UserControl
	{

		protected void Page_Load(object sender, EventArgs e)
		{
			//if we have onlye 1 or less options hide the drop down
			if(ddlSites.Items.Count <= 1)
			{
				Visible = false;
			}
		}

		protected void LoadSites(object sender, EventArgs e){
			//no point working if we aren't logged in
			if (!Page.User.Identity.IsAuthenticated) return;

			var userSites = new SiteUsersRepository()
				.GetAcitveSitesForUser(Page.User.Identity.Name)
				.Distinct()
				.ToList();

			//if the user only has 0 or 1 sites they don't get a choice in choosing another
			if (userSites.Count <= 1)
			{
				if (userSites.Count == 1)
				{
					Session["userSiteKey"] = userSites.First().site_key.ToString();
				}
				return;
			}



			//if we have sites then hook them up
			ddlSites.DataSource = userSites;
			ddlSites.DataTextField = "name";
			ddlSites.DataValueField = "site_key";
			ddlSites.DataBind();

			//see if we have a current site - if we dont set it to be the first result
			if (Session["userSiteKey"] == null)
			{
				Session["userSiteKey"] = userSites.First().site_key.ToString();
			}

			//if so this should be our selected site
			var currentSite = Session["userSiteKey"].ToString();
			if (!String.IsNullOrEmpty(currentSite))
			{
				ddlSites.SelectedValue = currentSite;
			}
		}

		/// <summary>
		/// Upon selecting a new site change our session value and bounce the user to the home page
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected void UpdateSiteSession(object sender, EventArgs e)
		{
			var ddl = sender as DropDownList;
			if (ddl != null) Session["userSiteKey"] = ddl.SelectedValue;
			Response.Redirect("~/");
		}
	}
}