using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ShareCluster.WebInterface.Models
{
    public class PackageIdDto
    {
        [Required]
        public Id PackageId { get; set; }
    }
}
