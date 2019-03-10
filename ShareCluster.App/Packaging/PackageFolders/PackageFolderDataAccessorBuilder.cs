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
        private readonly PackageFolderDataValidator _packageFolderDataValidator;

        public PackageFolderDataAccessorBuilder(ILoggerFactory loggerFactory, PackageFolderDataValidator packageFolderDataValidator)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _packageFolderDataValidator = packageFolderDataValidator ?? throw new ArgumentNullException(nameof(packageFolderDataValidator));
        }

        public PackageFolderDataAccessor BuildFor(PackageFolderRepository packageFolderRepository, IPackageFolderReference reference, PackageContentDefinition packageDefinition)
        {
            if (packageFolderRepository == null)
            {
                throw new ArgumentNullException(nameof(packageFolderRepository));
            }

            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (packageDefinition == null)
            {
                throw new ArgumentNullException(nameof(packageDefinition));
            }

            var result = new PackageFolderDataAccessor(_loggerFactory, packageFolderRepository, packageDefinition, reference, _packageFolderDataValidator);
            return result;
        }
    }
}
