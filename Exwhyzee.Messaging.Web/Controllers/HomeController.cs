using Microsoft.Extensions.DependencyInjection;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Exwhyzee.Messaging.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public HomeController(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _env = env;
        }

        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();

        private IEnumerable<string> GetSliderImages()
        {
            try
            {
                var webRoot = _env?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var sliderPath = Path.Combine(webRoot, "Sliderimage");
                if (!Directory.Exists(sliderPath))
                {
                    sliderPath = Path.Combine(webRoot, "SliderImage");
                }

                if (Directory.Exists(sliderPath))
                {
                    return Directory.EnumerateFiles(sliderPath)
                                    .Select(fn => "~/Sliderimage/" + Path.GetFileName(fn))
                                    .ToList();
                }
            }
            catch
            {
                // Fallback to empty list if directory is missing
            }
            return new List<string>();
        }

        [HttpGet]
        public async Task<ActionResult> Index()
        {
            try
            {
                var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
                ViewBag.AdminSetting = adminSetting;
            }
            catch
            {
                ViewBag.AdminSetting = null;
            }

            ViewBag.slides = GetSliderImages();
            return View();
        }

        public async Task<ActionResult> SmsFeatures()
        {
            return View("Features");
        }

        public async Task<ActionResult> Features()
        {
            return View();
        }

        public async Task<ActionResult> UpdatedFeatures()
        {
            return View("Features");
        }

        public async Task<ActionResult> SmsPlan()
        {
            var priceSettings = await db.PriceSettings.Include(p => p.DialCodes).ToListAsync();
            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            ViewBag.AdminSetting = adminSetting;
            return View(priceSettings);
        }

        public ActionResult Developers()
        {
            return View();
        }

        public ActionResult _slider()
        {
            ViewBag.slides = GetSliderImages();
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddSlider(Slider slider)
        {
            Random randomInteger = new Random();
            int genNumber = randomInteger.Next(1000000);

            if (ModelState.IsValid)
            {
                if (Request.Form.Files.Count > 0)
                {
                    IFormFile file = Request.Form.Files[0];
                    if (file.Length > 0 && (file.ContentType.ToUpper().Contains("JPEG") || file.ContentType.ToUpper().Contains("PNG") || file.ContentType.ToUpper().Contains("JPG")))
                    {
                        var webRoot = _env?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                        var sliderFolder = Path.Combine(webRoot, "Sliderimage");
                        if (!Directory.Exists(sliderFolder))
                        {
                            Directory.CreateDirectory(sliderFolder);
                        }
                        string fileName = Path.Combine(sliderFolder, Path.GetFileName(genNumber + file.FileName));
                        using (var stream = new FileStream(fileName, FileMode.Create)) { file.CopyTo(stream); }
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
                return BadRequest();
            }
            Slider slider = await db.Sliders.FindAsync(id);
            if (slider == null)
            {
                return NotFound();
            }
            return View(slider);
        }

        //delete slide
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteSlider(int id)
        {
            Slider slide = await db.Sliders.FindAsync(id);
            if (slide != null)
            {
                var slidename = slide.ImageUrl;
                var webRoot = _env?.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var delName = Path.Combine(webRoot, "Sliderimage", slidename ?? "");
                if (System.IO.File.Exists(delName))
                {
                    System.IO.File.Delete(delName);
                }
                db.Sliders.Remove(slide);
                await db.SaveChangesAsync();
                TempData["Success"] = " Slide Successfully Deleted.";
            }
            return RedirectToAction("Slider");
        }

        [HttpGet]
        public async Task<ActionResult> Contact()
        { 
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Contact(string FullName, string Email, string PhoneNumber, string Message)
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Message))
            {
                TempData["Error"] = "Please fill in all required fields (Name, Email, and Message).";
                return View();
            }

            try
            {
                var recaptchaService = HttpContext.RequestServices.GetService<Exwhyzee.Messaging.Core.Services.IGoogleReCaptchaService>();
                if (recaptchaService != null)
                {
                    var recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
                    var isHuman = await recaptchaService.VerifyTokenAsync(recaptchaToken);
                    if (!isHuman)
                    {
                        TempData["Error"] = "Please check the reCAPTCHA box to confirm you are not a robot.";
                        return View();
                    }
                }

                var zeptoService = HttpContext.RequestServices.GetService<IZeptoMailService>();
                if (zeptoService != null)
                {
                    var sent = await zeptoService.SendContactInquiryAsync(FullName, Email, PhoneNumber ?? "N/A", Message);
                    if (sent)
                    {
                        TempData["Success"] = "Thank you! Your message has been sent successfully. Our support team will follow up with you shortly.";
                    }
                    else
                    {
                        TempData["Success"] = "Thank you! Your message has been received. Our team will contact you shortly.";
                    }
                }
                else
                {
                    TempData["Success"] = "Thank you! Your message has been received.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = "An error occurred while sending your message. Please reach out directly via WhatsApp or phone.";
            }

            return RedirectToAction("Contact");
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Privacy()
        {
            return View();
        }

        public ActionResult Terms()
        {
            return View();
        }

        public ActionResult Faq()
        {
            return View();
        }

        public ActionResult Sitemap()
        {
            return View();
        }
    }
}
