﻿using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Http;
using ShareCluster.WebInterface.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShareCluster.WebInterface
{
    [ServiceFilter(typeof(HttpFilterOnlyLocal))]
    public class ClientApiController : Controller
    {
        private readonly WebFacade _facade;

        public ClientApiController(WebFacade facade)
        {
            _facade = facade ?? throw new ArgumentNullException(nameof(facade));
        }

        [HttpPost, ActionName("PACKAGE_DOWNLOAD")]
        public IActionResult StartDownloadDiscoveredPackage([FromBody] PackageIdDto request)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryStartDownloadRemotePackage(request.PackageId);
            return Ok();
        }
        
        [HttpPost, ActionName("PACKAGE_DOWNLOAD_STOP")]
        public IActionResult StopDownloadPackage([FromBody] PackageIdDto request)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryChangeDownloadPackage(request.PackageId, start: false);
            return Ok();
        }

        [HttpPost, ActionName("PACKAGE_VERIFY")]
        public IActionResult StartVerifyPackage([FromBody] PackageIdDto request)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryVerifyPackage(request.PackageId);
            return Ok();
        }

        [HttpPost, ActionName("PACKAGE_DELETE")]
        public IActionResult DeletePackage([FromBody] PackageIdDto request)
        {
            if (!ModelState.IsValid) return BadRequest();

            PackageOperationViewModel package;
            if ((package = _facade.GetPackageOrNull(request.PackageId)) == null) return NotFound();

            try
            {
                _facade.DeletePackage(package.Id);
            }
            catch(Exception e)
            {
                ModelState.AddModelError(nameof(package.Id), e.Message);
                return View(package);
            }

            return Ok();
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

            return Ok();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CreatePackage(CreatePackageViewModel viewModel)
        {
            if (!ModelState.IsValid) return View();

            try
            {
                _facade.CreateNewPackage(viewModel.Folder, viewModel.Name);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
                return View();
            }

            return Ok();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ClearCompletedTasks()
        {
            _facade.CleanTasksHistory();
            return Ok();
        }
    }
}