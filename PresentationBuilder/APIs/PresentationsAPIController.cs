﻿using PresentationBuilder.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using System.Web.Http;
using System.Net;

namespace PresentationBuilder.APIs
{
	public class PresentationsAPIController : ApiController
	{
		[HttpPost]
		public HttpResponseMessage download(int id)
		{
			return ZipHelper.zipPresentation(id);
		}

		[HttpPost]
		public Models.JsonReturn delete(int id)
		{
			var jsonReturn = new Models.JsonReturn();

			try
			{
				var context = new PresentationBuilder.Models.PresentationBuilderEntities();

				var presentation = (from p in context.Presentations where p.PresentationId == id select p).First();

				context.Presentations.Remove(presentation);

				System.IO.Directory.Delete(System.IO.Path.Combine(PathHelper.path(), presentation.PresentationId.ToString()));

				context.SaveChanges();
			}
			catch (Exception ex)
			{
				jsonReturn.isValid = false;
				jsonReturn.messages.Add(ex.Message);
			}
			
			return jsonReturn;
		}

		[HttpPost]
		public Models.JsonReturn save(PresentationBuilder.Models.Presentation model)
		{
			var jsonReturn = new Models.JsonReturn();

			if (ModelState.IsValid)
			{
				var context = new PresentationBuilder.Models.PresentationBuilderEntities();

				context.Entry(model).State = System.Data.Entity.EntityState.Modified;

				context.SaveChanges();
			}
			else
			{
				jsonReturn.isValid = false;
			}

			return jsonReturn;
		}
	}
}