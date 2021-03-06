﻿using PresentationBuilder.Helpers;
using PresentationBuilder.Models;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System.Threading.Tasks;

namespace PresentationBuilder.Controllers
{
	public class PresentationsController : Controller
	{
		private ApplicationUserManager _userManager;

		public PresentationsController()
		{
		}

		public PresentationsController(ApplicationUserManager userManager)
		{
			UserManager = userManager;
		}

		public ApplicationUserManager UserManager
		{
			get
			{
				return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
			}
			private set
			{
				_userManager = value;
			}
		}

		[Authorize]
		public ActionResult Index()
		{
			var context = new PresentationBuilderEntities();

			var presentations = (from p in context.Presentations.Include("AspNetUser")
									   where p.AspNetUser.UserName == User.Identity.Name
									   orderby p.Name
									   select p).ToList();

			foreach (var p in presentations)
			{
				p.PresentationPages = (from pg in context.PresentationPages
									   where pg.PresentationId == p.PresentationId 
									   orderby pg.Order
									   select pg).Take(1).ToList();
			}

			return View(presentations);
		}

		[Authorize]
		public ActionResult Presentation(int id)
		{
			var context = new PresentationBuilderEntities();

			var presentation = (from p in context.Presentations.Include("PresentationPages") where p.PresentationId == id select p).First();

			return View(presentation);
		}

		[Authorize]
        public ActionResult Preview(int id)
        {
            var context = new PresentationBuilderEntities();

            var presentation = (from p in context.Presentations.Include("PresentationPages") where p.PresentationId == id select p).First();

            return View(presentation);
        }

        [Authorize]
		[HttpPost]
		public ActionResult SavePresentation(PresentationBuilder.Models.Presentation model)
		{
			if (ModelState.IsValid)
			{
				var context = new PresentationBuilderEntities();

				context.Entry(model).State = System.Data.Entity.EntityState.Modified;

				context.SaveChanges();
			}

			return View(model);
		}

		
		[Authorize]
		[HttpPost]
		public ActionResult UploadZipAsync()
		{
			var uploadReturn = new UploadReturn();

			var context = new PresentationBuilderEntities();

			var UserId = (from u in context.AspNetUsers where u.UserName == User.Identity.Name select u.Id).First();

			try
			{
				var Presentation = Helpers.ZipHelper.unzipPresentation(Request.Files[0], UserId);

				uploadReturn.data = new Presentation
				{
					PresentationId = Presentation.PresentationId,
					Name = Presentation.Name
				};
			}
			catch (Exception ex)
			{
				uploadReturn.uploadStatus = uploadStatus.Error;
				uploadReturn.message = ex.Message;
			}

			return Json(uploadReturn, "text/plain");
		}

		[Authorize]
		[HttpPost]
        [ValidateInput(false)]        
        public ActionResult AddPresentation(Presentation model)
        {
            if (ModelState.IsValid)
            {
                var context = new PresentationBuilderEntities();

                Presentation p = context.Presentations.FirstOrDefault(x => x.PresentationId == model.PresentationId);
                p.Description = model.Description;
                p.Name = model.Name;

                context.SaveChanges();

                return RedirectToAction("Index", new { Message = "Presentation Saved !" });
            }

            return View(model);
        }       

        [Authorize]
		[HttpPost]
		public ActionResult SaveUploadedFile()
		{
			var uploadReturn = new UploadReturn();

			string fName = "";

			try
			{
				foreach (string fileName in Request.Files)
				{
					HttpPostedFileBase file = Request.Files[fileName];
					//Save file content goes here
					fName = file.FileName;


                    
					if (file != null && file.ContentLength > 0)
					{
                        var context = new PresentationBuilderEntities();
                        bool isZipFile = false;
                        var UserId = (from u in context.AspNetUsers where u.UserName == User.Identity.Name select u.Id).First();

						if (file.ContentType == "application/x-zip-compressed")
						{
							isZipFile = true;
						}

                        if (isZipFile)
                        {

                            try
                            {
                                var Presentation = Helpers.ZipHelper.unzipPresentation(file, UserId);

								return Json(new { Message = Presentation.PresentationId });
                            }
                            catch (Exception ex)
                            {
                                uploadReturn.uploadStatus = uploadStatus.Error;
                                uploadReturn.message = ex.Message;
                            }

						}
                        else
                        {
                            var fileNameUpload = Path.GetFileName(file.FileName);

						Presentation presentation = new Presentation
						{
							UserId = UserId,
							Date = DateTime.Now,
							Name = "New Presentation",
							Description = null
						};

						context.Presentations.Add(presentation);
						context.SaveChanges();

						uploadReturn.data = presentation;

						string pathWork = Path.Combine(ZipHelper.path(), presentation.PresentationId.ToString());

						if (!Directory.Exists(pathWork))
							Directory.CreateDirectory(pathWork);

						file.SaveAs(Path.Combine(pathWork, file.FileName));

						PdfHelper.splitToImages(Path.Combine(pathWork, file.FileName), pathWork);

						byte bOrder = 0;

						foreach (string filePresentaion in Directory.EnumerateFiles(pathWork, "*.*", SearchOption.AllDirectories).Where(s => s.EndsWith(".jpg")))
						{

							presentation.PresentationPages.Add(new PresentationPage
							{
								Order = bOrder++,
								ImagePath = Path.GetFileName(filePresentaion),
								SoundPath = null,
								SoundLength = null
							});
						}

						context.SaveChanges();

						System.IO.File.Delete(Path.Combine(pathWork, file.FileName));

						return Json(new { Message = presentation.PresentationId });
					}
				}
			}
            }
			catch (Exception ex)
			{
				uploadReturn.uploadStatus = uploadStatus.Error;
				uploadReturn.message = ex.Message;
			}

			return Json(uploadReturn, "text/plain");
		}

        [Authorize]        
		public ActionResult DuplicatePresentation(int id)
		{
			var context = new PresentationBuilderEntities();

			var currentPresentation = (from p in context.Presentations.Include("PresentationPages")
                                       where p.PresentationId == id select p).First();

            var newPresentation = new Presentation
            {
                //PresentationId = currentPresentation.PresentationId,
                UserId = currentPresentation.UserId,
                Name = currentPresentation.Name + " - Copy",
                Date = DateTime.Now,
                Description = currentPresentation.Description
            };

            context.Presentations.Add(newPresentation);
			context.SaveChanges();

			foreach (var oPresentationPage in currentPresentation.PresentationPages)
			{
                var newPresentationPage = new PresentationPage
                {
                    //PresentationPageId = oPresentationPage.PresentationPageId,
                    PresentationId = newPresentation.PresentationId,
                    ImagePath = oPresentationPage.ImagePath,
                    SoundPath = oPresentationPage.SoundPath,
                    SoundLength = oPresentationPage.SoundLength,
                    Order = oPresentationPage.Order,
                    Hidden = oPresentationPage.Hidden
                };

				context.PresentationPages.Add(newPresentationPage);
			}

            context.SaveChanges();

            var currentPath = Path.Combine(PathHelper.path(), currentPresentation.PresentationId.ToString());
            var newPath = Path.Combine(PathHelper.path(), newPresentation.PresentationId.ToString());

            Directory.CreateDirectory(newPath);

            foreach (string file in Directory.GetFiles(currentPath, "*.*", SearchOption.AllDirectories))
                System.IO.File.Copy(file, file.Replace(currentPath, newPath), true);

            return RedirectToAction("Presentation", new { id = newPresentation.PresentationId });
        }

		[Authorize]
		[HttpPost]
		public ActionResult ChangeOrder(string presentationPageId, string newOrder)
		{
			var uploadReturn = new UploadReturn();

			try
			{
				if (string.IsNullOrEmpty(presentationPageId))
				{
					uploadReturn.uploadStatus = uploadStatus.Error;
					uploadReturn.message = "presentationPageId is null";
				}
				else
				{
					int intPresentationPageId = Convert.ToInt32(presentationPageId);

					var context = new PresentationBuilderEntities();

					var presentationPage = (from p in context.PresentationPages where p.PresentationPageId == intPresentationPageId select p).First();

					presentationPage.Order = Convert.ToByte(newOrder);

					context.SaveChanges();

					return Json(new { Message = "Order Changed" });
				}


			}
			catch (Exception ex)
			{
				uploadReturn.uploadStatus = uploadStatus.Error;
				uploadReturn.message = ex.Message;
			}

			return Json(uploadReturn, "text/plain");
		}

		[Authorize]
		[HttpPost]
		public ActionResult ChangeAudio(int id)
		{
			var uploadReturn = new UploadReturn();

			try
			{
				var context = new PresentationBuilderEntities();

				var presentationPage = (from pg in context.PresentationPages where pg.PresentationPageId == id select pg).First();

				presentationPage.SoundPath = Request.Files[0].FileName;

				string path = System.IO.Path.Combine(PathHelper.path(), presentationPage.PresentationId.ToString(), Request.Files[0].FileName);

				Request.Files[0].SaveAs(path);

				TagLib.File f = TagLib.File.Create(path, TagLib.ReadStyle.Average );
				var duration = (int)f.Properties.Duration.TotalSeconds;

				presentationPage.SoundLength = duration;

				context.SaveChanges();

				return Json(new { Message = id });
			}
			catch (Exception ex)
			{
				uploadReturn.uploadStatus = uploadStatus.Error;
				uploadReturn.message = ex.Message;
			}

			return Json(uploadReturn, "text/plain");
		}

		[Authorize]
		[HttpPost]
		public ActionResult ChangePage(int id)
		{
			var uploadReturn = new UploadReturn();

			try
			{
				var context = new PresentationBuilderEntities();

				var presentationPage = (from pg in context.PresentationPages where pg.PresentationPageId == id select pg).First();

				presentationPage.ImagePath = Request.Files[0].FileName;

				Request.Files[0].SaveAs(System.IO.Path.Combine(PathHelper.path(), presentationPage.PresentationId.ToString(), Request.Files[0].FileName));

				context.SaveChanges();

				return Json(new { Message = id });
			}
			catch (Exception ex)
			{
				uploadReturn.uploadStatus = uploadStatus.Error;
				uploadReturn.message = ex.Message;
			}

			return Json(uploadReturn, "text/plain");
		}

		[Authorize]
		[HttpPost]
		public ActionResult AddPage(int id)
		{
			var uploadReturn = new UploadReturn();

			try
			{
				var context = new PresentationBuilderEntities();

				var presentationPage = (from pg in context.PresentationPages where pg.PresentationId == id orderby pg.Order descending select pg).FirstOrDefault();

				var newPresentationPage = new PresentationPage { PresentationId = id,
																 ImagePath = Request.Files[0].FileName,
																 Order = (presentationPage == null ? Convert.ToByte(1) : ++presentationPage.Order),
																 Hidden=false
				};

				context.PresentationPages.Add(newPresentationPage);

				Request.Files[0].SaveAs(System.IO.Path.Combine(PathHelper.path(), id.ToString(), Request.Files[0].FileName));

				context.SaveChanges();

				return Json(new { Message = id });
			}
			catch (Exception ex)
			{
				uploadReturn.uploadStatus = uploadStatus.Error;
				uploadReturn.message = ex.Message;
			}

			return Json(uploadReturn, "text/plain");
		}
	}
}