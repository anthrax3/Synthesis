﻿using System;
using System.Data.Linq;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using AjaxControlToolkit;
using mjjames.AdminSystem.dataentities;
using mjjames.AdminSystem.DataEntities;
using mjjames.AdminSystem.DataContexts;

/// <summary>
/// Summary description for xmlDB
/// </summary>
/// 
namespace mjjames.AdminSystem
{
	public class XmlDBtestimonies : XmlDBBase
	{
		/// <summary>
		/// constructor
		/// </summary>
		public XmlDBtestimonies()
			: base()
		{

		}


		#region datasources

		/// <summary>
		/// Gets the data for this editor
		/// </summary>
		/// <returns>a general object that needs casting to the correct type on use</returns>
		protected override object GetData()
		{
			object ourData = new object();
			
			testimony ourTestimony = new testimony();
			if (_iPKey > 0)
			{
				ourTestimony = (from p in adminDC.testimonies
							  where p.testimony_key == _iPKey
							  select p).SingleOrDefault();
			}
			ourData = ourTestimony;

			return ourData;
		}




		#endregion

		#region button events


		/// <summary>
		/// save away our data / insert
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		protected override void saveEdit(object sender, EventArgs e)
		{
			Button ourSender = (Button)sender;
			adminDataContext ourPageDataContext = new adminDataContext();
			testimony ourData = new testimony();
			if (_iPKey > 0)
			{
				ourData = ourPageDataContext.testimonies.Single(p => p.testimony_key == _iPKey);
			}

			var ourfields = from fields in atTable.Tabs
							select new
							{
								ID = fields.ID
							};

			foreach (AdminTab tab in atTable.Tabs)
			{
				TabPanel ourTab = (TabPanel)FindControlRecursive(ourSender.Page, tab.ID);
				if (ourTab != null)
				{
					foreach (AdminField field in tab.Fields)
					{
						Control ourControl = (Control)ourTab.FindControl("control" + field.ID);

						if (ourControl != null)
						{
							PropertyInfo ourProperty = ourData.GetType().GetProperty(field.ID);
							if (ourProperty != null)
							{
								HttpContext.Current.Trace.Warn("Saving Content In: " + ourControl.ID);
								ourProperty.SetValue(ourData, getDataValue(ourControl, field.Type, ourProperty.PropertyType), null);
							}
							else
							{
								HttpContext.Current.Trace.Warn("Error Saving Content: " + ourControl.ID);
							}
						}
					}
				}
			}

			if (_iPKey == 0)
			{
				ourPageDataContext.testimonies.InsertOnSubmit(ourData);
			}

			Label labelStatus = (Label)FindControlRecursive(ourSender.Page, ("labelStatus"));
			try
			{
				ChangeSet ourChanges = ourPageDataContext.GetChangeSet();

				labelStatus.Text = "Nothing to Save";

				ourPageDataContext.SubmitChanges();

				if (ourChanges.Inserts.Count > 0)
				{
					labelStatus.Text = String.Format("{0} Inserted", atTable.ID);
					string strPKeyField = String.Empty;


					_iPKey = ((testimony)ourData).testimony_key;
				
					strPKeyField = TablePrimaryKeyField;

					HiddenField ourPKey = (HiddenField)FindControlRecursive(labelStatus.Parent, "pkey");
					HiddenField ourControlPKey = (HiddenField)FindControlRecursive(labelStatus.Parent, "control" + strPKeyField);

					try
					{
						ourControlPKey.Value = _iPKey.ToString();
						ourPKey.Value = _iPKey.ToString();
					}
					catch
					{
						throw new Exception(String.Format("{0} doesn't contain a hidden control called {1}", atTable.ID, TablePrimaryKeyField));
					}
				}
				if (ourChanges.Updates.Count > 0)
				{
					labelStatus.Text = String.Format("{0} Updated", atTable.ID);
				}


			}
			catch (Exception ex)
			{
				labelStatus.Text = String.Format("{0} Update Failed: {1}", atTable.ID, ex);
			}
		}

		#endregion

		public override void ArchiveData(int iKey)
		{
			testimony oldTestimony = (from t in adminDC.testimonies
										where t.testimony_key == iKey
										select t).SingleOrDefault();
										
			DataEntities.Archive.testimony archiveTestimony = new mjjames.AdminSystem.DataEntities.Archive.testimony(){
				active = oldTestimony.active,
				description = oldTestimony.description,
				project_fkey = oldTestimony.project_fkey,
				testimony_key = oldTestimony.testimony_key,
				title = oldTestimony.title,
				url = oldTestimony.url,
				video_id = oldTestimony.video_id,
				DBName = adminDC.Connection.Database
			};
			
			DataContexts.Archive.archiveDataContext archiveDC = new mjjames.AdminSystem.DataContexts.Archive.archiveDataContext();
			
			archiveDC.testimonies.InsertOnSubmit(archiveTestimony);
			adminDC.testimonies.DeleteOnSubmit(oldTestimony);
			
			archiveDC.SubmitChanges();
			adminDC.SubmitChanges();
		}

	}

}