namespace Octopus.Tentacle.Tests.Integration.Util
{
    public class Reference<T>
    {
        public Reference()
        {
        }

        public Reference(T value)
        {
            Value = value;
        }

        public T Value { get; set; }
    }
}