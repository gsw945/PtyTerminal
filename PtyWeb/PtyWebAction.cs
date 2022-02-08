namespace PtyWeb
{
    public class PtyWebAction<T>
    {
        public enum ActionType
        {
            resize
        }

        public ActionType action { get; set; }
        public T data { get; set; }
    }
}
