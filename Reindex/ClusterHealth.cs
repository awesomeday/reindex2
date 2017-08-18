using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Reindex
{
    [DataContract]
    class ClusterHealth
    {
        [DataMember(Name = "cluster_name")]
        public string ClusterName { get; set; }
    }
}
