namespace AssertEventually
{
    public class AssertEventually
    {

        public static EventuallyAssertion<T> That<T>(
            Action<T> assertion)
        {
            return new EventuallyAssertion<T>(assertion);
        }
    }
}