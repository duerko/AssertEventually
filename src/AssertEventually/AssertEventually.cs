namespace AssertEventually
{
    public class AssertEventually
    {

        public static EventuallyAssertion<T> That<T>(
            Action<T> assertion)
        {
            return new EventuallyAssertion<T>(assertion);
        }

        public static EventuallyAssertion<T> That<T>(
            string description,
            Action<T> assertion)
        {
            return new EventuallyAssertion<T>(description, assertion);
        }

        public static EventuallyAssertion<T> That<T>(
            Func<T, Task> assertion)
        {
            return new EventuallyAssertion<T>(assertion);
        }

        public static EventuallyAssertion<T> That<T>(
            string description,
            Func<T, Task> assertion)
        {
            return new EventuallyAssertion<T>(description, assertion);
        }
    }
}