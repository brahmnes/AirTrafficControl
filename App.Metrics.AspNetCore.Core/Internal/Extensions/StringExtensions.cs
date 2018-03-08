// <copyright file="StringExtensions.cs" company="Allan Hardy">
// Copyright (c) Allan Hardy. All rights reserved.
// </copyright>

using System.Diagnostics;

// ReSharper disable CheckNamespace
namespace System
    // ReSharper restore CheckNamespace
{
    public static class StringExtensions
    {
        [DebuggerStepThrough]
        public static string EnsureLeadingSlash(this string url)
        {
            if (!url.StartsWith("/"))
            {
                return "/" + url;
            }

            return url;
        }

        [DebuggerStepThrough]
        public static string EnsureTrailingSlash(this string url)
        {
            if (!url.EndsWith("/"))
            {
                return url + "/";
            }

            return url;
        }

        [DebuggerStepThrough]
        public static string GetSafeString(Func<string> action)
        {
            try
            {
                return action();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        [DebuggerStepThrough]
        public static bool IsMissing(this string value) { return string.IsNullOrWhiteSpace(value); }

        [DebuggerStepThrough]
        public static bool IsPresent(this string value) { return !string.IsNullOrWhiteSpace(value); }

        [DebuggerStepThrough]
        public static string RemoveLeadingSlash(this string url)
        {
            if (url != null && url.StartsWith("/"))
            {
                url = url.Substring(1);
            }

            return url;
        }
    }
}