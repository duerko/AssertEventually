namespace AssertEventually
{
    /// <summary>Creates fluent eventual-consistency assertions.</summary>
    public class AssertEventually
    {
        /// <summary>Creates an assertion for a synchronous delegate.</summary>
        public static EventuallyAssertion<T> That<T>(
            Action<T> assertion)
        {
            return new EventuallyAssertion<T>(assertion);
        }

        /// <summary>Creates a described assertion for a synchronous delegate.</summary>
        public static EventuallyAssertion<T> That<T>(
            string description,
            Action<T> assertion)
        {
            return new EventuallyAssertion<T>(description, assertion);
        }

        /// <summary>Creates an assertion for an asynchronous delegate.</summary>
        public static EventuallyAssertion<T> That<T>(
            Func<T, Task> assertion)
        {
            return new EventuallyAssertion<T>(assertion);
        }

        /// <summary>Creates a described assertion for an asynchronous delegate.</summary>
        public static EventuallyAssertion<T> That<T>(
            string description,
            Func<T, Task> assertion)
        {
            return new EventuallyAssertion<T>(description, assertion);
        }
    }
}