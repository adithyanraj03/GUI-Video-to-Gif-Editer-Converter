using System;
using System.IO;
using System.Windows;

namespace MP4toGIFConverter
{
    public static class SecurityValidator
    {
        private const string AUTHOR_NAME = "Adithyanraj";
        
        public static bool ValidateApplication(Window mainWindow)
        {
            // For debugging - allow running without check
            #if DEBUG
            return true;
            #endif
            
            try
            {
                // Just check if the title contains the author name
                if (mainWindow.Title.Contains(AUTHOR_NAME))
                {
                    return true;
                }
                
                ShowSecurityError();
                return false;
            }
            catch (Exception ex)
            {
                // Log error to help diagnose issues
                try { File.WriteAllText("security_error.log", ex.ToString()); } catch { }
                
                ShowSecurityError();
                return false;
            }
        }
        
        private static void ShowSecurityError()
        {
            MessageBox.Show(
                "This application has been modified and cannot run.\n\n" + 
                "Please obtain an original copy from the author.",
                "Security Validation Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            
            Environment.Exit(1);
        }
    }
}