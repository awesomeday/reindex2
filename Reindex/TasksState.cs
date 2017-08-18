using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Reindex
{
    [DataContract]
    public class TasksState
    {
        [DataMember(Name = "nodes")]
        public Dictionary<string, NodeTaskState> Nodes { get; set; }
    }

    [DataContract]
    public class NodeTaskState
    {
        [DataMember(Name = "host")]
        public string Host { get; set; }

        [DataMember(Name = "tasks")]
        public Dictionary<string, TaskDetails> Tasks { get; set; }
    }

    [DataContract]
    public class TaskDetails
    {
        [DataMember(Name = "description")]
        public string Description { get; set; }

        [DataMember(Name = "status")]
        public TaskDetailStatus Status { get; set; }

        [DataMember(Name = "running_time_in_nanos")]
        public long Nanos { get; set; }

        [DataMember(Name = "start_time_in_millis")]
        public long StartTimeMs { get; set; }
    }

    [DataContract]
    public class TaskDetailStatus
    {
        [DataMember(Name = "total")]
        public int Total { get; set; }

        [DataMember(Name = "created")]
        public int Done { get; set; }
    }
}
