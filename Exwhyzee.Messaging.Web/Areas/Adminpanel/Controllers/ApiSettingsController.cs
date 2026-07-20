using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    [Area("Adminpanel")]
    public class ApiSettingsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private IApiSettings _apiSettings = new ApiSettings();

        

        public ApiSettingsController(ApiSettings apiSettings)
        {
            _apiSettings = apiSettings;
        }

        // GET: Adminpanel/ApiSettings
        public async Task<ActionResult> Index()
        {
            return View(await _apiSettings.GetApis());
        }

        // GET: Adminpanel/ApiSettings/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            ApiSetting apiSetting = await _apiSettings.GetApi(id);
            if (apiSetting == null)
            {
                return NotFound();
            }
            return View(apiSetting);
        }

        // GET: Adminpanel/ApiSettings/Create
        [AllowAnonymous]
        public ActionResult Create()
        {
            var data = JosClientMain.ListMessageData();
            return View();
        }

        // POST: Adminpanel/ApiSettings/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ApiSetting apiSetting)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _apiSettings.AddApi(apiSetting);
                    return RedirectToAction("Index");
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }
            }

            return View(apiSetting);
        }

        // GET: Adminpanel/ApiSettings/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            ApiSetting apiSetting = await _apiSettings.GetApi(id);
            if (apiSetting == null)
            {
                return NotFound();
            }
            return View(apiSetting);
        }

        // POST: Adminpanel/ApiSettings/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ApiSetting apiSetting)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var allApis = db.ApiSettings.Where(x => x.ApiSettingId != apiSetting.ApiSettingId && x.IsDefault == true);
                    if (apiSetting.IsDefault == true)
                    {
                        if (allApis.Count() != 0)
                        {
                            foreach (var apis in allApis)
                            {
                                apis.IsDefault = false;

                                db.Entry(apis).State = EntityState.Modified;
                            }
                        }
                    }
                    await _apiSettings.UpdateApi(apiSetting);
                    return RedirectToAction("Index");
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }
            }
            return View(apiSetting);
        }

        // GET: Adminpanel/ApiSettings/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            ApiSetting apiSetting = await _apiSettings.GetApi(id);
            if (apiSetting == null)
            {
                return NotFound();
            }
            return View(apiSetting);
        }

        // POST: Adminpanel/ApiSettings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            ApiSetting apiSetting = await _apiSettings.GetApi(id);
            await _apiSettings.DeleteApi(apiSetting);
            return RedirectToAction("Index");
        }

       
        

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}