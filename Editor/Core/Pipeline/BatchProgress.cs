namespace SemanticSearch.Editor.Core.Pipeline
{
    public class BatchProgress
    {
        public int Total { get; }
        public int Completed { get; }
        public int Succeeded { get; }
        public int Failed { get; }
        public int Skipped { get; }
        public string CurrentAsset { get; }
        public float Progress => Total > 0 ? (float)Completed / Total : 0f;

        public BatchProgress(int total, int completed, int succeeded, int failed, int skipped, string currentAsset)
        {
            Total = total;
            Completed = completed;
            Succeeded = succeeded;
            Failed = failed;
            Skipped = skipped;
            CurrentAsset = currentAsset;
        }
    }
}
