﻿using mjjames.AdminSystem.Services;
using System;
using System.Web;
using System.Web.Security;

namespace mjjames.AdminSystem.authentication
{
	public partial class Login : System.Web.UI.Page
	{
		protected void Page_Load(object sender, EventArgs e)
		{
			Title = "System Login";
		    if (string.IsNullOrWhiteSpace(Form.Action))
		    {
		        Form.Action = "Default.aspx";
		    }
            if (!IsPostBack)
            {
                returnUrl.Value = Request["ReturnUrl"];
            }
		}
		/// <summary>
		/// Validate Our User
		/// </summary>
		/// <param name="userName">Username</param>
		/// <param name="passWord">password</param>
		/// <returns></returns>
		private static bool ValidateUser(string userName, string passWord)
		{
			return Membership.ValidateUser(userName, passWord);
		}

		/// <summary>
		/// Login Button Handler
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected void btnLogin_Click(object sender, EventArgs e)
		{
			if (ValidateUser(inputUserName.Value, inputPassword.Value))
			{
				var tkt = new FormsAuthenticationTicket(1, inputUserName.Value, DateTime.Now, DateTime.Now.AddMinutes(30), false, "synthesis");
			
				var cookiestr = FormsAuthentication.Encrypt(tkt);
				var ck = new HttpCookie(FormsAuthentication.FormsCookieName, cookiestr) {Path = FormsAuthentication.FormsCookiePath};

			    Response.Cookies.Add(ck);
                AuditLogService.LogItem("Authentication", Models.AuditEvent.Login, inputUserName.Value, "");
				string strRedirect = returnUrl.Value ?? "~/";
                Response.Redirect(strRedirect, true);
			}
			else
			{
				lblMsg.Text = "System Login: <span class=\"text-error\">Login Failed</span>";
			}
		}
	}
}