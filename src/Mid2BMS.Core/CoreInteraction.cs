using System;

namespace Mid2BMS
{
    // The desktop host supplies the UI implementation. Core never references WinForms.
    public interface ICoreInteraction
    {
        void ShowMessage(string message);
        void ShowMessage(string message, string caption);
        bool ConfirmAbort(string message, string caption);
    }

    public static class CoreInteraction
    {
        private static ICoreInteraction current = new UnconfiguredInteraction();

        public static ICoreInteraction Current
        {
            get { return current; }
            set { current = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        public static void ShowMessage(string message)
        {
            Current.ShowMessage(message);
        }

        public static void ShowMessage(string message, string caption)
        {
            Current.ShowMessage(message, caption);
        }

        public static bool ConfirmAbort(string message, string caption)
        {
            return Current.ConfirmAbort(message, caption);
        }

        private sealed class UnconfiguredInteraction : ICoreInteraction
        {
            private static InvalidOperationException MissingHost()
            {
                return new InvalidOperationException("Core interaction requires a host implementation.");
            }

            public void ShowMessage(string message) { throw MissingHost(); }
            public void ShowMessage(string message, string caption) { throw MissingHost(); }
            public bool ConfirmAbort(string message, string caption) { throw MissingHost(); }
        }
    }
}
