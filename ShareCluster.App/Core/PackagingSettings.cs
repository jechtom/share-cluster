using System;
using System.IO;
using System.Reflection;

namespace ShareCluster.Core
{
    public class PackagingSettings
    {
        /// <summary>
        /// Gets or sets data root path.
        /// This can relative path during settings initialization - then it is converted to absolute path.
        /// </summary>
        public string DataRootPath { get; set; } = @"data";

        /// <summary>
        /// Gets or sets data root folder for storing package data files.
        /// This can be relative path to <see cref="DataRootPath"/> during settings initialization - then it is converted to absolute path.
        /// </summary>
        public string DataRootPathPackageRepository { get; private set; } = @"packages";

        /// <summary>
        /// Gets or sets default data root folder for extracting packages.
        /// This can be relative path to <see cref="DataRootPath"/> during settings initialization - then it is converted to absolute path.
        /// </summary>
        public string DataRootPathExtractDefault { get; private set; } = @"extracted";

        /// <summary>
        /// Updates all properties to contains absolute paths.
        /// </summary>
        public void ResolveAbsolutePaths()
        {
            if (string.IsNullOrEmpty(DataRootPath))
            {
                throw new InvalidOperationException($"Invalid value of property {nameof(DataRootPath)}");
            }

            if (string.IsNullOrEmpty(DataRootPathPackageRepository))
            {
                throw new InvalidOperationException($"Invalid value of property {nameof(DataRootPathPackageRepository)}");
            }

            if (string.IsNullOrEmpty(DataRootPathExtractDefault))
            {
                throw new InvalidOperationException($"Invalid value of property {nameof(DataRootPathExtractDefault)}");
            }

            // remark: Path.Combine will ignore first parameter if second param is absolute

            string appRootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            DataRootPath = Path.Combine(DataRootPath, appRootPath);
            DataRootPathPackageRepository = Path.Combine(DataRootPath, DataRootPathPackageRepository);
            DataRootPathExtractDefault = Path.Combine(DataRootPath, DataRootPathExtractDefault);
        }
    }
}
