using System;
using System.Collections.Generic;
using System.Text;

namespace ShareCluster.Packaging
{
    public static class IHasPackageIdExtensions
    {
        public static void ThrowIfNullOrDifferentPackageId<T>(this T instance, PackageId expectedPackageId) where T: class, IHasPackageId
        {
            if (instance == null)
            {
                throw new InvalidOperationException($"Instance of {typeof(T).Name} cannot be null.");
            }

            if (instance.PackageId != expectedPackageId)
            {
                throw new InvalidOperationException($"{nameof(instance.PackageId)} of {typeof(T).Name} is {instance.PackageId:s} but expected was {expectedPackageId:s}.");
            }
        }
    }
}
