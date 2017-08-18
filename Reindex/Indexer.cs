using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;

namespace Reindex
{
    public class Indexer
    {
        Regex dateRgx = new Regex(@"\d{4}.\d{2}.\d{2}");

        readonly HttpClient client = new HttpClient();
        private Dictionary<string, IndexStatus> status = new Dictionary<string, IndexStatus>();
        private const int maxTasksPerHost = 3;

        // Prod
        private const string masterNode = "10.1.1.141";
        readonly Dictionary<string, int> hosts = new Dictionary<string, int>
        {
            { "10.1.1.141", 0 },
            { "10.1.1.142", 0 },
            { "10.1.1.143", 0 },
            { "10.1.1.144", 0 },
            { "10.1.1.145", 0 },
            { "10.1.1.146", 0 }
        };

        // PP
        //private const string masterNode = "192.168.10.81";
        //readonly Dictionary<string, int> hosts = new Dictionary<string, int>
        //{
        //    { "192.168.10.81", 0 },
        //    { "192.168.10.82", 0 }
        //};

        public void Run()
        {
            while (true)
            {
                RunInternal();
                Thread.Sleep(10000);
            }
        }

        private void RunInternal()
        {
            // reset hosts
            foreach (var host in hosts.Keys.ToList())
            {
                hosts[host] = 0;
            }

            // reset status
            status = new Dictionary<string, IndexStatus>();

            RequestIndexes();
            var tasks = GetTaskInfo();

            foreach (var task in tasks)
            {
                var date = dateRgx.Match(task.Description).Value;

                if (status.ContainsKey(date))
                {
                    status[date].Task = task;
                }

                if (hosts.ContainsKey(task.Host))
                    hosts[task.Host]++;
            }

            CreateAliases();
            CreateTask();
            PrintStatus();
        }

        public void RequestIndexes()
        {
            var res = client.GetAsync($"http://{masterNode}:9200/_cat/indices").Result.Content.ReadAsStringAsync().Result;

            var lines = res.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if(string.IsNullOrWhiteSpace(line)) continue;

                var split = line.Split(' ').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                if(split.Count < 10) continue;

                var indexName = split[2];
                var docsCount = split[6];

                if(!indexName.StartsWith("articles"))
                    continue;

                var nameSplit = indexName.Split('-');
                if(nameSplit.Length < 2) continue;

                if (!status.ContainsKey(nameSplit[1]))
                    status.Add(nameSplit[1], new IndexStatus {Date = nameSplit[1] });

                if (indexName.StartsWith("articlesv2"))
                {
                    status[nameSplit[1]].V2Exists = true;
                    status[nameSplit[1]].DocsInV2 = Int32.Parse(docsCount);
                }
                else
                {
                    status[nameSplit[1]].V1Exists = true;
                    status[nameSplit[1]].DocsInV1 = Int32.Parse(docsCount);
                }
            }

            status = status.OrderByDescending(p => p.Key).ToDictionary(p => p.Key, p => p.Value);
        }

        public List<TaskInfo> GetTaskInfo()
        {
            var state = GetTasksState();

            var res = new List<TaskInfo>();

            if (state.Nodes != null)
            {
                foreach (var node in state.Nodes)
                {
                    foreach (var task in node.Value.Tasks)
                    {
                        res.Add(new TaskInfo
                        {
                            Host = node.Value.Host,
                            Description = task.Value.Description,
                            StartedAt = new DateTime(1970, 1, 1).AddMilliseconds(task.Value.StartTimeMs).ToLocalTime(),
                            Done = task.Value.Status.Done,
                            Total = task.Value.Status.Total
                        });
                    }
                }
            }

            return res;
        }

        private TasksState GetTasksState()
        {
            var res = client.GetAsync($"http://{masterNode}:9200/_tasks/?detailed=true&actions=*reindex").Result.Content.ReadAsStringAsync().Result;
            return JsonConvert.DeserializeObject<TasksState>(res);
        }

        private void PrintStatus()
        {
            Console.Clear();
            
            Console.WriteLine("Finished: " + status.Count(p => p.Value.GetState() == IndexState.Finished));
            Console.WriteLine("In progress: " + status.Count(p => p.Value.GetState() == IndexState.InProgress));
            Console.WriteLine("Need reindexing: " + status.Count(p => p.Value.GetState() == IndexState.NeedsReindexing));
            Console.WriteLine("Reindexing done: " + status.Count(p => p.Value.GetState() == IndexState.ReindexingDone));
            Console.WriteLine("Inconsistent: " + status.Count(p => p.Value.GetState() == IndexState.Inconsistent));
            Console.WriteLine();

            var inProgress = status.Where(p => p.Value.Task != null).ToList();

            foreach (var s in inProgress)
            {
                var timeRemaining = s.Value.Task.TimeRemaining();
                var elapsed = (DateTime.Now - s.Value.Task.StartedAt).TotalSeconds;
                var timeTotal = DateTime.Now - s.Value.Task.StartedAt + timeRemaining;

                var speed = (int)(s.Value.Task.Done / elapsed);

                Console.WriteLine($"{s.Key} {s.Value.Task.StartedAt:HH:mm:ss} {s.Value.Task.Total}\t\tEst: {timeTotal:hh\\:mm\\:ss}\tRem: {timeRemaining:hh\\:mm\\:ss}\t{speed}");
            }
        }

        private void CreateIndex(string indexName)
        {
            var res = client.PutAsync($"http://{masterNode}:9200/{indexName}", new StringContent(IndexTemplate.Template)).Result;
        }

        private void StartReindex(string server, string from, string to)
        {
            var post = @"{{
                    ""conflicts"": ""proceed"",
                    ""source"": {{
                        ""index"": ""{0}""
                    }},
                    ""dest"": {{
                        ""index"": ""{1}"",
                        ""op_type"": ""create""
                    }}}}";

            var req = string.Format(post, from, to);

            var res = client.PostAsync($"http://{server}:9200/_reindex?wait_for_completion=false", 
                new StringContent(req)).Result;
        }

        private void Optimize(string indexName)
        {
            var res = client.PostAsync($"http://{masterNode}:9200/{indexName}/_forcemerge", new StringContent("{}")).Result;
        }

        private void CreateTask()
        {
            var needReindex = status.Where(p => p.Value.GetState() == IndexState.NeedsReindexing).Select(p => p.Value).FirstOrDefault();
            if(needReindex == null) return;

            var server = GetFreeServer();
            if(server == null) return;

            CreateIndex(needReindex.NewIndexName);
            StartReindex(server, needReindex.OldIndexName, needReindex.NewIndexName);
        }

        private void CreateAliases()
        {
            var done = status.Where(p => p.Value.GetState() == IndexState.ReindexingDone).Select(p => p.Value).ToList();

            foreach (var index in done)
            {
                Optimize(index.NewIndexName);
                CreateAlias(index);
            }
        }

        private void CreateAlias(IndexStatus index)
        {
            var post = @"
                    {{
                        ""actions"" : [
                            {{ ""add"":  {{ ""index"": ""{0}"", ""alias"": ""{1}"" }}}},
                            {{ ""remove_index"": {{ ""index"": ""{2}"" }}}}  
                        ]
                    }}";

            var req = string.Format(post, index.NewIndexName, index.OldIndexName, index.OldIndexName);
        
            var res = client.PostAsync($"http://{masterNode}:9200/_aliases", new StringContent(req)).Result;
        }

        private string GetFreeServer()
        {
            foreach (var host in hosts)
            {
                if (host.Value < maxTasksPerHost)
                    return host.Key;
            }

            return null;
        }
    }
}
