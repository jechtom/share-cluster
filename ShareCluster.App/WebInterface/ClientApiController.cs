using Microsoft.AspNetCore.Mvc;
using ShareCluster.Network.Protocol;
using ShareCluster.WebInterface.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ShareCluster.WebInterface
{
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
            return Ok(GenericResultDto.Ok);
        }

        [HttpPost, ActionName("PACKAGE_VERIFY")]
        public IActionResult StartVerifyPackage([FromBody] PackageIdDto request)
        {
            if (!ModelState.IsValid) return BadRequest();
            _facade.TryVerifyPackage(request.PackageId);
            return Ok(GenericResultDto.Ok);
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

            return Ok(GenericResultDto.Ok);
        }

        [HttpPost, ActionName("EXTRACT_PACKAGE")]
        public IActionResult ExtractPackage([FromBody] ExtractPackageViewModel viewModel)
        {
            if (!ModelState.IsValid) return BadRequest(GenericResultDto.Failed(
                ModelState.SelectMany(ms => ms.Value.Errors).FirstOrDefault()?.ErrorMessage
            ));

            PackageOperationViewModel package;
            if ((package = _facade.GetPackageOrNull(viewModel.PackageId)) == null) return NotFound();

            try
            {
                _facade.ExtractPackage(package.Id, viewModel.Path, validate: false);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
                return BadRequest(error: GenericResultDto.Failed(e.Message));
            }

            return Ok(GenericResultDto.Ok);
        }

        [HttpPost, ActionName("CREATE_PACKAGE")]
        public ObjectResult CreatePackage([FromBody] CreatePackageViewModel viewModel)
        {
            if (!ModelState.IsValid) return BadRequest(GenericResultDto.Failed(
                ModelState.SelectMany(ms => ms.Value.Errors).FirstOrDefault()?.ErrorMessage
            ));

            try
            {
                _facade.CreateNewPackage(viewModel.Path, viewModel.Name, viewModel.GroupUse ? viewModel.GroupId : null);
            }
            catch (Exception e)
            {
                ModelState.AddModelError(string.Empty, e.Message);
                return BadRequest(error: GenericResultDto.Failed(e.Message));
            }

            return Ok(GenericResultDto.Ok);
        }

        [HttpPost, ActionName("TASKS_DISMISS")]
        public IActionResult ClearCompletedTasks()
        {
            _facade.CleanTasksHistory();
            return Ok(GenericResultDto.Ok);
        }
    }
}
