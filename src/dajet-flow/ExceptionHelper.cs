namespace DaJet.Flow
{
    internal static class ExceptionHelper
    {
        internal static string GetErrorMessage(Exception error)
        {
            if (error == null)
            {
                return string.Empty;
            }

            Exception current = error;
            string message = string.Empty;
            
            while (current != null)
            {
                if (message != string.Empty)
                {
                    message += Environment.NewLine;
                }
                message += current.Message;

                current = current.InnerException;
            }

            return message;
        }
        internal static string GetErrorMessageAndStackTrace(Exception error)
        {
            if (error == null)
            {
                return string.Empty;
            }

            string message = GetErrorMessage(error);

            if (!string.IsNullOrWhiteSpace(error.StackTrace))
            {
                message += Environment.NewLine + error.StackTrace;
            }

            return message;
        }
    }
}