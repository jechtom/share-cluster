using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShareCluster.Packaging.IO;

namespace ShareCluster.Packaging.PackageFolders
{
    public class PackageFolderDataAccessorBuilder
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly PackageFolderRepository _packageFolderManager;
        private readonly PackageFolderDataValidator _packageFolderDataValidator;

        public PackageFolderDataAccessorBuilder(ILoggerFactory loggerFactory, PackageFolderRepository packageFolderManager, PackageFolderDataValidator packageFolderDataValidator)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _packageFolderManager = packageFolderManager ?? throw new ArgumentNullException(nameof(packageFolderManager));
            _packageFolderDataValidator = packageFolderDataValidator ?? throw new ArgumentNullException(nameof(packageFolderDataValidator));
        }

        public PackageFolderDataAccessor BuildFor(IPackageFolderReference reference, PackageDefinition packageDefinition)
        {
            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
            }

            throw new NotImplementedException();
        }
    }
}
