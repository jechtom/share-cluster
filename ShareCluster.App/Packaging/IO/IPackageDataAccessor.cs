using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.IO
{
    public interface IPackageDataAccessor
    {
        IStreamController CreateReadAllPackageData();
        IStreamController CreateReadSpecificPackageData(int[] parts);
        IStreamController CreateWriteSpecificPackageData(int[] parts);
        Task<PackageDataValidatorResult> ValidatePackageDataAsync(MeasureItem measureItem);
        void DeletePackage();
        void UpdatePackageMeta(PackageMetadata metadata);
        void UpdatePackageDownloadStatus(PackageDownloadStatus status);
    }
}
