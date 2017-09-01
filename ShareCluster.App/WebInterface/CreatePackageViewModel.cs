using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ShareCluster.WebInterface
{
    public class CreatePackageViewModel
    {
        [Required]
        public string Folder { get; set; }

        public string Name { get; set; }
    }
}
