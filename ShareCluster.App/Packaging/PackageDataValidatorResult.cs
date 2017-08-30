using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ShareCluster.Packaging
{
    /// <summary>
    /// Describes validation result of <see cref="PackageDataValidator"/>
    /// </summary>
    public class PackageDataValidatorResult
    {
        private PackageDataValidatorResult() { }

        public static PackageDataValidatorResult Valid => new PackageDataValidatorResult() {
            IsValid = true,
            Errors = Array.Empty<string>().ToImmutableArray()
        };

        public static PackageDataValidatorResult WithErrors(IEnumerable<string> errors) => new PackageDataValidatorResult()
        {
            IsValid = false,
            Errors = errors.ToImmutableArray()
        };

        public static PackageDataValidatorResult WithError(string error) => WithErrors(new string[] { error });

        public bool IsValid { get; private set; }
        public IReadOnlyCollection<string> Errors { get; private set; }

    }
}