using Newtonsoft.Json;
using ShareCluster.Packaging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ShareCluster.WebInterface
{
    public class CreatePackageViewModel
    {
        public string Path { get; set; }

        public string Name { get; set; }

        [JsonProperty("package_type")]
        public string PackageType { get; set; }

        [JsonProperty("group_use")]
        public bool GroupUse { get; set; }

        [JsonProperty("group_id")]
        public Id? GroupId { get; set; }
    }
}
