using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ShareCluster.WebInterface
{
    public class ExtractPackageViewModel
    {
        [Required]
        public string Folder { get; set; }

        public bool DoValidate { get; set; }

        public PackageOperationViewModel Package { get; set; }
    }
}
