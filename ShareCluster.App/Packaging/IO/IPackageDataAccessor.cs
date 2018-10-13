using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ShareCluster.Packaging.IO
{
    public interface IPackageDataAccessor
    {
        IStreamSplitterController CreateReadAllPackageData();
        IStreamSplitterController CreateReadSpecificPackageData(int[] parts);
        IStreamSplitterController CreateWriteSpecificPackageData(int[] parts);
        IStoreNewPackageAccessor CreateStoreNewPackageAccessor();
        Task<PackageDataValidatorResult> ValidatePackageDataAsync(MeasureItem measureItem);
        void DeletePackage();
    }
}
