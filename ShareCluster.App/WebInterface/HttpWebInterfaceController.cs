using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShareCluster.WebInterface
{
    [ServiceFilter(typeof(HttpFilterOnlyLocal))]
    public class HttpWebInterfaceController : Controller
    {
        private readonly WebFacade _facade;

        public HttpWebInterfaceController(WebFacade facade)
        {
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        }

        public IActionResult Index() => View(model: _facade.GetStatusViewModel());

        public IActionResult Test()
        {
            return Json(_facade.GetStatusViewModel().Packages.Select(p => new
            {
                Id = p.Value.Id.ToString(),
                Name = p.Value.Metadata.Name,
                SizeFormatted = SizeFormatter.ToString(p.Value.SplitInfo.PackageSize)
            }));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StartDownloadDiscoveredPackage(Id packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryStartDownloadRemotePackage(packageId);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StartDownloadPackage(Id packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryChangeDownloadPackage(packageId, start: true);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StopDownloadPackage(Id packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryChangeDownloadPackage(packageId, start: false);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult StartVerifyPackage(Id packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryVerifyPackage(packageId);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult DeletePackage(Id packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            PackageOperationViewModel package;
            if ((package = _facade.GetPackageOrNull(packageId)) == null) return NotFound();
            return View(package);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DeletePackage(Id packageId, object _)
        {
            if (!ModelState.IsValid) return BadRequest();

            PackageOperationViewModel package;
            if ((package = _facade.GetPackageOrNull(packageId)) == null) return NotFound();

            try
            {
                _facade.DeletePackage(package.Id);
            }
            catch(Exception e)
            {
                ModelState.AddModelError(nameof(package.Id), e.Message);
                return View(package);
            }

            return RedirectToAction(nameof(Index));
        }

        public IActionResult ExtractPackage(Id packageId)
        {
            if (!ModelState.IsValid) return BadRequest();
            PackageOperationViewModel package;
            if ((package = _facade.GetPackageOrNull(packageId)) == null) return NotFound();
            var model = new ExtractPackageViewModel()
            {
                DoValidate = true,
                Folder = _facade.RecommendFolderForExtraction(),
                Package = package
            };
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ExtractPackage(Id packageId, ExtractPackageViewModel viewModel)
        {
            if (!ModelState.IsValid) return View();

            PackageOperationViewModel package;
            if ((package = _facade.GetPackageOrNull(packageId)) == null) return NotFound();

            try
            {
                _facade.ExtractPackage(package.Id, viewModel.Folder, viewModel.DoValidate);
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
                _facade.CreateNewPackage(viewModel.Path, viewModel.Name);
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
            _facade.CleanTasksHistory();
            return RedirectToAction(nameof(Index));
        }

        public IActionResult AddManualPeer() => throw new NotImplementedException();
    }
}
