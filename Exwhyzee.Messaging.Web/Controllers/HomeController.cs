using Exwhyzee.Messaging.Web.Areas.Content.Models;
using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using static Exwhyzee.Messaging.Web.Services.GeneralServices;

namespace Exwhyzee.Messaging.Web.Controllers
{
    public class HomeController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        [HttpGet, Tls]
        public async Task<ActionResult> Index()
        {
            return View();
        }
        public async Task<ActionResult> SmsFeatures()
        {
            return View();
        }


        public ActionResult _slider()
        {
            ViewBag.slides = Directory.EnumerateFiles(Server.MapPath("~/SliderImage"))
                                     .Select(fn => "~/SliderImage/" + Path.GetFileName(fn));
            return PartialView();
        }

        //slide index
        public ActionResult Slider()
        {
            var slides = db.Sliders.ToList();
            ViewBag.slides = slides;
            return View();
        }

        //adding new slide
        [HttpPost, ValidateInput(false)]
        [ValidateAntiForgeryToken]
        public ActionResult AddSlider(Slider slider)
        {
            System.Random randomInteger = new System.Random();
            int genNumber = randomInteger.Next(1000000);

            if (ModelState.IsValid)
            {
                if (Request.Files.Count > 0)
                {
                    HttpPostedFileBase file = Request.Files[0];
                    if (file.ContentLength > 0 && file.ContentType.ToUpper().Contains("JPEG") || file.ContentType.ToUpper().Contains("PNG") || file.ContentType.ToUpper().Contains("JPG"))
                    {
                        WebImage img = new WebImage(file.InputStream);
                        string fileName = Path.Combine(Server.MapPath("~/SliderImage/"), Path.GetFileName(genNumber + file.FileName));
                        img.Save(fileName);
                        slider.ImageUrl = Path.GetFileName(genNumber + file.FileName);
                    }
                }
                slider.Status = Status.Published;
                db.Sliders.Add(slider);
                db.SaveChanges();
                TempData["Success"] = " A New Slide Has Been Successfully Added.";
                return RedirectToAction("Slider");
            }

            return View(slider);
        }

        //delete slide
        public async Task<ActionResult> DeleteSlider(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Slider slider = await db.Sliders.FindAsync(id);
            if (slider == null)
            {
                return HttpNotFound();
            }
            return View(slider);
        }

        //delete slide
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteSlider(int id)
        {
            Slider slide = await db.Sliders.FindAsync(id);
            var slidename = slide.ImageUrl;

            var delName = Server.MapPath("~/SliderImage/" + slidename);
            if ((System.IO.File.Exists(delName)))
            {
                System.IO.File.Delete(delName);
            }
            db.Sliders.Remove(slide);
            await db.SaveChangesAsync();
            TempData["Success"] = " Slide Successfully Deleted.";
            return RedirectToAction("Slider");
        }
        public async Task<ActionResult> Nui()
        {
            string lnum = "";
            IQueryable<Message> homp = from s in db.Messages
                                           select s;
            var f = homp.Count();
            foreach (var input in homp)
            {
                lnum = lnum + "\r\n" + input;
            }
            string op = lnum.Replace("\r\n", ",");
            IList<string> numbers = op.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            IList<string> dnum = numbers.Distinct().ToList();
            return View();
        }
        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your app description page.";

            return View();
        }
    }
}