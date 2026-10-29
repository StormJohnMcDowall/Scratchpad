namespace Scratchpad.Core
{
    public static class InstanceMessage
    {
        public const string Show = "SHOW";

        public static bool IsShowCommand(string message)
        {
            return message != null && message.Trim() == Show;
        }
    }
}
