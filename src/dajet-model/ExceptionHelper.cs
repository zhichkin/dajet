using System;

namespace DaJet
{
    public static class ExceptionHelper
    {
        public static string GetErrorMessage(Exception error)
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
        public static string GetErrorMessageAndStackTrace(Exception error)
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