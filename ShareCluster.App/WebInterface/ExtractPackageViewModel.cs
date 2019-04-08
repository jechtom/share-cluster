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
        public string Path { get; set; }

        [Required]
        public Id PackageId { get; set; }
    }
}
