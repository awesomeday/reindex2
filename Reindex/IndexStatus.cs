using System;

namespace Reindex
{
    public class IndexStatus
    {
        private const int Delta = 1000;

        public string Date { get; set; }

        public bool V1Exists { get; set; }

        public bool V2Exists { get; set; }

        public int DocsInV1 { get; set; }

        public int DocsInV2 { get; set; }

        public TaskInfo Task { get; set; }

        public string OldIndexName => "articles-" + Date;

        public string NewIndexName => "articlesv2-" + Date;

        public IndexState GetState()
        {
            if(V1Exists && V2Exists && Task != null) return IndexState.InProgress;

            if(V1Exists && !V2Exists) return IndexState.NeedsReindexing;

            var diff = Math.Abs(DocsInV1 - DocsInV2);

            if (V1Exists && V2Exists && Task == null)
            {
                if (diff < Delta && DocsInV2 > 0) return IndexState.ReindexingDone;
                else return IndexState.Inconsistent;
            }

            return IndexState.Finished;
        }
    }
}
