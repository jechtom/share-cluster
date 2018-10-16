using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.WebInterface
{
    [ServiceFilter(typeof(HttpFilterOnlyLocal))]
    public class HttpWebInterfaceController : Controller
    {
        private readonly WebFacade facade;

        public HttpWebInterfaceController(WebFacade facade)
        {
            this.facade = facade ?? throw new ArgumentNullException(nameof(facade));
        }

        public IActionResult Index() => View(model: facade.GetStatusViewModel());

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StartDownloadDiscoveredPackage(PackageId packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            facade.TryStartDownloadDiscovered(packageId);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StartDownloadPackage(PackageId packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            facade.TryChangeDownloadPackage(packageId, start: true);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StopDownloadPackage(PackageId packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            facade.TryChangeDownloadPackage(packageId, start: false);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StartVerifyPackage(PackageId packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            facade.TryVerifyPackage(packageId);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult DeletePackage(PackageId packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            PackageOperationViewModel package;
            if ((package = facade.GetPackageOrNull(packageId)) == null) return NotFound();
            return View(package);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeletePackage(PackageId packageId, object _)
        {
            if (!ModelState.IsValid) return BadRequest();

            PackageOperationViewModel package;
            if ((package = facade.GetPackageOrNull(packageId)) == null) return NotFound();

            try
            {
                facade.DeletePackage(package.Id);
            }
            catch(Exception e)
            {
                ModelState.AddModelError(nameof(package.Id), e.Message);
                return View(package);
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult ExtractPackage(PackageId packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            PackageOperationViewModel package;
            if ((package = facade.GetPackageOrNull(packageId)) == null) return NotFound();
            var model = new ExtractPackageViewModel()
            {
                DoValidate = true,
                Folder = facade.RecommendFolderForExtraction(),
                Package = package
            };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ExtractPackage(PackageId packageId, ExtractPackageViewModel viewModel)
        {
            if (!ModelState.IsValid) return View();

            PackageOperationViewModel package;
            if ((package = facade.GetPackageOrNull(packageId)) == null) return NotFound();

            try
            {
                facade.ExtractPackage(package.Id, viewModel.Folder, viewModel.DoValidate);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult CreatePackage()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CreatePackage(CreatePackageViewModel viewModel)
        {
            if (!ModelState.IsValid) return View();

            try
            {
                facade.CreateNewPackage(viewModel.Folder, viewModel.Name);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
                return View();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ClearCompletedTasks()
        {
            facade.CleanTasksHistory();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult AddManualPeer() => throw new NotImplementedException();
    }
}
