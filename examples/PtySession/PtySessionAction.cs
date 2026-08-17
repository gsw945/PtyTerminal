namespace PtySession
{
    public class PtySessionAction<T>
    {
        public enum ActionType
        {
            list,
            create,
            attach,
            detach,
            destroy,
            resize
        }

        public ActionType action { get; set; }
        public T? data { get; set; }
    }

    public class PtySessionActionData
    {
        public string? id { get; set; }
        public int cols { get; set; }
        public int rows { get; set; }
    }
}
