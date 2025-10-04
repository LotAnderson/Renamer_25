using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;

namespace RenamerTest
{
    internal static class PathSafety
    {
        public static bool IsDangerousPath(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return true;

            string full = Path.GetFullPath(dir.Trim());
            string driveRoot = Path.GetPathRoot(full) ?? "";

            if (Regex.IsMatch(dir.Trim(), @"^[A-Za-z]:$")) return true; // C:
            if (string.Equals(full.TrimEnd('\\', '/'), driveRoot.TrimEnd('\\', '/'),
                              StringComparison.OrdinalIgnoreCase)) return true; // C:\

            string windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrEmpty(windowsDir) &&
                full.StartsWith(Path.GetFullPath(windowsDir), StringComparison.OrdinalIgnoreCase))
                return true;

            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(pf) &&
                full.StartsWith(Path.GetFullPath(pf), StringComparison.OrdinalIgnoreCase))
                return true;

            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf86) &&
                full.StartsWith(Path.GetFullPath(pf86), StringComparison.OrdinalIgnoreCase))
                return true;

            string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (!string.IsNullOrEmpty(pd) &&
                full.StartsWith(Path.GetFullPath(pd), StringComparison.OrdinalIgnoreCase))
                return true;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile))
            {
                string usersContainer = Path.GetFullPath(Path.Combine(userProfile, ".."));
                if (string.Equals(full.TrimEnd('\\', '/'), usersContainer.TrimEnd('\\', '/'),
                                  StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(full.TrimEnd('\\', '/'), userProfile.TrimEnd('\\', '/'),
                                  StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
